using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VPD.Application.Common.Interfaces.Authentication;
using VPD.Application.Common.Interfaces.Persistence;
using VPD.Application.Common.Interfaces.Services;
using VPD.Infrastructure.Authentication;
using VPD.Infrastructure.Persistence;
using VPD.Infrastructure.Persistence.Repositories;
using VPD.Infrastructure.Services;

namespace VPD.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        ConfigurationManager builderConfiguration)
    {
        var connectionString = builderConfiguration.GetConnectionString("MariageDatabase");
            
        services
            .AddAuth(builderConfiguration)
            .AddDbContext<VPDDbContext>(options =>
                options.UseSqlServer(connectionString)
                )
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