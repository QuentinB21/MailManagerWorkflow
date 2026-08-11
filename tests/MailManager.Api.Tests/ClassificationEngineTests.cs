using MailManager.Api.Contracts;
using MailManager.Api.Domain;
using MailManager.Api.Services;

namespace MailManager.Api.Tests;

public sealed class ClassificationEngineTests
{
    private readonly ClassificationEngine _engine = new();

    [Fact]
    public void Matches_sender_address_case_insensitively()
    {
        var rule = Rule(senderAddresses: ["  Alice@Client.FR "]);

        var result = _engine.Evaluate(Email(sender: "alice@client.fr"), [rule]);

        Assert.True(result.IsClassified);
        Assert.Contains("adresse expéditeur: alice@client.fr", result.MatchedCriteria);
    }

    [Fact]
    public void Matches_sender_domain_including_subdomains()
    {
        var rule = Rule(senderDomains: ["client.fr"]);

        var result = _engine.Evaluate(Email(sender: "bob@eu.client.fr"), [rule]);

        Assert.True(result.IsClassified);
        Assert.Contains("domaine expéditeur: client.fr", result.MatchedCriteria);
    }

    [Fact]
    public void Matches_subject_keyword_after_whitespace_normalization()
    {
        var rule = Rule(subjectKeywords: ["Projet   Alpha"]);

        var result = _engine.Evaluate(Email(subject: "Re: PROJET ALPHA — planning"), [rule]);

        Assert.True(result.IsClassified);
        Assert.Contains("mot-clé sujet: projet alpha", result.MatchedCriteria);
    }

    [Fact]
    public void Matches_body_keyword()
    {
        var rule = Rule(bodyKeywords: ["bon de commande"]);

        var result = _engine.Evaluate(Email(body: "Veuillez trouver le BON DE COMMANDE en pièce jointe."), [rule]);

        Assert.True(result.IsClassified);
        Assert.Contains("mot-clé corps: bon de commande", result.MatchedCriteria);
    }

    [Fact]
    public void Lower_priority_number_wins_when_two_rules_match()
    {
        var later = Rule(name: "Priorité 20", priority: 20, subjectKeywords: ["urgent"]);
        var winner = Rule(name: "Priorité 5", priority: 5, subjectKeywords: ["urgent"]);

        var result = _engine.Evaluate(Email(subject: "Demande urgente"), [later, winner]);

        Assert.Equal(winner.Id, result.Rule?.Id);
    }

    [Fact]
    public void Disabled_rule_is_ignored()
    {
        var rule = Rule(isActive: false, senderDomains: ["client.fr"]);

        var result = _engine.Evaluate(Email(sender: "alice@client.fr"), [rule]);

        Assert.False(result.IsClassified);
        Assert.Null(result.Rule);
    }

    [Fact]
    public void Returns_explanation_when_no_rule_matches()
    {
        var rule = Rule(subjectKeywords: ["facture"]);

        var result = _engine.Evaluate(Email(subject: "Compte-rendu"), [rule]);

        Assert.False(result.IsClassified);
        Assert.Equal("Aucune règle active ne correspond aux critères de cet email.", result.NoMatchReason);
    }

    [Fact]
    public void All_mode_requires_each_configured_group_to_match()
    {
        var rule = Rule(
            matchMode: MatchMode.All,
            senderDomains: ["client.fr"],
            subjectKeywords: ["alpha"]);

        var result = _engine.Evaluate(Email(sender: "alice@client.fr", subject: "Projet beta"), [rule]);

        Assert.False(result.IsClassified);
    }

    private static NormalizedEmailRequest Email(
        string sender = "sender@example.test",
        string subject = "",
        string body = "") =>
        new(Guid.NewGuid(), Guid.NewGuid().ToString(), sender, subject, body);

    private static ClassificationRule Rule(
        string name = "Règle test",
        int priority = 10,
        bool isActive = true,
        MatchMode matchMode = MatchMode.Any,
        string[]? senderAddresses = null,
        string[]? senderDomains = null,
        string[]? subjectKeywords = null,
        string[]? bodyKeywords = null)
    {
        var label = new LabelDefinition
        {
            Id = Guid.NewGuid(),
            MailboxConnectionId = Guid.NewGuid(),
            Name = "Label test",
            IsActive = true
        };
        return new ClassificationRule
        {
            Id = Guid.NewGuid(),
            MailboxConnectionId = label.MailboxConnectionId,
            DestinationLabelId = label.Id,
            DestinationLabel = label,
            Name = name,
            Priority = priority,
            IsActive = isActive,
            MatchMode = matchMode,
            SenderAddresses = senderAddresses ?? [],
            SenderDomains = senderDomains ?? [],
            SubjectKeywords = subjectKeywords ?? [],
            BodyKeywords = bodyKeywords ?? []
        };
    }
}
