namespace Backend_Api_services.Services.Interfaces
{
    public interface IAwsS3Service
    {
        string GetPresignedUrl(string objectKey);
        string ClientUploadBucket { get; }
    }
}