using System.Net;
using System.Text.Json;
using Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

namespace Web.Template.CQRS.Api.IntegrationTests.RateLimiting;

public sealed class RateLimitingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Matches the permit limit of the "read" policy in Api/Common/RateLimiting/RateLimiting.cs.
    private const int ReadPermitLimit = 600;

    [Fact]
    public async Task ReadPolicy_WhenQuotaExceeded_Returns429ProblemDetails()
    {
        var client = factory.CreateClient().AsUser("rate-limited-user");
        var cancellationToken = TestContext.Current.CancellationToken;

        for (var request = 0; request < ReadPermitLimit; request++)
        {
            var accepted = await client.GetAsync("/me", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        var rejected = await client.GetAsync("/me", cancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("RATE_LIMITED", problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }
}
