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
        private readonly IEnvironmentSettings _environmentSettings;
        private readonly ILogger<AwsS3Service> _logger;

        public AwsS3Service(IAwsSettings awsS3Settings, IEnvironmentSettings environmentSettings, ILogger<AwsS3Service> logger)
        {
            _awsS3Settings = awsS3Settings;
            _environmentSettings = environmentSettings;
            _logger = logger;
        }

        // Generate a presigned URL for uploading a file to S3
        public string GetPresignedUrl(string objectKey)
        {
            int presignedUrlTimeoutMinutes = 60; // 1 hour

            var request = new GetPreSignedUrlRequest
            {
                BucketName = ClientUploadBucket, // Use the dynamic bucket name
                Key = $"posts/{objectKey}", // Store files in the "posts" folder
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(presignedUrlTimeoutMinutes)
            };

            string preSignedUrl;

            // Initialize the AmazonS3Client with provided AWS credentials and region
            using (var awsAmazonS3 = new AmazonS3Client(
                _awsS3Settings.AccessKeyId,
                _awsS3Settings.SecretKey,
                new AmazonS3Config { RegionEndpoint = _awsS3Settings.RegionEndpoint }))
            {
                preSignedUrl = awsAmazonS3.GetPreSignedURL(request);
            }

            // Log the generated presigned URL for debugging purposes
            _logger.LogInformation("Generated presigned URL: {PresignedUrl}", preSignedUrl);

            return preSignedUrl;
        }

        // Dynamically construct the S3 bucket name using the environment's short name
        public string ClientUploadBucket => $"homepagecooking"; // Replace with dynamic generation if needed
    }
}
