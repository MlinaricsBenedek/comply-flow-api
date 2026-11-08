using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace comply_flow_api.Middleware;

public static class ApiProblemDetails
{
    public static ProblemDetails Create(
        HttpContext context,
        int statusCode,
        string title,
        string code,
        string? detail = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = "about:blank",
            Instance = context.Request.Path,
            Detail = detail
        };
        AddExtensions(problem, context, code);
        return problem;
    }

    public static ValidationProblemDetails CreateValidation(
        HttpContext context,
        ModelStateDictionary modelState)
    {
        var problem = new ValidationProblemDetails(modelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Type = "about:blank",
            Instance = context.Request.Path
        };
        AddExtensions(problem, context, "validation_error");
        return problem;
    }

    private static void AddExtensions(ProblemDetails problem, HttpContext context, string code)
    {
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
    }
}
