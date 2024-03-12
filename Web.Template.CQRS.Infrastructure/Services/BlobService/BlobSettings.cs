namespace Mariage.Infrastructure.Services.BlobService;

public class BlobSettings
{
    public const string SectionName = "BlobSettings";
    public string TableStorageAccount { get; init; } = null!;
    public string ContainerName { get; init; } = null!;
}