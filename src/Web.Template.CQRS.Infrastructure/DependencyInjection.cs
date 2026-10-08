using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web.Template.CQRS.Application.Common.Interfaces.Services;
using Web.Template.CQRS.Infrastructure.Persistence;
using Web.Template.CQRS.Infrastructure.Services;
using Web.Template.CQRS.Infrastructure.Services.BlobService;

namespace Web.Template.CQRS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        ConfigurationManager configuration)
    {
        var databaseConnectionString = configuration.GetConnectionString("projectdb");

        services
            .AddDbContext<ProjectDbContext>(options =>
                options.UseSqlServer(databaseConnectionString))
            .AddAzureServices(configuration)
            .AddBlob(configuration);

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        return services;
    }

    private static IServiceCollection AddAzureServices(
        this IServiceCollection services,
        ConfigurationManager configuration)
    {
        services.AddAzureClients(clientBuilder =>
        {
            var connectionString = configuration.GetConnectionString("blobs")
                ?? configuration.GetConnectionString("AzureBlobStorageConnectionString")
                ?? string.Empty;
            clientBuilder.AddBlobServiceClient(connectionString);
        });
        return services;
    }

    private static IServiceCollection AddBlob(
        this IServiceCollection services,
        ConfigurationManager configuration)
    {
        var blobSettings = configuration.GetSection(BlobSettings.SectionName).Get<BlobSettings>()
            ?? new BlobSettings();

        services.AddSingleton(Options.Create(blobSettings));
        services.AddSingleton<IBlobService, BlobService>();
        return services;
    }
}
