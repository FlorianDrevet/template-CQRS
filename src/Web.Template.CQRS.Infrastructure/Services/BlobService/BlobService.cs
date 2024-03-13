using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Web.Template.CQRS.Application.Common.Interfaces.Services;

namespace Web.Template.CQRS.Infrastructure.Services.BlobService;
public class BlobService
    : IBlobService
{
    private readonly BlobContainerClient _blobContainerClient;

    public BlobService(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        IOptions<BlobSettings> blobStorageSettings)
    {
        var client = clientFactory.CreateClient(blobStorageSettings.Value.StorageAccountName);
        _blobContainerClient = client.GetBlobContainerClient(blobStorageSettings.Value.ContainerName);
    }
    
    public async Task<Uri> UploadFileAsync(IFormFile formFile)
    {
        var fileName = Path.GetFileName(formFile.FileName);
        await using var stream = formFile.OpenReadStream();
        
        var blobClient = _blobContainerClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(stream, true);
        return blobClient.Uri;
    }

    public Task<string> DeleteFileAsync(string fileName)
    {
        throw new NotImplementedException();
    }
}
