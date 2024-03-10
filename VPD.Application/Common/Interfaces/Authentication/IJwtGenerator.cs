using VPD.Domain.UserAggregate;

namespace VPD.Application.Common.Interfaces.Authentication;

public interface IJwtGenerator
{
    string GenerateToken(User user);
}