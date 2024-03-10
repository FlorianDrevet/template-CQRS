using VPD.Api.Common.Mapping;

namespace VPD.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddMapping();
        services.AddAuthorization();
        return services;
    }
}