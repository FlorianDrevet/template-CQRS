namespace Web.Template.CQRS.Contracts.Authentication;

public record LoginRequest (
    string Email,
    string Password);
