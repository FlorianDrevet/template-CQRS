namespace Web.Template.CQRS.Application.Users.Common;

public sealed record CurrentUserResult(
    string? Id,
    string? DisplayName,
    string? Email);
