using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Web.Template.CQRS.Application.Common.Interfaces.Authentication;
using Web.Template.CQRS.Application.Common.Interfaces.Persistence;
using Web.Template.CQRS.Application.Common.Interfaces.Services;
using Web.Template.CQRS.Infrastructure.Authentication;
using Web.Template.CQRS.Infrastructure.Persistence;
using Web.Template.CQRS.Infrastructure.Persistence.Repositories;
using Web.Template.CQRS.Infrastructure.Services;

namespace Web.Template.CQRS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        ConfigurationManager builderConfiguration)
    {
        var connectionString = builderConfiguration.GetConnectionString("ProjectDatabase");
            
        services
            .AddAuth(builderConfiguration)
            .AddDbContext<ProjectDbContext>(options =>
                options.UseSqlServer(connectionString)
                )
            .AddAzureServices(builderConfiguration)
            .AddRepositories();
        
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        
        return services;
    }

    private static IServiceCollection AddRepositories(
        this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        return services;
    }

    private static IServiceCollection AddAzureServices(
        this IServiceCollection services,
        ConfigurationManager builderConfiguration)
    {
        services.AddAzureClients(clientBuilder =>
        {
            // Blob Service
            string connectionString = builderConfiguration.GetConnectionString("AzureBlobStorageConnectionString") ?? string.Empty;
            clientBuilder.AddBlobServiceClient(connectionString); 
        });
        return services;
    }
    
    private static IServiceCollection AddAuth(
        this IServiceCollection services,
        ConfigurationManager builderConfiguration)
    {
        var jwtSettings = new JwtSettings();
        builderConfiguration.Bind(JwtSettings.SectionName, jwtSettings);

        services.AddSingleton(Options.Create(jwtSettings));
        services.AddSingleton<IJwtGenerator, JwtGenerator>();
        services.AddSingleton<IHashPassword, HashPassword>();
        services.AddAuthentication(defaultScheme: JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.Secret)
                    ),
            });
        return services;
    }
}