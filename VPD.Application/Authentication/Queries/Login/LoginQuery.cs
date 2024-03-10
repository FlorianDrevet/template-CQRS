using ErrorOr;
using MediatR;
using VPD.Application.Authentication.Common;

namespace VPD.Application.Authentication.Queries.Login;

public record LoginQuery(string Email, string Password) : IRequest<ErrorOr<AuthenticationResult>>;