using System.Diagnostics;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Web.Template.CQRS.Api.Common.RateLimiting;

public static class RateLimiting
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitingPolicies.Read, context => CreatePartition(context, 600));
            options.AddPolicy(RateLimitingPolicies.Write, context => CreatePartition(context, 120));
            options.AddPolicy(RateLimitingPolicies.Generate, context => CreatePartition(context, 10));
            options.AddPolicy(RateLimitingPolicies.Publish, context => CreatePartition(context, 5));

            options.OnRejected = async (rejection, cancellationToken) =>
            {
                var context = rejection.HttpContext;
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too many requests.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "RATE_LIMITED",
                        ["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
                    }).ExecuteAsync(context);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(HttpContext context, int permitLimit)
    {
        var subject = context.User.FindFirst("oid")?.Value
            ?? context.User.FindFirst("sub")?.Value;
        var partitionKey = !string.IsNullOrWhiteSpace(subject)
            ? $"subject:{subject}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            });
    }
}
