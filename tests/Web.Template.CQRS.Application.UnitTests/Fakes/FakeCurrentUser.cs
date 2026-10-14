using Web.Template.CQRS.Application.Common.Interfaces.Authentication;

namespace Web.Template.CQRS.Application.UnitTests.Fakes;

public sealed class FakeCurrentUser : ICurrentUser
{
    public string? Id { get; init; }

    public string? DisplayName { get; init; }

    public string? Email { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public IReadOnlyList<string> Groups { get; init; } = [];
}
