using Microsoft.AspNetCore.Hosting;
using Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

namespace Web.Template.CQRS.Api.IntegrationTests.Authorization;

public sealed class GroupOptionsValidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void Startup_WithInvalidAdministratorsGroupId_Fails(string groupId)
    {
        using var invalidFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Authorization:Groups:Administrators", groupId));

        var exception = Record.Exception(() => invalidFactory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("Authorization:Groups:Administrators", exception.ToString());
    }
}
