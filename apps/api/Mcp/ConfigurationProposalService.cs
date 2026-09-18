using System.Data;
using MailManager.Api.Contracts;
using MailManager.Api.Data;
using MailManager.Api.Domain;
using MailManager.Api.Security;
using MailManager.Api.Services;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;

namespace MailManager.Api.Mcp;

public sealed class ConfigurationProposalService(MailManagerDbContext db, CurrentUser user,
    ClassificationEngine engine, MailboxProviderResolver providers, ILogger<ConfigurationProposalService> logger)
{
    public async Task<object> ListMailboxesAsync(CancellationToken ct)
    {
        EnsureUser();
        return await db.MailboxConnections.AsNoTracking().Where(x => x.OwnerSubject == user.Subject)
            .OrderBy(x => x.DisplayName).Select(x => new { x.Id, x.DisplayName, Provider = x.Provider.ToString(), x.EmailAddress, x.IsActive })
            .ToListAsync(ct);
    }

    public async Task<ConfigurationState> ReadAsync(Guid mailboxId, CancellationToken ct)
    {
        var mailbox = await OwnedMailboxAsync(mailboxId, ct);
        var destinations = await db.LabelDefinitions.AsNoTracking().Where(x => x.MailboxConnectionId == mailboxId).ToListAsync(ct);
        var rules = await db.ClassificationRules.AsNoTracking().Where(x => x.MailboxConnectionId == mailboxId).ToListAsync(ct);
        return new(mailboxId, mailbox.DisplayName, mailbox.Provider.ToString(), mailbox.IsActive,
            destinations.OrderBy(x => x.Id).Select(x => new DestinationState(x.Id, x.Name, x.Color, x.IsActive)).ToArray(),
            rules.OrderBy(x => x.Id).Select(x => new RuleState(x.Id, x.Name, x.DestinationLabelId, x.Priority, x.IsActive,
                x.MatchMode, x.SenderAddresses, x.SenderDomains, x.SubjectKeywords, x.BodyKeywords, x.CreatedAt, x.UpdatedAt)).ToArray());
    }

    public async Task<ProposalView> PrepareAsync(Guid mailboxId, ConfigurationChanges changes, CancellationToken ct)
    {
        EnsureWriter();
        if (changes is null || changes.Destinations is null || changes.Rules is null
            || changes.Destinations.Length + changes.Rules.Length is < 1 or > 50)
            throw Error("Une proposition doit contenir entre 1 et 50 changements.");
        var before = await ReadAsync(mailboxId, ct);
        var destinations = before.Destinations.ToDictionary(x => x.Id);
        var rules = before.Rules.ToDictionary(x => x.Id);
        var references = destinations.Keys.ToDictionary(x => x.ToString(), x => x, StringComparer.OrdinalIgnoreCase);
        var changedDestinations = new HashSet<Guid>();
        var changedRules = new HashSet<Guid>();
        var now = DatabaseUtcNow();
        foreach (var change in changes.Destinations)
        {
            if (change is null || string.IsNullOrWhiteSpace(change.Key) || change.Key.Length > 100)
                throw Error("Chaque destination doit avoir une clé non vide de 100 caractères maximum.");
            var name = Name(change.Name, 150);
            var id = change.Id ?? Guid.NewGuid();
            if (change.Id.HasValue && !destinations.ContainsKey(id)) throw Error("Destination introuvable dans cette boîte.");
            if (!changedDestinations.Add(id)) throw Error("Une destination ne peut être modifiée deux fois dans la proposition.");
            if (references.TryGetValue(change.Key, out var existingId) && existingId != id)
                throw Error("Clé de destination déjà utilisée.");
            references[change.Key] = id;
            var color = ProviderColorMapper.NormalizeHexColor(change.Color);
            if (!string.IsNullOrWhiteSpace(change.Color) && color is null) throw Error("Couleur attendue : #RRGGBB.");
            destinations[id] = new(id, name, color, change.IsActive);
        }
        if (destinations.Values.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw Error("Une destination de ce nom existe déjà. Réutilisez son identifiant.");
        foreach (var change in changes.Rules)
        {
            if (change is null) throw Error("Règle invalide.");
            var id = change.Id ?? Guid.NewGuid();
            if (change.Id.HasValue && !rules.ContainsKey(id)) throw Error("Règle introuvable dans cette boîte.");
            if (!changedRules.Add(id)) throw Error("Une règle ne peut être modifiée deux fois dans la proposition.");
            if (change.DestinationKey is null || !references.TryGetValue(change.DestinationKey, out var destinationId))
                throw Error("Destination inconnue : utilisez son identifiant existant ou la clé d'une destination proposée.");
            if (change.Priority < 0 || !Enum.IsDefined(change.MatchMode)) throw Error("Priorité ou mode de correspondance invalide.");
            var addresses = Criteria(change.SenderAddresses);
            var domains = Criteria(change.SenderDomains, true);
            var subjects = Criteria(change.SubjectKeywords);
            var bodies = Criteria(change.BodyKeywords);
            if (addresses.Length + domains.Length + subjects.Length + bodies.Length == 0)
                throw Error("Chaque règle doit avoir au moins un critère.");
            rules[id] = new(id, Name(change.Name, 200), destinationId, change.Priority, change.IsActive, change.MatchMode,
                addresses, domains, subjects, bodies, rules.GetValueOrDefault(id)?.CreatedAt ?? now, now);
        }
        var after = before with { Destinations = destinations.Values.OrderBy(x => x.Id).ToArray(), Rules = rules.Values.OrderBy(x => x.Id).ToArray() };
        var warnings = new List<string>();
        if (!before.IsActive) warnings.Add("La boîte est inactive : ces règles ne classeront pas de messages tant qu'elle reste inactive.");
        if (after.Rules.Any(x => x.IsActive && !destinations[x.DestinationId].IsActive))
            warnings.Add("Une règle active cible une destination inactive : elle sera ignorée par le moteur.");
        if (after.Rules.Where(x => x.IsActive).GroupBy(x => x.Priority).Any(x => x.Count() > 1))
            warnings.Add("Des règles actives ont la même priorité. Le moteur départage par date de création puis identifiant.");
        warnings.Add("La première règle correspondante gagne (plus petite priorité). L'activation concerne les prochains traitements ; aucun ancien message n'est retraité par cette action.");
        var proposal = new ConfigurationProposal
        {
            Id = Guid.NewGuid(), MailboxConnectionId = mailboxId, OwnerSubject = user.Subject,
            BeforeJson = McpJson.Serialize(before), AfterJson = McpJson.Serialize(after),
            ChangedDestinationIdsJson = McpJson.Serialize(changedDestinations.Order()),
            ChangedRuleIdsJson = McpJson.Serialize(changedRules.Order()),
            WarningsJson = McpJson.Serialize(warnings), ExpiresAt = now.AddMinutes(30)
        };
        db.ConfigurationProposals.Add(proposal);
        await db.SaveChangesAsync(ct);
        return View(proposal);
    }

    public async Task<ProposalView> GetAsync(Guid proposalId, CancellationToken ct) => View(await OwnedProposalAsync(proposalId, ct));

    public async Task<object> SimulateAsync(Guid mailboxId, Guid? proposalId, string sender, string? subject, string? body, CancellationToken ct)
    {
        var configuration = await ReadAsync(mailboxId, ct);
        if (proposalId.HasValue)
        {
            var proposal = await OwnedProposalAsync(proposalId.Value, ct);
            if (proposal.MailboxConnectionId != mailboxId) throw Error("Cette proposition concerne une autre boîte.");
            if (proposal.AppliedAt is null && proposal.ExpiresAt <= DateTimeOffset.UtcNow) throw Error("Proposition expirée.");
            configuration = McpJson.Deserialize<ConfigurationState>(proposal.AfterJson);
        }
        if (string.IsNullOrWhiteSpace(sender) || sender.Length > 320 || subject?.Length > 10000 || body?.Length > 100000)
            throw Error("Exemple de message invalide ou trop volumineux.");
        var labels = configuration.Destinations.ToDictionary(x => x.Id, x => new LabelDefinition { Id = x.Id, Name = x.Name, IsActive = x.IsActive });
        var evaluation = engine.Evaluate(new NormalizedEmailRequest(mailboxId, "mcp-simulation", sender, subject, body),
            configuration.Rules.Select(x => new ClassificationRule
            {
                Id = x.Id, Name = x.Name, DestinationLabelId = x.DestinationId, DestinationLabel = labels[x.DestinationId],
                IsActive = x.IsActive, Priority = x.Priority, MatchMode = x.MatchMode, CreatedAt = x.CreatedAt,
                SenderAddresses = x.SenderAddresses, SenderDomains = x.SenderDomains, SubjectKeywords = x.SubjectKeywords, BodyKeywords = x.BodyKeywords
            }));
        return new { evaluation.IsClassified, Destination = evaluation.Label?.Name, Rule = evaluation.Rule?.Name,
            evaluation.MatchedCriteria, evaluation.NoMatchReason, MailboxIsActive = configuration.IsActive,
            Note = "Simulation sur l'exemple fourni uniquement ; aucun email lu, modifié ou enregistré." };
    }

    public async Task<ProposalView> ApplyAsync(Guid proposalId, CancellationToken ct)
    {
        EnsureWriter();
        // PostgreSQL serializable transactions + the proposal concurrency token prevent duplicate or partial application.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var proposal = await OwnedProposalAsync(proposalId, ct);
        if (proposal.AppliedAt.HasValue) return View(proposal);
        if (proposal.ExpiresAt <= DateTimeOffset.UtcNow) throw Error("Proposition expirée. Préparez et faites confirmer une nouvelle proposition.");
        var before = await ReadAsync(proposal.MailboxConnectionId, ct);
        if (McpJson.Serialize(before) != proposal.BeforeJson)
            throw Error("La configuration a changé depuis la préparation. Préparez une nouvelle proposition et demandez confirmation.");
        var after = McpJson.Deserialize<ConfigurationState>(proposal.AfterJson);
        var destinationIds = McpJson.Deserialize<Guid[]>(proposal.ChangedDestinationIdsJson);
        var ruleIds = McpJson.Deserialize<Guid[]>(proposal.ChangedRuleIdsJson);
        foreach (var state in after.Destinations.Where(x => destinationIds.Contains(x.Id)))
        {
            var label = await db.LabelDefinitions.FindAsync([state.Id], ct);
            if (label is null)
            {
                label = new LabelDefinition { Id = state.Id, MailboxConnectionId = after.MailboxId, Name = state.Name };
                db.LabelDefinitions.Add(label);
            }
            if (label.Name != state.Name) label.ExternalLabelId = null;
            label.Name = state.Name; label.Color = state.Color; label.IsActive = state.IsActive;
        }
        foreach (var state in after.Rules.Where(x => ruleIds.Contains(x.Id)))
        {
            var rule = await db.ClassificationRules.FindAsync([state.Id], ct);
            if (rule is null)
            {
                rule = new ClassificationRule { Id = state.Id, MailboxConnectionId = after.MailboxId, Name = state.Name, CreatedAt = state.CreatedAt };
                db.ClassificationRules.Add(rule);
            }
            rule.Name = state.Name; rule.DestinationLabelId = state.DestinationId; rule.Priority = state.Priority;
            rule.IsActive = state.IsActive; rule.MatchMode = state.MatchMode; rule.UpdatedAt = state.UpdatedAt;
            rule.SenderAddresses = state.SenderAddresses; rule.SenderDomains = state.SenderDomains;
            rule.SubjectKeywords = state.SubjectKeywords; rule.BodyKeywords = state.BodyKeywords;
        }
        proposal.AppliedAt = DatabaseUtcNow();
        proposal.SynchronizationStatus = "pending";
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { throw Error("Application concurrente. Relisez la proposition pour connaître son résultat."); }
        catch (Exception ex) when (ex is Npgsql.PostgresException { SqlState: "40001" }
            || ex is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "40001" or "23505" } })
        { throw Error("La configuration a changé pendant l'application. Relisez la proposition avant de réessayer."); }
        // External synchronization happens after commit. A failure must never cause recreation of the configuration.
        if (transaction is not null) await transaction.DisposeAsync();
        var mailbox = await OwnedMailboxAsync(after.MailboxId, ct);
        var warnings = McpJson.Deserialize<List<string>>(proposal.WarningsJson);
        var failed = false;
        if (!string.IsNullOrWhiteSpace(mailbox.EncryptedRefreshToken))
        {
            foreach (var id in destinationIds.Where(id => after.Destinations.Any(x => x.Id == id && x.IsActive)))
            {
                try
                {
                    if (!await providers.Resolve(mailbox.Provider).SynchronizeDestinationAsync(id, ct))
                        throw new InvalidOperationException("Destination non synchronisée.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed = true;
                    logger.LogWarning(ex, "MCP: synchronisation impossible pour la destination {DestinationId}", id);
                    warnings.Add($"Configuration enregistrée ; synchronisation impossible pour {id}. Vérifiez la connexion et relancez la synchronisation dans les paramètres MailManager.");
                }
            }
        }
        proposal.SynchronizationStatus = failed ? "failed" : string.IsNullOrWhiteSpace(mailbox.EncryptedRefreshToken) ? "not_connected" : "completed";
        proposal.WarningsJson = McpJson.Serialize(warnings);
        await db.SaveChangesAsync(ct);
        return View(proposal);
    }

    private async Task<MailboxConnection> OwnedMailboxAsync(Guid id, CancellationToken ct)
    {
        EnsureUser();
        return await db.MailboxConnections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubject == user.Subject, ct)
            ?? throw Error("Boîte introuvable.");
    }
    private async Task<ConfigurationProposal> OwnedProposalAsync(Guid id, CancellationToken ct)
    {
        EnsureUser();
        var proposal = await db.ConfigurationProposals.SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubject == user.Subject, ct)
            ?? throw Error("Proposition introuvable.");
        await OwnedMailboxAsync(proposal.MailboxConnectionId, ct);
        return proposal;
    }
    private void EnsureUser() { if (user.IsAutomation) throw Error("Le MCP est réservé aux sessions utilisateur."); }
    private void EnsureWriter() { EnsureUser(); if (user.IsDemo) throw Error("Le profil de démonstration est en lecture seule."); }
    private static string Name(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max) throw Error($"Nom obligatoire, limité à {max} caractères.");
        return value.Trim();
    }
    private static string[] Criteria(string[]? values, bool domain = false)
    {
        if (values?.Length > 100 || values?.Any(x => x is null || x.Length > 500) == true)
            throw Error("Chaque groupe accepte au plus 100 critères de 500 caractères.");
        return RuleValueNormalizer.Values(values, domain);
    }
    private static ProposalView View(ConfigurationProposal p) => new(p.Id,
        p.AppliedAt.HasValue ? "applied" : p.ExpiresAt <= DateTimeOffset.UtcNow ? "expired" : "pending_confirmation",
        p.ExpiresAt, p.AppliedAt, McpJson.Deserialize<ConfigurationState>(p.BeforeJson), McpJson.Deserialize<ConfigurationState>(p.AfterJson),
        McpJson.Deserialize<Guid[]>(p.ChangedDestinationIdsJson), McpJson.Deserialize<Guid[]>(p.ChangedRuleIdsJson),
        McpJson.Deserialize<string[]>(p.WarningsJson), p.SynchronizationStatus);
    private static McpException Error(string message) => new(message);
    private static DateTimeOffset DatabaseUtcNow() => new(DateTimeOffset.UtcNow.Ticks / 10 * 10, TimeSpan.Zero);
}
