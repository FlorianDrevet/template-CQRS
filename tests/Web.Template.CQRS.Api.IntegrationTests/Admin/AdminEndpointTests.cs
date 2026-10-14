using System.Net;
using System.Net.Http.Json;
using Web.Template.CQRS.Api.IntegrationTests.Infrastructure;
using Web.Template.CQRS.Contracts.Admin;
#if (UseAppRoles)
using Web.Template.CQRS.Api.Authorization;
#endif

namespace Web.Template.CQRS.Api.IntegrationTests.Admin;

public sealed class AdminEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Ping_WithoutAuthentication_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/admin/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ping_AsStandardUser_Returns403()
    {
#if (UseAppRoles)
        var client = factory.CreateClient().AsUser("standard-user", roles: [AppRoles.User]);
#else
        var client = factory.CreateClient().AsUser("standard-user");
#endif

        var response = await client.GetAsync("/admin/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
#if (UseAppRoles)

    [Fact]
    public async Task Ping_WithAdminRole_Returns200()
    {
        var client = factory.CreateClient().AsUser("admin-by-role", roles: [AppRoles.Admin]);

        var body = await client.GetFromJsonAsync<PingAdminResponse>("/admin/ping", TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("pong", body.Message);
        Assert.Equal("admin-by-role", body.UserId);
    }
#endif
#if (UseGroups)

    [Fact]
    public async Task Ping_AsMemberOfAdministratorsGroup_Returns200()
    {
        var client = factory.CreateClient().AsUser("admin-by-group", groups: [ApiFactory.AdministratorsGroupId]);

        var body = await client.GetFromJsonAsync<PingAdminResponse>("/admin/ping", TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("admin-by-group", body.UserId);
    }

    [Fact]
    public async Task Ping_WithGroupIdInDifferentCase_Returns200()
    {
        var client = factory.CreateClient().AsUser(
            "admin-by-group-uppercase",
            groups: [ApiFactory.AdministratorsGroupId.ToUpperInvariant()]);

        var response = await client.GetAsync("/admin/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
#endif
}
