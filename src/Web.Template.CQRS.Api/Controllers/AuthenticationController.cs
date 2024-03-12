using MapsterMapper;
using MediatR;
using Web.Template.CQRS.Api.Errors;
using Web.Template.CQRS.Application.Authentication.Commands.Register;
using Web.Template.CQRS.Application.Authentication.Queries.Login;
using Web.Template.CQRS.Contracts.Authentication;

namespace Web.Template.CQRS.Api.Controllers;

public static class AuthenticationController
{
    public static IApplicationBuilder UseAuthenticationController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/auth/register",
                    async (RegisterRequest request, IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<RegisterCommand>(request);
                        var authenticationResult = await mediator.Send(command);

                        return authenticationResult.Match(
                            authenticationResult =>
                            {
                                var user = mapper.Map<AuthenticationResponse>(authenticationResult);
                                return Results.Ok(user);
                            },
                            error => error.Result());
                    })
                .WithName("Register")
                .RequireAuthorization("IsAdmin")
                .WithOpenApi();

            endpoints.MapPost("/auth/login",
                    async (LoginRequest request, IMediator mediator, IMapper mapper) =>
                    {
                        var query = mapper.Map<LoginQuery>(request);
                        var authenticationResult = await mediator.Send(query);
                        
                        return authenticationResult.Match(
                            authenticationResult =>
                            {
                                var user = mapper.Map<AuthenticationResponse>(authenticationResult);
                                return Results.Ok(user);
                            },
                            error => error.Result()
                            );
                    })
                .WithName("Login")
                .RequireRateLimiting("Login")
                .WithOpenApi();
        });
    }
}