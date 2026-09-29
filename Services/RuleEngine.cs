using System.Collections;
using System.Text.Json;
using comply_flow_api.Models;

namespace comply_flow_api.Services;

public static class RuleEngine
{
    public static bool EvaluateCondition(Condition condition, StructuredCaseState caseState)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(caseState);

        var actual = GetFieldValue(caseState, condition.Field);
        var expected = NormalizeJsonValue(condition.Value);

        return condition.Operator.Trim().ToLowerInvariant() switch
        {
            "equals" => EqualsValue(actual, expected),
            "notequals" => NotEqualsValue(actual, expected),
            "in" => InValues(actual, GetExpectedValues(condition.Value)),
            "notin" => NotInValues(actual, GetExpectedValues(condition.Value)),
            "greaterthan" => GreaterThan(actual, expected),
            "greaterthanorequal" => GreaterThanOrEqual(actual, expected),
            "lessthan" => LessThan(actual, expected),
            "lessthanorequal" => LessThanOrEqual(actual, expected),
            _ => false
        };
    }

    public static bool EqualsValue(object? actual, object? expected)
    {
        actual = NormalizeJsonValue(actual);
        expected = NormalizeJsonValue(expected);

        if (actual is null || expected is null)
        {
            return actual is null && expected is null;
        }

        if (TryCompareNumbers(actual, expected, out var comparison))
        {
            return comparison == 0;
        }

        return actual.Equals(expected);
    }

    public static bool NotEqualsValue(object? actual, object? expected) =>
        !EqualsValue(actual, expected);

    public static bool InValues(object? actual, IEnumerable<object?> expected) =>
        expected.Any(value => EqualsValue(actual, value));

    public static bool NotInValues(object? actual, IEnumerable<object?> expected) =>
        !InValues(actual, expected);

    public static bool GreaterThan(object? actual, object? expected) =>
        TryCompareNumbers(actual, expected, out var comparison) && comparison > 0;

    public static bool GreaterThanOrEqual(object? actual, object? expected) =>
        TryCompareNumbers(actual, expected, out var comparison) && comparison >= 0;

    public static bool LessThan(object? actual, object? expected) =>
        TryCompareNumbers(actual, expected, out var comparison) && comparison < 0;

    public static bool LessThanOrEqual(object? actual, object? expected) =>
        TryCompareNumbers(actual, expected, out var comparison) && comparison <= 0;

    public static object? GetFieldValue(StructuredCaseState caseState, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(caseState);

        return caseState.TryGetValue(fieldName, out var value)
            ? NormalizeJsonValue(value)
            : null;
    }

    public static bool EvaluateAll(IEnumerable<Condition> conditions, StructuredCaseState caseState) =>
        conditions.All(condition => EvaluateCondition(condition, caseState));

    public static bool EvaluateAny(IEnumerable<Condition> conditions, StructuredCaseState caseState) =>
        conditions.Any(condition => EvaluateCondition(condition, caseState));

    public static bool EvaluateConditionGroup(ConditionGroup group, StructuredCaseState caseState)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (group.All is not null && group.Any is not null)
        {
            return EvaluateAll(group.All, caseState) && EvaluateAny(group.Any, caseState);
        }

        if (group.All is not null)
        {
            return EvaluateAll(group.All, caseState);
        }

        return group.Any is null || EvaluateAny(group.Any, caseState);
    }

    public static bool EvaluateRule(Rule rule, StructuredCaseState caseState)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return rule.Conditions is null || EvaluateConditionGroup(rule.Conditions, caseState);
    }

    public static List<RuleEvaluationResult> EvaluateRules(
        IEnumerable<Rule> rules,
        StructuredCaseState caseState) =>
        rules.Select(rule => new RuleEvaluationResult(rule, EvaluateRule(rule, caseState))).ToList();

    public static List<RuleEvaluationResult> GetMatchedRules(IEnumerable<RuleEvaluationResult> results) =>
        results.Where(result => result.IsMatched).ToList();

    public static RuleEvaluationResult? SelectRule(IEnumerable<RuleEvaluationResult> matchedRules) =>
        matchedRules
            .Where(result => result.IsMatched)
            .OrderByDescending(result => result.Rule.Priority)
            .FirstOrDefault();

    public static RuleEngineResult Evaluate(
        StructuredCaseState caseState,
        IEnumerable<Rule> rules)
    {
        var evaluations = EvaluateRules(rules, caseState);
        var matchedRules = GetMatchedRules(evaluations);
        var selectedRule = SelectRule(matchedRules);

        return new RuleEngineResult(evaluations, matchedRules, selectedRule);
    }

    private static IEnumerable<object?> GetExpectedValues(object? value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            return jsonArray.EnumerateArray().Select(item => NormalizeJsonValue(item)).ToArray();
        }

        if (value is IEnumerable values and not string)
        {
            return values.Cast<object?>().Select(NormalizeJsonValue).ToArray();
        }

        return [];
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement jsonValue)
        {
            return value;
        }

        return jsonValue.ValueKind switch
        {
            JsonValueKind.String => jsonValue.GetString(),
            JsonValueKind.Number when jsonValue.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when jsonValue.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Number => jsonValue.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => jsonValue
        };
    }

    private static bool TryCompareNumbers(object? actual, object? expected, out int comparison)
    {
        actual = NormalizeJsonValue(actual);
        expected = NormalizeJsonValue(expected);
        comparison = 0;

        if (actual is null || expected is null || !IsNumeric(actual) || !IsNumeric(expected))
        {
            return false;
        }

        if (TryGetDecimal(actual, out var actualDecimal) && TryGetDecimal(expected, out var expectedDecimal))
        {
            comparison = actualDecimal.CompareTo(expectedDecimal);
            return true;
        }

        var actualDouble = Convert.ToDouble(actual, System.Globalization.CultureInfo.InvariantCulture);
        var expectedDouble = Convert.ToDouble(expected, System.Globalization.CultureInfo.InvariantCulture);
        comparison = actualDouble.CompareTo(expectedDouble);
        return true;
    }

    private static bool IsNumeric(object value) => value is
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static bool TryGetDecimal(object value, out decimal result)
    {
        try
        {
            result = Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
    }
}