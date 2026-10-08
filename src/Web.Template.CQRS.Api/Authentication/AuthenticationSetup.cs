using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace Web.Template.CQRS.Api.Authentication;

public static class AuthenticationSetup
{
    public static IServiceCollection AddTemplateAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authSection = configuration.GetSection(AuthOptions.SectionName);
        var authOptions = authSection.Get<AuthOptions>() ?? new AuthOptions();

        services.AddOptions<AuthOptions>()
            .Bind(authSection)
            .Validate(options => Enum.IsDefined(options.Provider),
                "Auth:Provider must be Entra or Keycloak.")
            .Validate(options => options.Provider != AuthProvider.Entra
                || (!string.IsNullOrWhiteSpace(options.Instance)
                    && !string.IsNullOrWhiteSpace(options.TenantId)
                    && !string.IsNullOrWhiteSpace(options.ClientId)),
                "Auth:Instance, Auth:TenantId and Auth:ClientId are required for Entra.")
            .Validate(options => options.Provider != AuthProvider.Keycloak
                || (Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority)
                    && (authority.Scheme == Uri.UriSchemeHttps
                        || (environment.IsDevelopment()
                            && authority.Scheme == Uri.UriSchemeHttp
                            && authority.IsLoopback))),
                "Auth:Authority must use HTTPS, except for loopback HTTP during development.")
            .Validate(options => options.Provider != AuthProvider.Keycloak
                || !string.IsNullOrWhiteSpace(options.Audience),
                "Auth:Audience is required for Keycloak.")
            .ValidateOnStart();

        var authentication = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
        if (authOptions.Provider == AuthProvider.Entra)
        {
            authentication.AddMicrosoftIdentityWebApi(authSection);
            services.Configure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                ConfigureCommonJwtOptions);
        }
        else
        {
            authentication.AddJwtBearer(options =>
            {
                ConfigureCommonJwtOptions(options);
                options.Authority = authOptions.Authority;
                options.Audience = authOptions.Audience;
                options.RequireHttpsMetadata = !environment.IsDevelopment();
            });
        }

        return services;
    }

    private static void ConfigureCommonJwtOptions(JwtBearerOptions options)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "roles";
    }
}
