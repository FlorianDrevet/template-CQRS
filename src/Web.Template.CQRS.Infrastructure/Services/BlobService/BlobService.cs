using Azure.Storage.Blobs;
using Mariage.Infrastructure.Services.BlobService;
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
        var client = clientFactory.CreateClient(blobStorageSettings.Value.TableStorageAccount);
        _blobContainerClient = client.GetBlobContainerClient(blobStorageSettings.Value.ContainerName);
    }
    
    public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
    {
        var blobClient = _blobContainerClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(fileStream, true);
        return blobClient.Uri.ToString();
    }

    public Task<string> DeleteFileAsync(string fileName)
    {
        throw new NotImplementedException();
    }
}