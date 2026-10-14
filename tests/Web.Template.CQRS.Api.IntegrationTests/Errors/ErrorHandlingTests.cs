using System.Net;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Web.Template.CQRS.Api.Errors;

namespace Web.Template.CQRS.Api.IntegrationTests.Errors;

public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task ValidationException_Returns400ProblemDetailsWithErrors()
    {
        using var host = await StartHostAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await host.GetTestClient().GetAsync("/validation", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("VALIDATION", problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal(
            "Name is required.",
            problem.RootElement.GetProperty("errors").GetProperty("Name")[0].GetString());
    }

    [Fact]
    public async Task UnexpectedException_Returns500WithoutInternalDetails()
    {
        using var host = await StartHostAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await host.GetTestClient().GetAsync("/boom", cancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("INTERNAL", problem.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("Sensitive internal detail", body);
    }

    private static async Task<IHost> StartHostAsync()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddRouting())
                .Configure(app =>
                {
                    app.UseErrorHandling();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/validation", ThrowValidation);
                        endpoints.MapGet("/boom", ThrowUnexpected);
                    });
                }))
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }

    private static IResult ThrowValidation() =>
        throw new ValidationException([new ValidationFailure("Name", "Name is required.")]);

    private static IResult ThrowUnexpected() =>
        throw new InvalidOperationException("Sensitive internal detail");
}
