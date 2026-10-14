using Microsoft.AspNetCore.Authorization;
#if (UseGroups)
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Web.Template.CQRS.Api.Authentication;
#endif

namespace Web.Template.CQRS.Api.Authorization;

public static class AuthorizationSetup
{
    public static IServiceCollection AddTemplateAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
#if (UseGroups)
        services.AddOptions<GroupOptions>()
            .Bind(configuration.GetSection(GroupOptions.SectionName))
            .Validate(options => Guid.TryParse(options.Administrators, out _),
                "Authorization:Groups:Administrators must be the object ID (GUID) of an Entra security group.")
            .ValidateOnStart();

#endif
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
#if (UseAuthorizationPolicies)

        services.AddOptions<AuthorizationOptions>()
            .Configure<IServiceProvider>((options, serviceProvider) =>
                options.AddPolicy(Policies.RequireAdmin, policy => ConfigureRequireAdmin(policy, serviceProvider)));
#endif

        return services;
    }
#if (UseAuthorizationPolicies)

    private static void ConfigureRequireAdmin(AuthorizationPolicyBuilder policy, IServiceProvider serviceProvider)
    {
#if (UseGroups)
        var administratorsGroupId = Guid.Parse(serviceProvider
            .GetRequiredService<IOptions<GroupOptions>>()
            .Value
            .Administrators);
#endif
#if (UseAppRoles && UseGroups)
        policy.RequireAssertion(context =>
            context.User.IsInRole(AppRoles.Admin)
            || IsMemberOf(context.User, administratorsGroupId));
#elif (UseAppRoles)
        policy.RequireRole(AppRoles.Admin);
#else
        policy.RequireAssertion(context => IsMemberOf(context.User, administratorsGroupId));
#endif
    }
#endif
#if (UseGroups)

    private static bool IsMemberOf(ClaimsPrincipal user, Guid groupObjectId) =>
        user.FindAll(ClaimNames.Groups)
            .Any(claim => Guid.TryParse(claim.Value, out var id) && id == groupObjectId);
#endif
}
