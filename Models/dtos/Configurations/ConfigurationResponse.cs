using System.Text.Json;
using System.Text.Json.Serialization;

namespace comply_flow_api.Models.dtos.Configurations;

public sealed record ConfigurationSummaryResponse(int Id, string Name);

public sealed record ConfigurationResponse(
    int Id,
    string Name,
    string Bank,
    string GenerationMode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TemplateVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PromptVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ModelName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Dictionary<string, JsonElement>? ModelParameters,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RetrievalTopK = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? RetrievalMinSimilarity = null);