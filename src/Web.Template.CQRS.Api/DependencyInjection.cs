using Web.Template.CQRS.Api.Common.Mapping;

namespace Web.Template.CQRS.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddMapping();
        services.AddAuthorization();
        return services;
    }
}