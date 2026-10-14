namespace Web.Template.CQRS.Contracts.Admin;

public sealed record PingAdminResponse(string Message, string? UserId);
