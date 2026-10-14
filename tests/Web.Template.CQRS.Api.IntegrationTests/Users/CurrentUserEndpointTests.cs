using System.Net;
using System.Net.Http.Json;
using Web.Template.CQRS.Api.IntegrationTests.Infrastructure;
using Web.Template.CQRS.Contracts.Authentication;

namespace Web.Template.CQRS.Api.IntegrationTests.Users;

public sealed class CurrentUserEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Me_WithoutAuthentication_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithAuthenticatedUser_ReturnsIdentityRolesAndGroups()
    {
        var client = factory.CreateClient().AsUser(
            "user-42",
            roles: ["Admin", "User"],
            groups: ["6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"]);

        var body = await client.GetFromJsonAsync<CurrentUserResponse>("/me", TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("user-42", body.Id);
        Assert.Equal("Test user-42", body.DisplayName);
        Assert.Equal("user-42@tests.local", body.Email);
        Assert.Equal(["Admin", "User"], body.Roles);
        Assert.Equal(["6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"], body.Groups);
    }

    [Fact]
    public async Task Me_WithUserWithoutRolesOrGroups_ReturnsEmptyArrays()
    {
        var client = factory.CreateClient().AsUser("user-without-rights");

        var response = await client.GetAsync("/me", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"roles\":[]", json);
        Assert.Contains("\"groups\":[]", json);
    }

    [Fact]
    public async Task Me_WithRolesHeaderButNoUser_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Admin");

        var response = await client.GetAsync("/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
