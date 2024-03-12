namespace Web.Template.CQRS.Application.Common.Interfaces.Services;

public interface IBlobService
{
    public Task<string> UploadFileAsync(Stream fileStream, string fileName);
    public Task<string> DeleteFileAsync(string fileName);
}