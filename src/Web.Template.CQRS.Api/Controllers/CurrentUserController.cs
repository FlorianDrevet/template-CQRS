using Mediator;
using Web.Template.CQRS.Application.Users.Queries.GetCurrentUser;
using Web.Template.CQRS.Contracts.Authentication;

#if (EnableRateLimiting)
using Microsoft.AspNetCore.RateLimiting;
using Web.Template.CQRS.Api.Common.RateLimiting;
#endif

namespace Web.Template.CQRS.Api.Controllers;

public static class CurrentUserController
{
    public static IEndpointRouteBuilder MapCurrentUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var currentUserEndpoint = endpoints.MapGet("/me", async (
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var currentUser = await mediator.Send(new GetCurrentUserQuery(), cancellationToken);
                return TypedResults.Ok(new CurrentUserResponse(
                    currentUser.Id,
                    currentUser.DisplayName,
                    currentUser.Email));
            })
            .WithName("GetCurrentUser")
            .RequireAuthorization();

#if (EnableRateLimiting)
        currentUserEndpoint.RequireRateLimiting(RateLimitingPolicies.Read);
#endif

        return endpoints;
    }
}
