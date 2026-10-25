using System.Net;
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
    private static readonly JsonSerializerOptions ResponseJsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AiModelOption>> GetModelsAsync(
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<AiModelsApiResponse>(
            token => httpClient.GetAsync("models", token),
            "models",
            options.Value.ModelsTimeoutSeconds,
            cancellationToken);

        if (response is null)
        {
            throw InvalidResponse("models", "The response body is empty.");
        }

        if (!response.Success)
        {
            throw CreateServiceError("models", response.Error);
        }

        if (response.Models is not { Count: > 0 } models ||
            models.Any(model => model is null || string.IsNullOrWhiteSpace(model.Name)) ||
            models.Select(model => model.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != models.Count ||
            models.Count(model => model.IsDefault) > 1)
        {
            throw InvalidResponse("models", "The response contains an invalid models list.");
        }

        return models;
    }

    public async Task<AiPreprocessingResult> PreprocessAsync(
        string message,
        CancellationToken cancellationToken)
    {
        var payload = await SendAsync<AiPreprocessingApiResponse>(
            token => httpClient.PostAsJsonAsync(
                "preprocessing",
                new AiPreprocessingRequest(message),
                token),
            "preprocessing",
            options.Value.PreprocessingTimeoutSeconds,
            cancellationToken);
        if (payload is null)
        {
            throw InvalidResponse("preprocessing", "The response body is empty.");
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
            throw InvalidResponse("preprocessing", "Required fields are missing or invalid.");
        }

        var warnings = payload.Warnings ?? [];
        if (warnings.Any(string.IsNullOrWhiteSpace) || payload.PartialSuccess && warnings.Count == 0)
        {
            throw InvalidResponse("preprocessing", "The warning list is invalid.");
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
        var payload = await SendAsync<AiGenerationApiResponse>(
            token => httpClient.PostAsJsonAsync("generate", request, token),
            "generate",
            options.Value.GenerationTimeoutSeconds,
            cancellationToken);
        if (payload is null)
        {
            throw InvalidResponse("generate", "The response body is empty.");
        }

        if (!payload.Success)
        {
            throw CreateServiceError("generate", payload.Error);
        }

        if (string.IsNullOrWhiteSpace(payload.Response) ||
            string.IsNullOrWhiteSpace(payload.PromptVersion) ||
            string.IsNullOrWhiteSpace(payload.ModelName) ||
            string.IsNullOrWhiteSpace(payload.ModelId) ||
            payload.ModelParameters is null ||
            payload.ReferenceSimilarity.HasValue !=
                !string.IsNullOrWhiteSpace(payload.ReferenceSimilarityMetric) ||
            payload.ReferenceSimilarity is < 0 or > 1 ||
            payload.ReferenceSimilarityMetric is not null &&
                string.IsNullOrWhiteSpace(payload.ReferenceSimilarityMetric))
        {
            throw InvalidResponse("generate", "Required fields are missing or invalid.");
        }

        return new AiGenerationResult(
            payload.Response,
            payload.PromptVersion,
            payload.ModelName,
            payload.ModelId,
            payload.ModelParameters,
            payload.ReferenceSimilarity,
            payload.ReferenceSimilarityMetric);
    }

    private async Task<T?> SendAsync<T>(
        Func<CancellationToken, Task<HttpResponseMessage>> sendRequest,
        string endpoint,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeoutTokenSource(cancellationToken, timeoutSeconds);
        try
        {
            using var response = await sendRequest(timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            var responseCorrelationId = response.Headers.TryGetValues("x-correlation-id", out var values)
                ? values.FirstOrDefault()
                : null;

            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpError(endpoint, response.StatusCode, responseCorrelationId, body);
            }

            try
            {
                return JsonSerializer.Deserialize<T>(body, ResponseJsonOptions);
            }
            catch (JsonException exception)
            {
                throw InvalidResponse(endpoint, "The response body is not valid JSON.", exception);
            }
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new AiServiceException(
                AiServiceFailureKind.Timeout,
                endpoint,
                $"The AI service '{endpoint}' timed out after {timeoutSeconds} seconds.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiServiceException(
                AiServiceFailureKind.Unavailable,
                endpoint,
                $"The AI service '{endpoint}' is unavailable: {exception.Message}",
                exception.StatusCode,
                innerException: exception);
        }
    }

    private static AiServiceException CreateServiceError(string endpoint, AiServiceError? error)
    {
        var details = error is null
            ? "The service reported an unsuccessful result."
            : $"{error.Code ?? "AI_ERROR"}: {error.Message ?? "No error message was provided."}";

        return new AiServiceException(
            AiServiceFailureKind.RemoteFailure,
            endpoint,
            $"The AI service '{endpoint}' failed. {details}",
            correlationId: error?.CorrelationId);
    }

    private static AiServiceException CreateHttpError(
        string endpoint,
        HttpStatusCode statusCode,
        string? responseCorrelationId,
        string body)
    {
        AiServiceError? error = null;
        try
        {
            error = JsonSerializer.Deserialize<AiServiceErrorEnvelope>(body, ResponseJsonOptions)?.Error;
        }
        catch (JsonException)
        {
        }

        var correlationId = error?.CorrelationId ?? responseCorrelationId;
        var message = error?.Message ?? $"HTTP {(int)statusCode} ({statusCode}).";
        var failureKind = statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout
            ? AiServiceFailureKind.Timeout
            : (int)statusCode >= 500
                ? AiServiceFailureKind.Unavailable
                : AiServiceFailureKind.HttpError;

        return new AiServiceException(
            failureKind,
            endpoint,
            $"The AI service '{endpoint}' returned HTTP {(int)statusCode}: {message}" +
            (string.IsNullOrWhiteSpace(error?.Code) ? string.Empty : $" Code: {error.Code}.") +
            (string.IsNullOrWhiteSpace(correlationId) ? string.Empty : $" Correlation ID: {correlationId}."),
            statusCode,
            correlationId);
    }

    private static AiServiceException InvalidResponse(
        string endpoint,
        string message,
        Exception? innerException = null) =>
        new(
            AiServiceFailureKind.InvalidResponse,
            endpoint,
            $"The AI service '{endpoint}' returned an invalid response. {message}",
            innerException: innerException);

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

public enum AiServiceFailureKind
{
    Unavailable,
    Timeout,
    HttpError,
    RemoteFailure,
    InvalidResponse
}

public sealed class AiServiceException : Exception
{
    public AiServiceException(
        AiServiceFailureKind failureKind,
        string endpoint,
        string message,
        HttpStatusCode? statusCode = null,
        string? correlationId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        Endpoint = endpoint;
        StatusCode = statusCode;
        CorrelationId = correlationId;
    }

    public AiServiceFailureKind FailureKind { get; }
    public string Endpoint { get; }
    public HttpStatusCode? StatusCode { get; }
    public string? CorrelationId { get; }
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
    [property: JsonPropertyName("reference_similarity")] decimal? ReferenceSimilarity = null,
    [property: JsonPropertyName("reference_similarity_metric")] string? ReferenceSimilarityMetric = null,
    [property: JsonPropertyName("error")] AiServiceError? Error = null);

public sealed record AiGenerationResult(
    string Response,
    string PromptVersion,
    string ModelName,
    string ModelId,
    Dictionary<string, JsonElement> ModelParameters,
    decimal? ReferenceSimilarity,
    string? ReferenceSimilarityMetric);

public sealed record AiServiceError(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("correlation_id")] string? CorrelationId);

public sealed record AiServiceErrorEnvelope(
    [property: JsonPropertyName("error")] AiServiceError? Error);