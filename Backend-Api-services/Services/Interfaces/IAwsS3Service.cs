namespace Backend_Api_services.Services.Interfaces
{
    public interface IAwsS3Service
    {
        // Generate a presigned URL for uploading a file to a specific folder in S3
        string GetPresignedUrl(string objectKey, string folderName);

        // Get the S3 bucket name
        string ClientUploadBucket { get; }
    }
}
