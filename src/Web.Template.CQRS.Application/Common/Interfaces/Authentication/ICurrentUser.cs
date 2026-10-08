namespace Web.Template.CQRS.Application.Common.Interfaces.Authentication;

public interface ICurrentUser
{
    string? Id { get; }

    string? DisplayName { get; }

    string? Email { get; }
}
