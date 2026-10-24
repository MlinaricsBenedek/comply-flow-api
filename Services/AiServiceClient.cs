using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace comply_flow_api.Services;

public interface IAiServiceClient
{
    Task<IReadOnlyList<AiModelOption>> GetModelsAsync(CancellationToken cancellationToken);
    Task<AiPreprocessingResponse> PreprocessAsync(string message, CancellationToken cancellationToken);
    Task<AiGenerationResponse> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken);
}

public sealed class AiServiceClient(
    HttpClient httpClient,
    IOptions<AiServiceOptions> options) : IAiServiceClient
{
    public async Task<IReadOnlyList<AiModelOption>> GetModelsAsync(
        CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeoutTokenSource(
            cancellationToken,
            options.Value.ModelsTimeoutSeconds);
        var response = await httpClient.GetFromJsonAsync<AiModelsResponse>(
            "models",
            timeout.Token);

        if (response is { Success: false })
        {
            throw new InvalidOperationException("The AI service could not return its available models.");
        }

        return response?.Models
            ?? throw new InvalidOperationException("The AI service returned no models response.");
    }

    public async Task<AiPreprocessingResponse> PreprocessAsync(
        string message,
        CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeoutTokenSource(
            cancellationToken,
            options.Value.PreprocessingTimeoutSeconds);
        using var response = await httpClient.PostAsJsonAsync(
            "preprocessing",
            new AiPreprocessingRequest(message),
            timeout.Token);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AiPreprocessingResponse>(
                cancellationToken: timeout.Token)
            ?? throw new InvalidOperationException("The AI service returned no preprocessing response.");
    }

    public async Task<AiGenerationResponse> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeoutTokenSource(
            cancellationToken,
            options.Value.GenerationTimeoutSeconds);
        using var response = await httpClient.PostAsJsonAsync(
            "generate",
            request,
            timeout.Token);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AiGenerationResponse>(
                cancellationToken: timeout.Token)
            ?? throw new InvalidOperationException("The AI service returned no generation response.");
    }

    private static CancellationTokenSource CreateTimeoutTokenSource(
        CancellationToken cancellationToken,
        int timeoutSeconds)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return timeout;
    }
}

public sealed record AiModelOption(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("is_default")] bool IsDefault);

public sealed record AiModelsResponse(
    [property: JsonPropertyName("models")] IReadOnlyList<AiModelOption> Models,
    [property: JsonPropertyName("success")] bool Success = true);

public sealed record AiPreprocessingRequest(
    [property: JsonPropertyName("message")] string Message);

public sealed record AiPreprocessingResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("facts")] Dictionary<string, List<string>> Facts,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("success")] bool Success = true);

public sealed record AiGenerationRequest(
    [property: JsonPropertyName("input_data")] Dictionary<string, object?> InputData,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("facts")] Dictionary<string, List<string>> Facts,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("prompt_name")] string? PromptName,
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("model_parameters")] Dictionary<string, JsonElement>? ModelParameters);

public sealed record AiGenerationResponse(
    [property: JsonPropertyName("response")] string Response,
    [property: JsonPropertyName("prompt_version")] string PromptVersion,
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("model_id")] string ModelId,
    [property: JsonPropertyName("model_parameters")] Dictionary<string, JsonElement> ModelParameters,
    [property: JsonPropertyName("success")] bool Success = true);