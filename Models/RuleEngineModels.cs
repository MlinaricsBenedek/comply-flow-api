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
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public object? Value { get; set; }
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
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("conditions")]
    public ConditionGroup Conditions { get; set; } = new();
}

public sealed record RuleEvaluationResult(Rule Rule, bool IsMatched);

public sealed record RuleEngineResult(
    IReadOnlyList<RuleEvaluationResult> Evaluations,
    IReadOnlyList<RuleEvaluationResult> MatchedRules,
    RuleEvaluationResult? SelectedRule);