using ErrorOr;
using MediatR;
using VPD.Application.Authentication.Common;

namespace VPD.Application.Authentication.Commands.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string Firstname,
    string Lastname) : IRequest<ErrorOr<AuthenticationResult>>;