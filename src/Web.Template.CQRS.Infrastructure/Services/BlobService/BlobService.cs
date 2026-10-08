using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Web.Template.CQRS.Application.Common.Interfaces.Services;

namespace Web.Template.CQRS.Infrastructure.Services.BlobService;
public class BlobService
    : IBlobService
{
    private readonly BlobContainerClient _blobContainerClient;

    public BlobService(
        BlobServiceClient blobServiceClient,
        IOptions<BlobSettings> blobStorageSettings)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(blobStorageSettings.Value.ContainerName);
    }
    
    public async Task<Uri> UploadFileAsync(Stream content, string fileName, string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        await _blobContainerClient.CreateIfNotExistsAsync();

        var blobClient = _blobContainerClient.GetBlobClient(fileName);
        var headers = string.IsNullOrWhiteSpace(contentType)
            ? null
            : new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = headers,
            TransferOptions = new StorageTransferOptions
            {
                InitialTransferSize = 4 * 1024 * 1024,
                MaximumTransferSize = 4 * 1024 * 1024
            }
        });
        return blobClient.Uri;
    }

    public async Task<bool> DeleteFileAsync(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        await _blobContainerClient.CreateIfNotExistsAsync();
        var response = await _blobContainerClient.DeleteBlobIfExistsAsync(fileName);
        return response.Value;
    }
}
