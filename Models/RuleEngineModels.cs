using System.Text.Json.Serialization;

namespace comply_flow_api.Models;

public sealed class StructuredCaseState : Dictionary<string, object?>
{
    public StructuredCaseState() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    public StructuredCaseState(IEnumerable<KeyValuePair<string, object?>> values)
        : base(values, StringComparer.OrdinalIgnoreCase)
    {
    }
}

public sealed class Condition
{
    [JsonPropertyName("field")]
    public required string Field { get; set; }

    [JsonPropertyName("operator")]
    public required string Operator { get; set; }

    [JsonPropertyName("value")]
    public required object? Value { get; set; }
}

public sealed class ConditionGroup
{
    [JsonPropertyName("all")]
    public List<Condition>? All { get; set; }

    [JsonPropertyName("any")]
    public List<Condition>? Any { get; set; }
}

public sealed class Rule
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("prioScore")]
    public int PrioScore { get; set; } = 0;

    [JsonPropertyName("conditions")]
    public required ConditionGroup? Conditions { get; set; }

    [JsonPropertyName("result")]
    public required RuleOutcome Result { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("effectiveFrom")]
    public string? EffectiveFrom { get; set; }
}

public sealed class RuleSetDocument
{
    [JsonPropertyName("ruleSetVersion")]
    public required string RuleSetVersion { get; set; }

    [JsonPropertyName("rules")]
    public required List<Rule> Rules { get; set; }
}

public sealed class RuleOutcome
{
    [JsonPropertyName("decision")]
    public required string Decision { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement> AdditionalData { get; set; } = [];
}

public sealed record RuleEvaluationResult(Rule Rule, bool IsMatched);

public sealed record RuleEngineResult(
    IReadOnlyList<RuleEvaluationResult> Evaluations,
    IReadOnlyList<RuleEvaluationResult> MatchedRules,
    RuleEvaluationResult? SelectedRule);