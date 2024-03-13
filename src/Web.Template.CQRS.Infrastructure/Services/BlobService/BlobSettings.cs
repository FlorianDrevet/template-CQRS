namespace Web.Template.CQRS.Infrastructure.Services.BlobService;

public class BlobSettings
{
    public const string SectionName = "BlobSettings";
    public string StorageAccountName { get; init; } = null!;
    public string ContainerName { get; init; } = null!;
}