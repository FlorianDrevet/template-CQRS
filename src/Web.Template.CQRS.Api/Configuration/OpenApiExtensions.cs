using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Web.Template.CQRS.Api.Authentication;

namespace Web.Template.CQRS.Api.Configuration;

public static class OpenApiExtensions
{
    public static IServiceCollection AddOpenApiExtensions(
        this IServiceCollection services,
        AuthOptions authOptions)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (!metadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(
                            ScalarOAuth2.SecuritySchemeName,
                            context.Document)] = ScalarOAuth2.Scopes(authOptions).ToList()
                    });
                }

                return Task.CompletedTask;
            });

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Web.Template.CQRS API v1";
                document.Servers = [];
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[ScalarOAuth2.SecuritySchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri(ScalarOAuth2.AuthorizationUrl(authOptions)),
                            TokenUrl = new Uri(ScalarOAuth2.TokenUrl(authOptions)),
                            Scopes = ScalarOAuth2.ScopeDescriptions(authOptions)
                        }
                    }
                };

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
