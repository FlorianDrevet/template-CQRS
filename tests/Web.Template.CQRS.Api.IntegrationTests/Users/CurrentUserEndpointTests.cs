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
    public async Task Me_WithAuthenticatedUser_ReturnsIdentityClaims()
    {
        var client = factory.CreateClient().AsUser("user-42");

        var body = await client.GetFromJsonAsync<CurrentUserResponse>("/me", TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("user-42", body.Id);
        Assert.Equal("Test user-42", body.DisplayName);
        Assert.Equal("user-42@tests.local", body.Email);
    }
}
