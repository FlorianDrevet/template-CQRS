using Web.Template.CQRS.Domain.UserAggregate;

namespace Web.Template.CQRS.Application.Common.Interfaces.Authentication;

public interface IJwtGenerator
{
    string GenerateToken(User user);
}