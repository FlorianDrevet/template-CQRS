using System.Diagnostics;

namespace Web.Template.CQRS.Api.Observability;

public sealed class RequestLoggingScopeMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingScopeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var endpointName = context.GetEndpoint()?.DisplayName ?? "unmatched";
        Activity.Current?.SetTag("app.endpoint", endpointName);

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["RequestId"] = context.TraceIdentifier,
            ["Endpoint"] = endpointName
        }))
        {
            await next(context);
        }
    }
}
