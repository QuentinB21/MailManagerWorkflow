using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Services;

public sealed partial class OutlookMailboxService(
    MailManagerDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    OutlookOAuthService oauthService,
    EmailProcessingService processingService,
    ILogger<OutlookMailboxService> logger) : IMailboxProviderAdapter
{
    private const int MaximumBodyLength = 50_000;
    public MailProvider Provider => MailProvider.Outlook;

    public async Task<MailboxConnectionTestResponse?> TestConnectionAsync(
        Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        var mailbox = await GetMailboxAsync(mailboxConnectionId, false, cancellationToken);
        if (mailbox?.EncryptedRefreshToken is null) return null;
        var accessToken = await oauthService.GetAccessTokenAsync(mailbox, cancellationToken);
        var profile = await GetJsonAsync(
            accessToken,
            "https://graph.microsoft.com/v1.0/me?$select=mail,userPrincipalName",
            cancellationToken);
        var email = profile.TryGetProperty("mail", out var mail) ? mail.GetString() : null;
        email ??= profile.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() : null;
        return new MailboxConnectionTestResponse(true, email ?? mailbox.EmailAddress ?? string.Empty);
    }

    public async Task<MailboxSyncResponse?> SyncAsync(
        Guid mailboxConnectionId,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var mailbox = await GetMailboxAsync(mailboxConnectionId, true, cancellationToken);
        if (mailbox?.EncryptedRefreshToken is null) return null;

        try
        {
            var accessToken = await oauthService.GetAccessTokenAsync(mailbox, cancellationToken);
            var messages = await ListNewInboxMessagesAsync(
                accessToken,
                mailbox.ConnectedAt ?? DateTimeOffset.UtcNow,
                cancellationToken);
            var messageIds = messages.Select(message => message.Id).ToArray();
            var existingLogs = await dbContext.ProcessingLogs
                .Where(log => log.MailboxConnectionId == mailboxConnectionId
                    && messageIds.Contains(log.ExternalMessageId))
                .ToDictionaryAsync(log => log.ExternalMessageId, cancellationToken);
            var candidates = messages
                .Where(message => !existingLogs.TryGetValue(message.Id, out var log)
                    || string.IsNullOrWhiteSpace(log.SubjectPreview)
                    || (log.IsClassified && log.ProviderLabelAppliedAt is null))
                .Take(maxResults)
                .ToArray();

            var results = new List<MailboxMessageProcessingResult>();
            foreach (var message in candidates)
            {
                try
                {
                    var email = Normalize(mailboxConnectionId, message);
                    if (existingLogs.TryGetValue(message.Id, out var existingLog)
                        && string.IsNullOrWhiteSpace(existingLog.SubjectPreview))
                    {
                        existingLog.SubjectPreview = EmailProcessingService.CreateSubjectPreview(email.Subject) ?? "Sans objet";
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }

                    var classification = await processingService.ProcessAsync(email, cancellationToken)
                        ?? throw new InvalidOperationException("La boîte mail n’est plus active.");
                    var destinationApplied = false;
                    if (classification.IsClassified && classification.Label is not null)
                    {
                        var categoryName = await EnsureOutlookCategoryAsync(
                            accessToken,
                            classification.Label.Id,
                            cancellationToken);
                        await ApplyCategoryAsync(accessToken, message, categoryName, cancellationToken);
                        destinationApplied = true;
                        if (classification.ProcessingLogId is Guid logId)
                        {
                            var log = await dbContext.ProcessingLogs.FindAsync([logId], cancellationToken);
                            if (log is not null)
                            {
                                log.ProviderLabelAppliedAt = DateTimeOffset.UtcNow;
                                log.ProviderActionError = null;
                                await dbContext.SaveChangesAsync(cancellationToken);
                            }
                        }
                    }

                    results.Add(new MailboxMessageProcessingResult(
                        message.Id,
                        EmailProcessingService.CreateSubjectPreview(email.Subject),
                        classification.IsClassified,
                        classification.Label,
                        classification.MatchedRule,
                        classification.MatchedCriteria,
                        classification.NoMatchReason,
                        classification.WasAlreadyProcessed,
                        destinationApplied));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "Échec du traitement du message Outlook {MessageId}.", message.Id);
                    var log = await dbContext.ProcessingLogs.FirstOrDefaultAsync(
                        item => item.MailboxConnectionId == mailboxConnectionId
                            && item.ExternalMessageId == message.Id,
                        cancellationToken);
                    if (log is not null)
                    {
                        log.ProviderActionError = SafeError(exception);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    results.Add(new MailboxMessageProcessingResult(
                        message.Id, message.Subject, false, null, null, [], null, false, false, SafeError(exception)));
                }
            }

            mailbox.LastSyncAt = DateTimeOffset.UtcNow;
            mailbox.LastSyncError = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new MailboxSyncResponse(
                maxResults,
                messages.Count,
                results.Count,
                results.Count(item => item.IsClassified),
                results.Count(item => item.DestinationApplied),
                results.Count(item => !item.IsClassified && item.Error is null),
                results.Count(item => item.Error is not null),
                results);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            mailbox.LastSyncAt = DateTimeOffset.UtcNow;
            mailbox.LastSyncError = SafeError(exception);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private Task<MailboxConnection?> GetMailboxAsync(
        Guid mailboxConnectionId,
        bool requireActive,
        CancellationToken cancellationToken) =>
        dbContext.MailboxConnections.FirstOrDefaultAsync(
            item => item.Id == mailboxConnectionId
                && item.Provider == MailProvider.Outlook
                && (!requireActive || item.IsActive),
            cancellationToken);

    private async Task<List<GraphMessage>> ListNewInboxMessagesAsync(
        string accessToken,
        DateTimeOffset connectedAt,
        CancellationToken cancellationToken)
    {
        var receivedAfter = connectedAt.UtcDateTime.ToString("O");
        var query = new Dictionary<string, string>
        {
            ["$select"] = "id,subject,from,body,receivedDateTime,categories",
            ["$filter"] = $"receivedDateTime ge {receivedAfter}",
            ["$orderby"] = "receivedDateTime desc",
            ["$top"] = "100"
        };
        var url = "https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages?" + string.Join(
            "&", query.Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value)}"));
        var payload = await GetJsonAsync(accessToken, url, cancellationToken, preferTextBody: true);
        return payload.GetProperty("value").Deserialize<List<GraphMessage>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
    }

    private static NormalizedEmailRequest Normalize(Guid mailboxConnectionId, GraphMessage message)
    {
        var sender = ExtractEmailAddress(message.From?.EmailAddress?.Address);
        var body = message.Body?.Content ?? string.Empty;
        if (message.Body?.ContentType?.Equals("html", StringComparison.OrdinalIgnoreCase) == true)
        {
            body = WebUtility.HtmlDecode(HtmlTagsRegex().Replace(body, " "));
        }
        if (body.Length > MaximumBodyLength) body = body[..MaximumBodyLength];
        return new NormalizedEmailRequest(mailboxConnectionId, message.Id, sender, message.Subject, body);
    }

    private async Task<string> EnsureOutlookCategoryAsync(
        string accessToken,
        Guid labelDefinitionId,
        CancellationToken cancellationToken)
    {
        var label = await dbContext.LabelDefinitions.FirstOrDefaultAsync(
            item => item.Id == labelDefinitionId && item.IsActive,
            cancellationToken) ?? throw new InvalidOperationException("La destination est introuvable ou inactive.");
        var payload = await GetJsonAsync(
            accessToken,
            "https://graph.microsoft.com/v1.0/me/outlook/masterCategories?$select=id,displayName",
            cancellationToken);
        var categories = payload.GetProperty("value").EnumerateArray().ToArray();
        var existing = categories.FirstOrDefault(category =>
            string.Equals(category.GetProperty("displayName").GetString(), label.Name, StringComparison.OrdinalIgnoreCase));
        if (existing.ValueKind == JsonValueKind.Undefined)
        {
            var created = await SendJsonAsync(
                accessToken,
                HttpMethod.Post,
                "https://graph.microsoft.com/v1.0/me/outlook/masterCategories",
                new { displayName = label.Name, color = "preset0" },
                cancellationToken);
            label.ExternalLabelId = created.GetProperty("id").GetString();
        }
        else
        {
            label.ExternalLabelId = existing.GetProperty("id").GetString();
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return label.Name;
    }

    private async Task ApplyCategoryAsync(
        string accessToken,
        GraphMessage message,
        string categoryName,
        CancellationToken cancellationToken)
    {
        var categories = (message.Categories ?? [])
            .Append(categoryName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        await SendJsonAsync(
            accessToken,
            HttpMethod.Patch,
            $"https://graph.microsoft.com/v1.0/me/messages/{Uri.EscapeDataString(message.Id)}",
            new { categories },
            cancellationToken);
    }

    private async Task<JsonElement> GetJsonAsync(
        string accessToken,
        string url,
        CancellationToken cancellationToken,
        bool preferTextBody = false) =>
        await SendJsonAsync(accessToken, HttpMethod.Get, url, null, cancellationToken, preferTextBody);

    private async Task<JsonElement> SendJsonAsync(
        string accessToken,
        HttpMethod method,
        string url,
        object? body,
        CancellationToken cancellationToken,
        bool preferTextBody = false)
    {
        var client = httpClientFactory.CreateClient("MicrosoftGraph");
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (preferTextBody) request.Headers.TryAddWithoutValidation("Prefer", "outlook.body-content-type=\"text\"");
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new OutlookApiException($"Microsoft Graph a refusé la requête ({(int)response.StatusCode}).", details);
        }
        if (response.Content.Headers.ContentLength == 0) return default;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    private static string ExtractEmailAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown@invalid.local";
        try { return new MailAddress(value).Address; }
        catch (FormatException) { return "unknown@invalid.local"; }
    }

    private static string SafeError(Exception exception) => exception switch
    {
        OutlookConfigurationException => exception.Message,
        OutlookApiException => exception.Message,
        _ => "Le traitement du message Outlook a échoué. Consultez les logs techniques."
    };

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagsRegex();

    private sealed record GraphMessage(
        string Id,
        string? Subject,
        GraphRecipient? From,
        GraphBody? Body,
        DateTimeOffset ReceivedDateTime,
        string[]? Categories);
    private sealed record GraphRecipient(GraphEmailAddress? EmailAddress);
    private sealed record GraphEmailAddress(string? Address);
    private sealed record GraphBody(string? ContentType, string? Content);
}
