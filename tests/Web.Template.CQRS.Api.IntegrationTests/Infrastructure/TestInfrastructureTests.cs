using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Template.CQRS.Infrastructure.Persistence;

namespace Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

public sealed class TestInfrastructureTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void DbContext_IsReplacedByInMemoryProvider()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProjectDbContext>();

        Assert.True(dbContext.Database.IsInMemory());
    }
}
