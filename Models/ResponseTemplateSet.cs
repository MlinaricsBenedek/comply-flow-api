using System.Text.Json.Serialization;

namespace comply_flow_api.Models;

public sealed class ResponseTemplateSet
{
    [JsonPropertyName("templateVersion")]
    public required string TemplateVersion { get; set; }

    [JsonPropertyName("templates")]
    public required Dictionary<string, string> Templates { get; set; }
}
