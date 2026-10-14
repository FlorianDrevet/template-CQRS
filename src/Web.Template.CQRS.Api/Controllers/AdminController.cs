using Mediator;
using Web.Template.CQRS.Api.Authorization;
using Web.Template.CQRS.Application.Admin.Queries.PingAdmin;
using Web.Template.CQRS.Contracts.Admin;

#if (EnableRateLimiting)
using Microsoft.AspNetCore.RateLimiting;
using Web.Template.CQRS.Api.Common.RateLimiting;
#endif

namespace Web.Template.CQRS.Api.Controllers;

public static class AdminController
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var pingEndpoint = endpoints.MapGet("/admin/ping", async (
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new PingAdminQuery(), cancellationToken);
                return TypedResults.Ok(new PingAdminResponse(result.Message, result.UserId));
            })
            .WithName("PingAdmin")
            .RequireAuthorization(Policies.RequireAdmin);

#if (EnableRateLimiting)
        pingEndpoint.RequireRateLimiting(RateLimitingPolicies.Read);
#endif

        return endpoints;
    }
}
