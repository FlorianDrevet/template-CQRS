using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Web.Template.CQRS.Application.Common.Interfaces.Services;
using Web.Template.CQRS.Infrastructure.Persistence;

namespace Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
#if (UseGroups)
    public const string AdministratorsGroupId = "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11";

#endif
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth:Provider", "Keycloak");
        builder.UseSetting("Auth:Authority", "https://keycloak.tests.local/realms/template");
        builder.UseSetting("Auth:Audience", "template-api");
#if (UseGroups)
        builder.UseSetting("Authorization:Groups:Administrators", AdministratorsGroupId);
#endif

        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.RemoveAll<IDbContextOptionsConfiguration<ProjectDbContext>>();
            services.RemoveAll<DbContextOptions<ProjectDbContext>>();
            services.AddDbContext<ProjectDbContext>(options =>
                options.UseInMemoryDatabase("api-integration-tests"));

            services.RemoveAll<IBlobService>();
            services.AddSingleton<IBlobService, FakeBlobService>();
        });
    }
}
