using VPD.Domain.UserAggregate;

namespace VPD.Application.Authentication.Common;

public record AuthenticationResult(
    User User,
    string Token);