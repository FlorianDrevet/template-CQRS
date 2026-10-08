namespace Web.Template.CQRS.Contracts.Authentication;

public sealed record CurrentUserResponse(
    string? Id,
    string? DisplayName,
    string? Email);
