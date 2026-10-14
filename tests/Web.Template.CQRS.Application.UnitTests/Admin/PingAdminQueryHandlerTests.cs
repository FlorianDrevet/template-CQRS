using Web.Template.CQRS.Application.Admin.Queries.PingAdmin;
using Web.Template.CQRS.Application.UnitTests.Fakes;

namespace Web.Template.CQRS.Application.UnitTests.Admin;

public sealed class PingAdminQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPongWithCurrentUserId()
    {
        var handler = new PingAdminQueryHandler(new FakeCurrentUser { Id = "admin-1" });

        var result = await handler.Handle(new PingAdminQuery(), TestContext.Current.CancellationToken);

        Assert.Equal("pong", result.Message);
        Assert.Equal("admin-1", result.UserId);
    }
}
