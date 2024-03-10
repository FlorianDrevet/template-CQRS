using MapsterMapper;
using MediatR;
using VPD.Api.Errors;
using VPD.Application.Authentication.Commands.Register;
using VPD.Application.Authentication.Queries.Login;
using VPD.Contracts.Authentication;

namespace VPD.Api.Controllers;

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