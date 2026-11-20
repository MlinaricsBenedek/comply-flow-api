using comply_flow_api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace comply_flow_api.Middleware;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (statusCode, title, code) = MapException(exception);
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Request {TraceId} failed with {ErrorCode}.",
                httpContext.TraceIdentifier,
                code);
        }
        else
        {
            logger.LogWarning(
                "Request {TraceId} was rejected with {ErrorCode}: {ExceptionMessage}",
                httpContext.TraceIdentifier,
                code,
                exception.Message);
        }

        var problem = ApiProblemDetails.Create(
            httpContext,
            statusCode,
            title,
            code,
            statusCode == StatusCodes.Status400BadRequest
                ? "The request is invalid."
                : null);

        if (exception is AiServiceException aiException)
        {
            if (!string.IsNullOrWhiteSpace(aiException.ErrorCode))
            {
                problem.Extensions["aiErrorCode"] = aiException.ErrorCode;
            }

            if (!string.IsNullOrWhiteSpace(aiException.CorrelationId))
            {
                problem.Extensions["aiCorrelationId"] = aiException.CorrelationId;
            }
        }

        httpContext.Response.StatusCode = statusCode;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });

        return true;
    }

    private static (int StatusCode, string Title, string Code) MapException(Exception exception) =>
        exception switch
        {
            AiServiceException { FailureKind: AiServiceFailureKind.Timeout } =>
                (StatusCodes.Status504GatewayTimeout, "AI service timed out.", "ai_service_timeout"),
            AiServiceException { FailureKind: AiServiceFailureKind.Unavailable } =>
                (StatusCodes.Status503ServiceUnavailable, "AI service is unavailable.", "ai_service_unavailable"),
            AiServiceException { FailureKind: AiServiceFailureKind.ConfigurationError } =>
                (StatusCodes.Status502BadGateway, "AI service configuration failed.", "ai_service_configuration_error"),
            AiServiceException =>
                (StatusCodes.Status502BadGateway, "AI service request failed.", "ai_service_failure"),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "The request is invalid.", "invalid_request"),
            _ =>
                (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "internal_error")
        };
}
