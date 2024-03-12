using Web.Template.CQRS.Domain.UserAggregate;

namespace Web.Template.CQRS.Application.Authentication.Common;

public record AuthenticationResult(
    User User,
    string Token);