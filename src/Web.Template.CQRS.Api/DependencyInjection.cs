using Web.Template.CQRS.Api.Authentication;
using Web.Template.CQRS.Api.Authorization;
using Web.Template.CQRS.Application.Common.Interfaces.Authentication;

namespace Web.Template.CQRS.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddTemplateAuthentication(configuration, environment);
        services.AddTemplateAuthorization(configuration);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        return services;
    }
}
