using Web.Template.CQRS.Application.UnitTests.Fakes;
using Web.Template.CQRS.Application.Users.Queries.GetCurrentUser;

namespace Web.Template.CQRS.Application.UnitTests.Users;

public sealed class GetCurrentUserQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCurrentUserIdentity()
    {
        var currentUser = new FakeCurrentUser
        {
            Id = "user-1",
            DisplayName = "Ada Lovelace",
            Email = "ada@contoso.test"
        };
        var handler = new GetCurrentUserQueryHandler(currentUser);

        var result = await handler.Handle(new GetCurrentUserQuery(), TestContext.Current.CancellationToken);

        Assert.Equal("user-1", result.Id);
        Assert.Equal("Ada Lovelace", result.DisplayName);
        Assert.Equal("ada@contoso.test", result.Email);
    }
}
