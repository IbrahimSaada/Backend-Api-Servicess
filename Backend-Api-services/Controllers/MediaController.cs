using Microsoft.AspNetCore.Authorization;  // For JWT Authorization
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;  // For HMAC
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Backend_Api_services.Services.Interfaces;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]  // Require JWT authentication for the controller
    public class MediaController : ControllerBase
    {
        private readonly IAwsS3Service _awsS3Service;
        private readonly IConfiguration _configuration;

        public MediaController(IAwsS3Service awsS3Service, IConfiguration configuration)
        {
            _awsS3Service = awsS3Service;
            _configuration = configuration;
        }

        /// <summary>
        /// Get pre-signed URLs for direct upload from the client
        /// </summary>
        /// <returns>List of Pre-signed URLs, bucket, and object keys</returns>
        [HttpPost("s3-presigned-upload-urls")]
        [ResponseCache(NoStore = true, Duration = 0)]
        public async Task<ActionResult<List<object>>> GetS3PresignedUploadUrlsAsync([FromBody] Dictionary<string, string> payload)
        {
            // Validate the HMAC signature from the client
            var signature = Request.Headers["X-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature) || !ValidateSignature(signature, payload))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            if (payload == null || !payload.ContainsKey("fileNames") || !payload.ContainsKey("folderName"))
            {
                return BadRequest("Both fileNames and folderName fields are required.");
            }

            string[] fileNames = payload["fileNames"].Split(',');
            string folderName = payload["folderName"];

            if (string.IsNullOrEmpty(folderName))
            {
                return BadRequest("folderName cannot be empty.");
            }

            var presignedUrls = new List<object>();

            foreach (var fileName in fileNames)
            {
                string fileExtension = Path.GetExtension(fileName)?.ToLower();
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

                string urlFriendlyFileName = Regex.Replace(fileNameWithoutExtension, "[^A-Za-z0-9]", "-");
                string objectKey = $"{Guid.NewGuid()}-{urlFriendlyFileName}{fileExtension}";

                string presignedUrl = _awsS3Service.GetPresignedUrl(objectKey, folderName);

                presignedUrls.Add(new
                {
                    presignedUrl,
                    bucket = _awsS3Service.ClientUploadBucket,
                    objectKey
                });
            }

            return Ok(presignedUrls);
        }

        // Helper method to validate the HMAC signature
        private bool ValidateSignature(string receivedSignature, Dictionary<string, string> payload)
        {
            // Concatenate the data to sign
            string dataToSign = $"{payload["fileNames"]}:{payload["folderName"]}";

            // Retrieve the shared secret key from the configuration
            var secretKey = _configuration["AppSecretKey"];

            // Compute the HMAC-SHA256 signature
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
                var computedSignature = Convert.ToBase64String(computedHash);

                // Debug: Log the computed and received signatures for troubleshooting
                Console.WriteLine($"Computed Signature: {computedSignature}");
                Console.WriteLine($"Received Signature: {receivedSignature}");

                // Compare the computed signature with the received one
                return computedSignature == receivedSignature;
            }
        }
    }
}
