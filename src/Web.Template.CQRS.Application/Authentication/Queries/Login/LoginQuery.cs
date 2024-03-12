using ErrorOr;
using MediatR;
using Web.Template.CQRS.Application.Authentication.Common;

namespace Web.Template.CQRS.Application.Authentication.Queries.Login;

public record LoginQuery(string Email, string Password) : IRequest<ErrorOr<AuthenticationResult>>;