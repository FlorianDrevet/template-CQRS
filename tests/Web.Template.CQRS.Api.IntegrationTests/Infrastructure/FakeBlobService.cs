using Web.Template.CQRS.Application.Common.Interfaces.Services;

namespace Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

public sealed class FakeBlobService : IBlobService
{
    public Task<Uri> UploadFileAsync(Stream content, string fileName, string? contentType = null) =>
        Task.FromResult(new Uri($"https://blobs.tests.local/uploads/{fileName}"));

    public Task<bool> DeleteFileAsync(string fileName) => Task.FromResult(true);
}
