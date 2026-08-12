using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Services;

public sealed partial class GmailMailboxService(
    MailManagerDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    GmailOAuthService oauthService,
    EmailProcessingService processingService,
    ILogger<GmailMailboxService> logger) : IMailboxProviderAdapter
{
    private const int MaximumBodyLength = 50_000;

    public MailProvider Provider => MailProvider.Gmail;

    public async Task<MailboxConnectionTestResponse?> TestConnectionAsync(
        Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        var mailbox = await dbContext.MailboxConnections.AsNoTracking().FirstOrDefaultAsync(
            item => item.Id == mailboxConnectionId && item.Provider == MailProvider.Gmail,
            cancellationToken);
        if (mailbox?.EncryptedRefreshToken is null) return null;

        var accessToken = await oauthService.GetAccessTokenAsync(
            mailbox.EncryptedRefreshToken,
            cancellationToken);
        var profile = await GetJsonAsync(
            accessToken,
            "https://gmail.googleapis.com/gmail/v1/users/me/profile",
            cancellationToken);
        return new MailboxConnectionTestResponse(true, profile.GetProperty("emailAddress").GetString() ?? mailbox.EmailAddress ?? string.Empty);
    }

    public Task<MailboxSyncResponse?> SyncAsync(
        Guid mailboxConnectionId,
        int maxResults,
        CancellationToken cancellationToken) =>
        ProcessUnreadAsync(mailboxConnectionId, maxResults, cancellationToken);

    public async Task<MailboxSyncResponse?> ProcessUnreadAsync(
        Guid mailboxConnectionId,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var mailbox = await dbContext.MailboxConnections.FirstOrDefaultAsync(
            item => item.Id == mailboxConnectionId
                && item.Provider == MailProvider.Gmail
                && item.IsActive,
            cancellationToken);
        if (mailbox?.EncryptedRefreshToken is null) return null;

        try
        {
            var accessToken = await oauthService.GetAccessTokenAsync(
                mailbox.EncryptedRefreshToken,
                cancellationToken);
            await BackfillMissingSubjectPreviewsAsync(
                accessToken,
                mailboxConnectionId,
                cancellationToken);
            var discoveredIds = await ListNewInboxMessageIdsAsync(
                accessToken,
                mailbox.ConnectedAt ?? DateTimeOffset.UtcNow,
                cancellationToken);

            var existingLogs = await dbContext.ProcessingLogs
                .Where(log => log.MailboxConnectionId == mailboxConnectionId
                    && discoveredIds.Contains(log.ExternalMessageId))
                .ToDictionaryAsync(log => log.ExternalMessageId, cancellationToken);
            var candidateIds = discoveredIds
                .Where(id => !existingLogs.TryGetValue(id, out var log)
                    || string.IsNullOrWhiteSpace(log.SubjectPreview)
                    || (log.IsClassified && log.ProviderLabelAppliedAt is null))
                .Take(maxResults)
                .ToArray();

            var results = new List<MailboxMessageProcessingResult>();
            foreach (var messageId in candidateIds)
            {
                try
                {
                    var email = await GetNormalizedEmailAsync(
                        accessToken,
                        mailboxConnectionId,
                        messageId,
                        cancellationToken);
                    if (existingLogs.TryGetValue(messageId, out var existingLog)
                        && string.IsNullOrWhiteSpace(existingLog.SubjectPreview))
                    {
                        existingLog.SubjectPreview = EmailProcessingService.CreateSubjectPreview(email.Subject);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    var classification = await processingService.ProcessAsync(email, cancellationToken)
                        ?? throw new InvalidOperationException("La boîte mail n’est plus active.");
                    var labelApplied = false;

                    if (classification.IsClassified && classification.Label is not null)
                    {
                        var externalLabelId = await EnsureGmailLabelAsync(
                            accessToken,
                            classification.Label.Id,
                            cancellationToken);
                        await ApplyLabelAsync(accessToken, messageId, externalLabelId, cancellationToken);
                        labelApplied = true;

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
                        messageId,
                        EmailProcessingService.CreateSubjectPreview(email.Subject),
                        classification.IsClassified,
                        classification.Label,
                        classification.MatchedRule,
                        classification.MatchedCriteria,
                        classification.NoMatchReason,
                        classification.WasAlreadyProcessed,
                        labelApplied));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "Échec du traitement du message Gmail {MessageId}.", messageId);
                    var log = await dbContext.ProcessingLogs.FirstOrDefaultAsync(
                        item => item.MailboxConnectionId == mailboxConnectionId
                            && item.ExternalMessageId == messageId,
                        cancellationToken);
                    if (log is not null)
                    {
                        log.ProviderActionError = SafeError(exception);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    results.Add(new MailboxMessageProcessingResult(
                        messageId, null, false, null, null, [], null, false, false, SafeError(exception)));
                }
            }

            mailbox.LastSyncAt = DateTimeOffset.UtcNow;
            mailbox.LastSyncError = null;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new MailboxSyncResponse(
                maxResults,
                discoveredIds.Count,
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

    private async Task BackfillMissingSubjectPreviewsAsync(
        string accessToken,
        Guid mailboxConnectionId,
        CancellationToken cancellationToken)
    {
        var logs = await dbContext.ProcessingLogs
            .Where(log => log.MailboxConnectionId == mailboxConnectionId
                && log.SubjectPreview == null)
            .OrderByDescending(log => log.ProcessedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var log in logs.Where(log => GmailMessageIdRegex().IsMatch(log.ExternalMessageId)))
        {
            try
            {
                var email = await GetNormalizedEmailAsync(
                    accessToken,
                    mailboxConnectionId,
                    log.ExternalMessageId,
                    cancellationToken);
                log.SubjectPreview = EmailProcessingService.CreateSubjectPreview(email.Subject)
                    ?? "Sans objet";
            }
            catch (GmailApiException exception)
            {
                logger.LogInformation(
                    exception,
                    "Impossible de compléter le sujet du message Gmail {MessageId}.",
                    log.ExternalMessageId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<string>> ListNewInboxMessageIdsAsync(
        string accessToken,
        DateTimeOffset connectedAt,
        CancellationToken cancellationToken)
    {
        // The timestamp prevents the first automatic run from classifying historical mail.
        // We intentionally don't filter on UNREAD: a newly-arrived message must not be missed
        // merely because the user opened it before the next n8n polling interval.
        var query = $"after:{connectedAt.ToUnixTimeSeconds()}";
        var url = "https://gmail.googleapis.com/gmail/v1/users/me/messages"
            + $"?labelIds=INBOX&q={Uri.EscapeDataString(query)}&maxResults=500";
        var payload = await GetJsonAsync(accessToken, url, cancellationToken);
        if (!payload.TryGetProperty("messages", out var messages)) return [];
        return messages.EnumerateArray()
            .Select(message => message.GetProperty("id").GetString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();
    }

    private async Task<NormalizedEmailRequest> GetNormalizedEmailAsync(
        string accessToken,
        Guid mailboxConnectionId,
        string messageId,
        CancellationToken cancellationToken)
    {
        var payload = await GetJsonAsync(
            accessToken,
            $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{Uri.EscapeDataString(messageId)}?format=full",
            cancellationToken);
        var messagePayload = payload.GetProperty("payload");
        var headers = messagePayload.GetProperty("headers").EnumerateArray().ToArray();
        string HeaderValue(string name)
        {
            foreach (var header in headers)
            {
                if (header.TryGetProperty("name", out var headerName)
                    && string.Equals(headerName.GetString(), name, StringComparison.OrdinalIgnoreCase)
                    && header.TryGetProperty("value", out var value))
                {
                    return value.GetString() ?? string.Empty;
                }
            }
            return string.Empty;
        }
        var sender = ExtractEmailAddress(HeaderValue("From"));
        var subject = HeaderValue("Subject");
        var body = ExtractBody(messagePayload);
        if (body.Length > MaximumBodyLength) body = body[..MaximumBodyLength];

        return new NormalizedEmailRequest(mailboxConnectionId, messageId, sender, subject, body);
    }

    private async Task<string> EnsureGmailLabelAsync(
        string accessToken,
        Guid labelDefinitionId,
        CancellationToken cancellationToken)
    {
        var label = await dbContext.LabelDefinitions.FirstOrDefaultAsync(
            item => item.Id == labelDefinitionId && item.IsActive,
            cancellationToken) ?? throw new InvalidOperationException("Le label de destination est introuvable ou inactif.");

        var labelsPayload = await GetJsonAsync(
            accessToken,
            "https://gmail.googleapis.com/gmail/v1/users/me/labels",
            cancellationToken);
        var gmailLabels = labelsPayload.GetProperty("labels").EnumerateArray().ToArray();

        // A cached Gmail ID can become stale if the label is renamed or deleted in Gmail.
        // The local label name remains the source of truth.
        var cached = gmailLabels.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(label.ExternalLabelId)
            && string.Equals(item.GetProperty("id").GetString(), label.ExternalLabelId, StringComparison.Ordinal)
            && string.Equals(item.GetProperty("name").GetString(), label.Name, StringComparison.OrdinalIgnoreCase));
        if (cached.ValueKind != JsonValueKind.Undefined) return label.ExternalLabelId!;

        var existing = gmailLabels.FirstOrDefault(
            item => string.Equals(item.GetProperty("name").GetString(), label.Name, StringComparison.OrdinalIgnoreCase));
        var externalLabelId = existing.ValueKind == JsonValueKind.Undefined
            ? await CreateGmailLabelAsync(accessToken, label.Name, cancellationToken)
            : existing.GetProperty("id").GetString();
        label.ExternalLabelId = externalLabelId
            ?? throw new GmailApiException("Google n’a pas retourné l’identifiant du label.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return label.ExternalLabelId;
    }

    private async Task<string?> CreateGmailLabelAsync(
        string accessToken,
        string name,
        CancellationToken cancellationToken)
    {
        var payload = await SendJsonAsync(
            accessToken,
            HttpMethod.Post,
            "https://gmail.googleapis.com/gmail/v1/users/me/labels",
            new { name, labelListVisibility = "labelShow", messageListVisibility = "show" },
            cancellationToken);
        return payload.GetProperty("id").GetString();
    }

    private async Task ApplyLabelAsync(
        string accessToken,
        string messageId,
        string externalLabelId,
        CancellationToken cancellationToken)
    {
        var updatedMessage = await SendJsonAsync(
            accessToken,
            HttpMethod.Post,
            $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{Uri.EscapeDataString(messageId)}/modify",
            new { addLabelIds = new[] { externalLabelId } },
            cancellationToken);

        var applied = updatedMessage.TryGetProperty("labelIds", out var labelIds)
            && labelIds.EnumerateArray().Any(item =>
                string.Equals(item.GetString(), externalLabelId, StringComparison.Ordinal));
        if (!applied)
        {
            throw new GmailApiException("Gmail n’a pas confirmé l’application du label au message.");
        }
    }

    private async Task<JsonElement> GetJsonAsync(
        string accessToken,
        string url,
        CancellationToken cancellationToken) =>
        await SendJsonAsync(accessToken, HttpMethod.Get, url, null, cancellationToken);

    private async Task<JsonElement> SendJsonAsync(
        string accessToken,
        HttpMethod method,
        string url,
        object? body,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("GmailApi");
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new GmailApiException($"Gmail a refusé la requête ({(int)response.StatusCode}).", details);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    private static string ExtractBody(JsonElement part)
    {
        var mimeType = part.TryGetProperty("mimeType", out var mimeElement)
            ? mimeElement.GetString() ?? string.Empty
            : string.Empty;
        if (part.TryGetProperty("body", out var body)
            && body.TryGetProperty("data", out var dataElement)
            && dataElement.GetString() is { Length: > 0 } data
            && (mimeType.Equals("text/plain", StringComparison.OrdinalIgnoreCase)
                || mimeType.Equals("text/html", StringComparison.OrdinalIgnoreCase)))
        {
            var decoded = DecodeBase64Url(data);
            return mimeType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
                ? WebUtility.HtmlDecode(HtmlTagsRegex().Replace(decoded, " "))
                : decoded;
        }
        if (!part.TryGetProperty("parts", out var parts)) return string.Empty;
        var plain = parts.EnumerateArray()
            .Where(child => child.TryGetProperty("mimeType", out var childMime)
                && childMime.GetString() == "text/plain")
            .Select(ExtractBody)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(plain)) return plain;
        return string.Join(" ", parts.EnumerateArray().Select(ExtractBody).Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
    }

    private static string ExtractEmailAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown@invalid.local";
        try { return new MailAddress(value).Address; }
        catch (FormatException)
        {
            var match = EmailRegex().Match(value);
            return match.Success ? match.Value : "unknown@invalid.local";
        }
    }

    private static string SafeError(Exception exception) => exception switch
    {
        GmailConfigurationException => exception.Message,
        GmailApiException => exception.Message,
        _ => "Le traitement du message a échoué. Consultez les logs techniques."
    };

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagsRegex();

    [GeneratedRegex(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex("^[0-9a-f]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GmailMessageIdRegex();
}
