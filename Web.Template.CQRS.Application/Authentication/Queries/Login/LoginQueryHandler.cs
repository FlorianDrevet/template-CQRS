using ErrorOr;
using MediatR;
using Web.Template.CQRS.Application.Authentication.Common;
using Web.Template.CQRS.Application.Common.Interfaces.Authentication;
using Web.Template.CQRS.Application.Common.Interfaces.Persistence;
using Web.Template.CQRS.Domain.Common.Errors;
using Web.Template.CQRS.Domain.UserAggregate;

namespace Web.Template.CQRS.Application.Authentication.Queries.Login;

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