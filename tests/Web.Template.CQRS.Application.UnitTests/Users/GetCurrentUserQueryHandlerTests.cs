using Web.Template.CQRS.Application.UnitTests.Fakes;
using Web.Template.CQRS.Application.Users.Queries.GetCurrentUser;

namespace Web.Template.CQRS.Application.UnitTests.Users;

public sealed class GetCurrentUserQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCurrentUserIdentityRolesAndGroups()
    {
        var currentUser = new FakeCurrentUser
        {
            Id = "user-1",
            DisplayName = "Ada Lovelace",
            Email = "ada@contoso.test",
            Roles = ["Admin"],
            Groups = ["6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"]
        };
        var handler = new GetCurrentUserQueryHandler(currentUser);

        var result = await handler.Handle(new GetCurrentUserQuery(), TestContext.Current.CancellationToken);

        Assert.Equal("user-1", result.Id);
        Assert.Equal("Ada Lovelace", result.DisplayName);
        Assert.Equal("ada@contoso.test", result.Email);
        Assert.Equal(["Admin"], result.Roles);
        Assert.Equal(["6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"], result.Groups);
    }
}
