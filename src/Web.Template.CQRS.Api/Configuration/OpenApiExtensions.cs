using Web.Template.CQRS.Api.Configuration.OpenApiTransformers;

namespace Web.Template.CQRS.Api.Configuration;

public static class OpenApiExtensions
{
    public static IServiceCollection AddOpenApiExtensions(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options => { options.AddDocumentTransformer<BearerSecuritySchemeTransformer>(); });
        return services;
    }
}
