using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using comply_flow_api.Models;

namespace comply_flow_api.Services;

public sealed record ResponseContentValidationResult(
    int CheckedCount,
    int PassedCount,
    IReadOnlyList<string> MissingRequirements)
{
    public decimal Coverage => CheckedCount == 0 ? 1m : (decimal)PassedCount / CheckedCount;
    public bool IsComplete => MissingRequirements.Count == 0;
}

public static partial class ResponseContentValidator
{
    public static ResponseValidationRequirements? GetRequirements(Rule? rule)
    {
        if (rule is null)
        {
            return null;
        }

        var validationProperty = rule.Result.AdditionalData
            .FirstOrDefault(item => string.Equals(
                item.Key,
                "responseValidation",
                StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(validationProperty.Key))
        {
            return null;
        }

        if (validationProperty.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"Rule '{rule.Id}' responseValidation must be a JSON object.");
        }

        var requirements = validationProperty.Value.Deserialize<ResponseValidationRequirements>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException(
                $"Rule '{rule.Id}' responseValidation is invalid.");

        if (requirements.RequiredPhrases.Count == 0 && requirements.RequiredFacts.Count == 0 ||
            requirements.RequiredPhrases.Any(string.IsNullOrWhiteSpace) ||
            requirements.RequiredFacts.Any(string.IsNullOrWhiteSpace) ||
            requirements.RequiredPhrases
                .Concat(requirements.RequiredFacts)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != requirements.RequiredPhrases.Count + requirements.RequiredFacts.Count)
        {
            throw new InvalidDataException(
                $"Rule '{rule.Id}' responseValidation must contain non-empty, unique requiredPhrases or requiredFacts.");
        }

        return requirements;
    }

    public static ResponseContentValidationResult Validate(
        string response,
        ResponseValidationRequirements? requirements,
        StructuredCaseState caseState)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(caseState);

        if (requirements is null)
        {
            return new ResponseContentValidationResult(0, 0, []);
        }

        var normalizedResponse = Normalize(response);
        var missing = new List<string>();
        foreach (var phrase in requirements.RequiredPhrases)
        {
            if (!normalizedResponse.Contains(Normalize(phrase), StringComparison.Ordinal))
            {
                missing.Add($"phrase:{phrase}");
            }
        }

        foreach (var field in requirements.RequiredFacts)
        {
            if (!caseState.TryGetValue(field, out var rawValue))
            {
                missing.Add($"fact:{field}");
                continue;
            }

            var values = GetStringValues(rawValue)
                .Select(Normalize)
                .Where(value => value.Length > 0)
                .ToArray();
            if (values.Length == 0 || values.Any(value =>
                    !normalizedResponse.Contains(value, StringComparison.Ordinal)))
            {
                missing.Add($"fact:{field}");
            }
        }

        var total = requirements.RequiredPhrases.Count + requirements.RequiredFacts.Count;
        return new ResponseContentValidationResult(
            total,
            total - missing.Count,
            missing);
    }

    private static IEnumerable<string> GetStringValues(object? value)
    {
        switch (value)
        {
            case string text:
                yield return text;
                break;
            case IEnumerable<string> texts:
                foreach (var item in texts)
                {
                    yield return item;
                }
                break;
            case JsonElement { ValueKind: JsonValueKind.String } element:
                yield return element.GetString()!;
                break;
            case JsonElement { ValueKind: JsonValueKind.Array } array:
                foreach (var item in array.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        yield return item.GetString()!;
                    }
                    else
                    {
                        yield return item.ToString();
                    }
                }
                break;
            case JsonElement element when element.ValueKind != JsonValueKind.Null:
                yield return element.ToString();
                break;
            case IFormattable formattable:
                yield return formattable.ToString(null, CultureInfo.InvariantCulture);
                break;
        }
    }

    private static string Normalize(string value) =>
        Whitespace().Replace(value.Trim(), " ").ToUpperInvariant();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
