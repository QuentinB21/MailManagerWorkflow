using MailManager.Api.Contracts;
using MailManager.Api.Domain;

namespace MailManager.Api.Services;

public sealed record ClassificationEvaluation(
    bool IsClassified,
    LabelDefinition? Label,
    ClassificationRule? Rule,
    IReadOnlyCollection<string> MatchedCriteria,
    string? NoMatchReason);

public sealed class ClassificationEngine
{
    public ClassificationEvaluation Evaluate(
        NormalizedEmailRequest email,
        IEnumerable<ClassificationRule> rules)
    {
        var activeRules = rules
            .Where(x => x.IsActive && x.DestinationLabel?.IsActive != false)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id);

        foreach (var rule in activeRules)
        {
            var evaluation = EvaluateRule(email, rule);
            if (evaluation.IsMatch)
            {
                return new ClassificationEvaluation(
                    true,
                    rule.DestinationLabel,
                    rule,
                    evaluation.Criteria,
                    null);
            }
        }

        return new ClassificationEvaluation(
            false,
            null,
            null,
            [],
            "Aucune règle active ne correspond aux critères de cet email.");
    }

    private static RuleEvaluation EvaluateRule(NormalizedEmailRequest email, ClassificationRule rule)
    {
        var sender = RuleValueNormalizer.Text(email.Sender);
        var senderDomain = sender.Contains('@') ? sender[(sender.LastIndexOf('@') + 1)..] : string.Empty;
        var subject = RuleValueNormalizer.Text(email.Subject);
        var body = RuleValueNormalizer.Text(email.Body);

        var groups = new List<CriteriaGroup>();
        AddGroup(groups, "adresse expéditeur", rule.SenderAddresses,
            value => sender.Equals(value, StringComparison.OrdinalIgnoreCase));
        AddGroup(groups, "domaine expéditeur", rule.SenderDomains,
            value => senderDomain.Equals(value, StringComparison.OrdinalIgnoreCase)
                || senderDomain.EndsWith($".{value}", StringComparison.OrdinalIgnoreCase),
            domain: true);
        AddGroup(groups, "mot-clé sujet", rule.SubjectKeywords,
            value => subject.Contains(value, StringComparison.OrdinalIgnoreCase));
        AddGroup(groups, "mot-clé corps", rule.BodyKeywords,
            value => body.Contains(value, StringComparison.OrdinalIgnoreCase));

        if (groups.Count == 0)
        {
            return new RuleEvaluation(false, []);
        }

        var isMatch = rule.MatchMode == MatchMode.All
            ? groups.All(x => x.HasMatch)
            : groups.Any(x => x.HasMatch);

        var criteria = groups
            .SelectMany(x => x.MatchedValues.Select(value => $"{x.Name}: {value}"))
            .ToArray();

        return new RuleEvaluation(isMatch, criteria);
    }

    private static void AddGroup(
        ICollection<CriteriaGroup> groups,
        string name,
        IEnumerable<string>? configuredValues,
        Func<string, bool> predicate,
        bool domain = false)
    {
        var values = RuleValueNormalizer.Values(configuredValues, domain);
        if (values.Length == 0)
        {
            return;
        }

        groups.Add(new CriteriaGroup(name, values.Where(predicate).ToArray()));
    }

    private sealed record CriteriaGroup(string Name, IReadOnlyCollection<string> MatchedValues)
    {
        public bool HasMatch => MatchedValues.Count > 0;
    }

    private sealed record RuleEvaluation(bool IsMatch, IReadOnlyCollection<string> Criteria);
}
