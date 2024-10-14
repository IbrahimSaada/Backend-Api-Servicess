namespace Backend_Api_services.Services.Interfaces
{
    public interface IFileService
    {
        Task<string> UploadFileAsync(IFormFile file, string bucketName, string? prefix);
    }
}
