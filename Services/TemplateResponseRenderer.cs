using System.Text.Json;
using comply_flow_api.Models;

namespace comply_flow_api.Services;

public static class TemplateResponseRenderer
{
    private static readonly string[] RequiredPlaceholders =
    [
        "{{decision}}",
        "{{reason}}",
        "{{sources}}"
    ];

    public static string Render(
        ResponseTemplateSet templateSet,
        string decision,
        string reason,
        string action,
        string nextStep,
        string requiredElementsJson,
        string responseStructureJson,
        string sourcesJson)
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

        if (RequiredPlaceholders.Any(placeholder =>
            !template.Contains(placeholder, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"The selected template must include all required placeholders: {string.Join(", ", RequiredPlaceholders)}.");
        }

        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{decision}}"] = decision,
            ["{{reason}}"] = reason,
            ["{{action}}"] = action,
            ["{{nextStep}}"] = nextStep,
            ["{{requiredElements}}"] = FormatStringArray(requiredElementsJson),
            ["{{responseStructure}}"] = FormatJsonObject(responseStructureJson),
            ["{{sources}}"] = FormatStringArray(sourcesJson)
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
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidDataException("The selected template produced an empty response.");
        }

        return response;
    }

    private static string FormatStringArray(string json)
    {
        var values = JsonSerializer.Deserialize<List<string>>(json)
            ?? throw new InvalidDataException("A response template list must be a JSON string array.");
        return values.Count == 0
            ? "Nincs megadott elem."
            : string.Join(Environment.NewLine, values.Select(value => $"- {value}"));
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
        string.Equals(name, "responseInstructions", StringComparison.OrdinalIgnoreCase);
}
