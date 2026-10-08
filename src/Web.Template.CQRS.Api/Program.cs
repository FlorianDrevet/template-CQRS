using System.Diagnostics;
using Scalar.AspNetCore;
using Web.Template.CQRS.Api;
using Web.Template.CQRS.Api.Authentication;
#if (EnableRateLimiting)
using Web.Template.CQRS.Api.Common.RateLimiting;
#endif
using Web.Template.CQRS.Api.Configuration;
using Web.Template.CQRS.Api.Controllers;
using Web.Template.CQRS.Api.Errors;
using Web.Template.CQRS.Api.Observability;
using Web.Template.CQRS.Application;
using Web.Template.CQRS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

#if (EnableAspire)
builder.AddServiceDefaults();
#endif

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        policy.AllowAnyHeader().AllowAnyMethod();
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiExtensions(authOptions);
builder.Services.AddPresentation(builder.Configuration, builder.Environment);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

#if (EnableRateLimiting)
builder.Services.AddRateLimiting();
#endif

var app = builder.Build();

app.UseErrorHandling();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<RequestLoggingScopeMiddleware>();

#if (EnableRateLimiting)
app.UseRateLimiter();
#endif

app.UseAuthorization();

#if (EnableAspire)
app.MapDefaultEndpoints();
#endif

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.AddDocuments("v1");
        options.WithTitle("Web.Template.CQRS API v1");
        options.AddAuthorizationCodeFlow(ScalarOAuth2.SecuritySchemeName, flow =>
        {
            flow.ClientId = ScalarOAuth2.ClientId(authOptions);
            flow.AuthorizationUrl = ScalarOAuth2.AuthorizationUrl(authOptions);
            flow.TokenUrl = ScalarOAuth2.TokenUrl(authOptions);
            flow.Pkce = Pkce.Sha256;
            flow.SelectedScopes = ScalarOAuth2.Scopes(authOptions);
        });
    }).AllowAnonymous();
}

app.MapCurrentUserEndpoints();
app.MapFallback(() => Results.NotFound()).AllowAnonymous();

app.Run();

public partial class Program
{
}
