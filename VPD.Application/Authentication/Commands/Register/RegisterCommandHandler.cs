using ErrorOr;
using MediatR;
using VPD.Application.Authentication.Common;
using VPD.Application.Common.Interfaces.Authentication;
using VPD.Application.Common.Interfaces.Persistence;
using VPD.Domain.Common.Errors;
using VPD.Domain.UserAggregate;
using VPD.Domain.UserAggregate.ValueObjects;

namespace VPD.Application.Authentication.Commands.Register;

public class RegisterCommandHandler(
    IJwtGenerator jwtGenerator,
    IHashPassword hashPassword,
    IUserRepository userRepository) : IRequestHandler<RegisterCommand, ErrorOr<AuthenticationResult>>
{
    public async Task<ErrorOr<AuthenticationResult>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        if (userRepository.GetUserByEmail(command.Email) is not null)
        {
            return Errors.User.DuplicateEmailError();
        }
        
        var hashedPassword = hashPassword.GetHashedPassword(command.Password);
        var user = User
            .Create(command.Email, hashedPassword.Item2, new Name(command.Firstname, command.Lastname),hashedPassword.Item1);
        userRepository.AddUser(user);
        
        var token = jwtGenerator.GenerateToken(user);
        return new AuthenticationResult(user, token);
    }
}