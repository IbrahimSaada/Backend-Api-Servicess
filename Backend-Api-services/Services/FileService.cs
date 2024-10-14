using Amazon.S3.Model;
using Amazon.S3;
using Backend_Api_services.Services.Interfaces;

namespace Backend_Api_services.Services
{
    public class FileService : IFileService
    {
        private readonly IAmazonS3 _s3Client;

        public FileService(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string bucketName, string? prefix)
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists) throw new Exception($"Bucket {bucketName} does not exist.");

            var request = new PutObjectRequest()
            {
                BucketName = bucketName,
                Key = string.IsNullOrEmpty(prefix) ? file.FileName : $"{prefix?.TrimEnd('/')}/{file.FileName}",
                InputStream = file.OpenReadStream()
            };
            request.Metadata.Add("Content-Type", file.ContentType);
            await _s3Client.PutObjectAsync(request);

            return $"https://{bucketName}.s3.amazonaws.com/{request.Key}";
        }
    }
}
