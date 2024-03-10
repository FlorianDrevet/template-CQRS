using ErrorOr;
using MediatR;
using Web.Template.CQRS.Application.Authentication.Common;

namespace Web.Template.CQRS.Application.Authentication.Commands.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string Firstname,
    string Lastname) : IRequest<ErrorOr<AuthenticationResult>>;