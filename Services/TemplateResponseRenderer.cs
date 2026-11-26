using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using comply_flow_api.Models;

namespace comply_flow_api.Services;

public static class TemplateResponseRenderer
{
    public static string Render(
        ResponseTemplateSet templateSet,
        string decision,
        string reason,
        string assessment,
        string requiredElementsJson,
        string responseStructureJson,
        string sourcesJson,
        string ruleId,
        string ruleName,
        string customerDecision,
        string customerReason,
        TransferFeeComplaintAssessment? transferAssessment,
        string transferFeeAssessmentJson)
    {
        ArgumentNullException.ThrowIfNull(templateSet);

        if (templateSet.Templates is null || templateSet.Templates.Count == 0)
        {
            throw new InvalidDataException(
                $"Template version '{templateSet.TemplateVersion}' does not define any templates.");
        }

        if (templateSet.Templates.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)) ||
            templateSet.Templates.Keys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != templateSet.Templates.Count)
        {
            throw new InvalidDataException(
                $"Template version '{templateSet.TemplateVersion}' contains an invalid or duplicate decision template.");
        }

        var template = templateSet.Templates
            .FirstOrDefault(item => string.Equals(item.Key, decision, StringComparison.OrdinalIgnoreCase))
            .Value;
        template ??= templateSet.Templates
            .FirstOrDefault(item => string.Equals(item.Key, "default", StringComparison.OrdinalIgnoreCase))
            .Value;
        if (template is null)
        {
            throw new InvalidDataException(
                $"Template version '{templateSet.TemplateVersion}' has no template for decision '{decision}' or a default template.");
        }

        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{decision}}"] = decision,
            ["{{reason}}"] = reason,
            ["{{assessment}}"] = assessment,
            ["{{appliedRule}}"] = FormatAppliedRule(ruleId, ruleName),
            ["{{customerDecision}}"] = customerDecision,
            ["{{customerReason}}"] = customerReason,
            ["{{transactionSummary}}"] = FormatTransactionSummary(transferAssessment),
            ["{{transferFeeTerms}}"] = template.Contains("{{transferFeeTerms}}", StringComparison.Ordinal)
                ? FormatTransferFeeTerms(transferFeeAssessmentJson)
                : string.Empty,
            ["{{requiredElements}}"] = FormatStringArray(requiredElementsJson),
            ["{{responseStructure}}"] = FormatJsonObject(responseStructureJson),
            ["{{sources}}"] = FormatSources(sourcesJson)
        };

        var response = template;
        foreach (var (placeholder, value) in replacements)
        {
            response = response.Replace(placeholder, value, StringComparison.Ordinal);
        }

        if (response.Contains("{{", StringComparison.Ordinal) ||
            response.Contains("}}", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The selected template contains an unsupported or unresolved placeholder.");
        }

        response = response.Trim();
        if (string.Equals(templateSet.TemplateVersion, "template-v4", StringComparison.Ordinal))
        {
            response = Regex.Replace(response, @"(\r?\n){3,}", "\n\n");
        }
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidDataException("The selected template produced an empty response.");
        }

        return response;
    }

    private static string FormatAppliedRule(string ruleId, string ruleName) =>
        string.IsNullOrWhiteSpace(ruleId) && string.IsNullOrWhiteSpace(ruleName)
            ? string.Empty
            : $"A megadott adatok alapján a rendszer a **{ruleId} ({ruleName})** szabályt alkalmazta.";

    private static string FormatTransactionSummary(TransferFeeComplaintAssessment? assessment)
    {
        if (assessment is null || assessment.Status == TransferFeeAssessmentStatus.InsufficientData)
        {
            return string.Empty;
        }

        var culture = CultureInfo.GetCultureInfo("hu-HU");
        var details = new List<string>();
        if (assessment.TransferAmountHuf is decimal transferAmount)
        {
            details.Add($"az átutalás összege {FormatHuf(transferAmount, culture)} Ft");
        }

        if (assessment.ChargedFeeHuf is decimal chargedFee)
        {
            details.Add($"a közölt díj {FormatHuf(chargedFee, culture)} Ft");
        }

        return details.Count == 0
            ? string.Empty
            : $"Az Ön által közölt adatok szerint {string.Join(", ", details)}.";
    }

    private static string FormatTransferFeeTerms(string json)
    {
        using var document = JsonDocument.Parse(json);
        var tariff = document.RootElement;
        if (tariff.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A transfer fee assessment must be a JSON object.");
        }

        if (!tariff.EnumerateObject().MoveNext())
        {
            return string.Empty;
        }

        if (!tariff.TryGetProperty("ratePercent", out var rate) ||
            !rate.TryGetDecimal(out var ratePercent) ||
            !tariff.TryGetProperty("minimumFeeHuf", out var minimum) ||
            !minimum.TryGetDecimal(out var minimumFeeHuf) ||
            !tariff.TryGetProperty("maximumFeeHuf", out var maximum) ||
            !maximum.TryGetDecimal(out var maximumFeeHuf) ||
            ratePercent <= 0 ||
            minimumFeeHuf < 0 ||
            maximumFeeHuf < minimumFeeHuf)
        {
            throw new InvalidDataException("A transfer fee assessment contains an invalid tariff.");
        }

        var culture = CultureInfo.GetCultureInfo("hu-HU");
        var terms = $"A szabályban szereplő díjtétel az átutalt összeg {ratePercent.ToString("0.##", culture)}%-a, " +
            $"minimum {FormatHuf(minimumFeeHuf, culture)} Ft, " +
            $"maximum {FormatHuf(maximumFeeHuf, culture)} Ft.";
        if (tariff.TryGetProperty("effectiveFrom", out var effectiveFrom))
        {
            if (effectiveFrom.ValueKind != JsonValueKind.String ||
                !DateOnly.TryParseExact(
                    effectiveFrom.GetString(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var effectiveDate))
            {
                throw new InvalidDataException("A transfer fee assessment contains an invalid effective date.");
            }

            terms += $" Ez a díjtétel {effectiveDate.ToString("yyyy.MM.dd.", culture)} napjától hatályos.";
        }

        return terms;
    }

    private static string FormatHuf(decimal amount, CultureInfo culture) =>
        amount.ToString("#,##0.##", culture)
            .Replace('\u00a0', ' ')
            .Replace('\u202f', ' ');

    private static string FormatStringArray(string json)
    {
        var values = JsonSerializer.Deserialize<List<string>>(json)
            ?? throw new InvalidDataException("A response template list must be a JSON string array.");
        return values.Count == 0
            ? "Nincs megadott elem."
            : string.Join(Environment.NewLine, values.Select(value => $"- {value}"));
    }

    private static string FormatSources(string json)
    {
        var sources = JsonSerializer.Deserialize<List<string>>(json)
            ?? throw new InvalidDataException("A response template sources must be a JSON string array.");
        return sources.Count == 0
            ? "A rendelkezésre álló szabályok nem tartalmaznak ehhez a válaszhoz ellenőrzött forrást."
            : string.Join(Environment.NewLine, sources
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(value => $"- {value}"));
    }

    private static string FormatJsonObject(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The response structure must be a JSON object.");
        }

        var lines = new List<string>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (IsInternalProperty(property.Name))
            {
                continue;
            }

            AddJsonLines(property.Value, property.Name, lines);
        }

        return lines.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, lines.Select(line => $"- {line}"));
    }

    private static void AddJsonLines(JsonElement value, string path, ICollection<string> lines)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (IsInternalProperty(property.Name))
                    {
                        continue;
                    }

                    AddJsonLines(property.Value, $"{path} - {property.Name}", lines);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    AddJsonLines(item, path, lines);
                }
                break;
            case JsonValueKind.String:
                lines.Add($"{path}: {value.GetString()}");
                break;
            case JsonValueKind.Null:
                lines.Add($"{path}: nincs adat");
                break;
            default:
                lines.Add($"{path}: {value}");
                break;
        }
    }

    private static bool IsInternalProperty(string name) =>
        string.Equals(name, "documents", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "responseInstructions", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "transferFeeAssessment", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "complaintAssessment", StringComparison.OrdinalIgnoreCase);
}
