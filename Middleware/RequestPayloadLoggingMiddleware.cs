using System.Text;
using comply_flow_api.Services;

namespace comply_flow_api.Middleware;

public sealed class RequestPayloadLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PayloadFileLogger payloadFileLogger)
    {
        var request = context.Request;
        string? body = null;

        if (request.ContentLength != 0 &&
            request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            request.EnableBuffering();
            using var reader = new StreamReader(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            try
            {
                body = await reader.ReadToEndAsync(context.RequestAborted);
            }
            finally
            {
                request.Body.Position = 0;
            }
        }

        await payloadFileLogger.LogAsync(
            "inbound",
            request.Method,
            request.Path.ToString() + request.QueryString.ToString(),
            context.TraceIdentifier,
            body);

        await next(context);
    }
}
