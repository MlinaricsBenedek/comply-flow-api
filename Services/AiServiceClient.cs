using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using comply_flow_api.Models;
using Microsoft.Extensions.Options;

namespace comply_flow_api.Services;

public interface IAiServiceClient
{
    Task<IReadOnlyList<AiModelOption>> GetModelsAsync(CancellationToken cancellationToken);
    Task<AiPreprocessingResult> PreprocessAsync(string message, CancellationToken cancellationToken);
    Task<AiGenerationResult> GenerateAsync(
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
        using var httpResponse = await httpClient.GetAsync("models", timeout.Token);
        httpResponse.EnsureSuccessStatusCode();
        var response = await ReadResponseAsync<AiModelsApiResponse>(
            httpResponse.Content,
            "models",
            timeout.Token);

        if (response is null)
        {
            throw new InvalidDataException("The AI service returned no models response.");
        }

        if (!response.Success)
        {
            throw CreateServiceError("models", response.Error);
        }

        if (response.Models is not { Count: > 0 } models ||
            models.Any(model => string.IsNullOrWhiteSpace(model.Name)) ||
            models.Select(model => model.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != models.Count ||
            models.Count(model => model.IsDefault) > 1)
        {
            throw new InvalidDataException("The AI service returned an invalid models list.");
        }

        return models;
    }

    public async Task<AiPreprocessingResult> PreprocessAsync(
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

        var payload = await ReadResponseAsync<AiPreprocessingApiResponse>(
            response.Content,
            "preprocessing",
            timeout.Token);
        if (payload is null)
        {
            throw new InvalidDataException("The AI service returned no preprocessing response.");
        }

        if (!payload.Success)
        {
            throw CreateServiceError("preprocessing", payload.Error);
        }

        if (string.IsNullOrWhiteSpace(payload.Message) ||
            payload.Facts is null ||
            payload.Category is not null && string.IsNullOrWhiteSpace(payload.Category) ||
            payload.Facts.Any(fact =>
                string.IsNullOrWhiteSpace(fact.Key) ||
                fact.Value is null ||
                fact.Value.Any(string.IsNullOrWhiteSpace)))
        {
            throw new InvalidDataException("The AI service returned an invalid preprocessing response.");
        }

        var warnings = payload.Warnings ?? [];
        if (warnings.Any(string.IsNullOrWhiteSpace) || payload.PartialSuccess && warnings.Count == 0)
        {
            throw new InvalidDataException("The AI service returned invalid preprocessing warnings.");
        }

        return new AiPreprocessingResult(
            payload.Message,
            payload.Facts,
            payload.Category,
            MapCaseState(payload),
            payload.PartialSuccess,
            warnings);
    }

    public async Task<AiGenerationResult> GenerateAsync(
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

        var payload = await ReadResponseAsync<AiGenerationApiResponse>(
            response.Content,
            "generate",
            timeout.Token);
        if (payload is null)
        {
            throw new InvalidDataException("The AI service returned no generation response.");
        }

        if (!payload.Success)
        {
            throw CreateServiceError("generate", payload.Error);
        }

        if (string.IsNullOrWhiteSpace(payload.Response) ||
            string.IsNullOrWhiteSpace(payload.PromptVersion) ||
            string.IsNullOrWhiteSpace(payload.ModelName) ||
            string.IsNullOrWhiteSpace(payload.ModelId) ||
            payload.ModelParameters is null)
        {
            throw new InvalidDataException("The AI service returned an invalid generation response.");
        }

        return new AiGenerationResult(
            payload.Response,
            payload.PromptVersion,
            payload.ModelName,
            payload.ModelId,
            payload.ModelParameters);
    }

    private static async Task<T?> ReadResponseAsync<T>(
        HttpContent content,
        string endpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            return await content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The AI service returned invalid JSON from '{endpoint}'.",
                exception);
        }
    }

    private static InvalidOperationException CreateServiceError(string endpoint, AiServiceError? error)
    {
        var details = error is null
            ? "The service reported an unsuccessful result."
            : $"{error.Code}: {error.Message} (correlation ID: {error.CorrelationId ?? "unknown"})";

        return new InvalidOperationException($"The AI service '{endpoint}' failed. {details}");
    }

    private static StructuredCaseState MapCaseState(AiPreprocessingApiResponse response)
    {
        var caseState = new StructuredCaseState
        {
            ["category"] = response.Category
        };

        foreach (var (name, values) in response.Facts!)
        {
            caseState[name] = values.Count == 1 ? values[0] : values;
        }

        return caseState;
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

public sealed record AiModelsApiResponse(
    [property: JsonPropertyName("models")] List<AiModelOption>? Models,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] AiServiceError? Error = null);

public sealed record AiPreprocessingRequest(
    [property: JsonPropertyName("message")] string Message);

public sealed record AiPreprocessingApiResponse(
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("facts")] Dictionary<string, List<string>>? Facts,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("partial_success")] bool PartialSuccess = false,
    [property: JsonPropertyName("warnings")] List<string>? Warnings = null,
    [property: JsonPropertyName("error")] AiServiceError? Error = null);

public sealed record AiPreprocessingResult(
    string Message,
    Dictionary<string, List<string>> Facts,
    string? Category,
    StructuredCaseState CaseState,
    bool PartialSuccess,
    IReadOnlyList<string> Warnings);

public sealed record AiGenerationRequest(
    [property: JsonPropertyName("input_data")] Dictionary<string, object?> InputData,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("facts")] Dictionary<string, List<string>> Facts,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("prompt_name")] string? PromptName,
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("model_parameters")] Dictionary<string, JsonElement>? ModelParameters);

public sealed record AiGenerationApiResponse(
    [property: JsonPropertyName("response")] string? Response,
    [property: JsonPropertyName("prompt_version")] string? PromptVersion,
    [property: JsonPropertyName("model_name")] string? ModelName,
    [property: JsonPropertyName("model_id")] string? ModelId,
    [property: JsonPropertyName("model_parameters")] Dictionary<string, JsonElement>? ModelParameters,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] AiServiceError? Error = null);

public sealed record AiGenerationResult(
    string Response,
    string PromptVersion,
    string ModelName,
    string ModelId,
    Dictionary<string, JsonElement> ModelParameters);

public sealed record AiServiceError(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("correlation_id")] string? CorrelationId);