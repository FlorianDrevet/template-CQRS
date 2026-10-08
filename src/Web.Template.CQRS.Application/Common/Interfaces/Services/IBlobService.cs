namespace Web.Template.CQRS.Application.Common.Interfaces.Services;

public interface IBlobService
{
    Task<Uri> UploadFileAsync(Stream content, string fileName, string? contentType = null);
    Task<bool> DeleteFileAsync(string fileName);
}
