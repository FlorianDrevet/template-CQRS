using ErrorOr;
using MediatR;
using VPD.Application.Authentication.Common;
using VPD.Application.Common.Interfaces.Authentication;
using VPD.Application.Common.Interfaces.Persistence;
using VPD.Domain.Common.Errors;
using VPD.Domain.UserAggregate;

namespace VPD.Application.Authentication.Queries.Login;

public class LoginQueryHandler(IJwtGenerator jwtGenerator, IUserRepository userRepository, IHashPassword hashPassword):
    IRequestHandler<LoginQuery, ErrorOr<AuthenticationResult>>
{
    public async Task<ErrorOr<AuthenticationResult>> Handle(LoginQuery query, CancellationToken cancellationToken)
    {
        if (userRepository.GetUserByEmail(query.Email) is not User user)
        {
            return Errors.Authentication.InvalidUsername();
        }
        
        var hashedPassword = hashPassword.GetHashedPassword(query.Password, user.Salt);
        if (user.Password != hashedPassword)
        {
            return Errors.Authentication.InvalidPassword();
        }
        
        var token = jwtGenerator.GenerateToken(user);
        
        return new AuthenticationResult(user, token);
    }
}