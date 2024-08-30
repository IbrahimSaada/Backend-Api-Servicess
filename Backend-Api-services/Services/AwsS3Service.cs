using System;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Backend_Api_services.Services.Interfaces;

namespace Backend_Api_services.Services
{
    public class AwsS3Service : IAwsS3Service
    {
        private readonly IAwsSettings _awsS3Settings;
        private readonly ILogger<AwsS3Service> _logger;
        private readonly IAmazonS3 _s3Client;

        public AwsS3Service(IAwsSettings awsS3Settings, ILogger<AwsS3Service> logger)
        {
            _awsS3Settings = awsS3Settings;
            _logger = logger;
            _s3Client = new AmazonS3Client(
                _awsS3Settings.AccessKeyId,
                _awsS3Settings.SecretKey,
                _awsS3Settings.RegionEndpoint
            );
        }

        // Generate a presigned URL for uploading a file to a specified folder in S3
        public string GetPresignedUrl(string objectKey, string folderName)
        {
            int presignedUrlTimeoutMinutes = 60; // 1 hour

            // Combine folder name and object key to form the full key in S3
            string fullObjectKey = $"{folderName.TrimEnd('/')}/{objectKey}";

            var request = new GetPreSignedUrlRequest
            {
                BucketName = ClientUploadBucket, // Use the dynamic bucket name
                Key = fullObjectKey, // Store files in the specified folder
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(presignedUrlTimeoutMinutes)
            };

            // Generate the presigned URL using the S3 client
            string preSignedUrl = _s3Client.GetPreSignedURL(request);

            // Log the generated presigned URL for debugging purposes
            _logger.LogInformation("Generated presigned URL for folder '{FolderName}': {PresignedUrl}", folderName, preSignedUrl);

            return preSignedUrl;
        }

        // Dynamically construct the S3 bucket name using the environment's short name
        public string ClientUploadBucket => $"homepagecooking"; // Static bucket name for now
    }
}
