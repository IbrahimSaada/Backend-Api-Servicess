using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Backend_Api_services.Services.Interfaces;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MediaController : ControllerBase
    {
        private readonly IAwsS3Service _awsS3Service;

        public MediaController(IAwsS3Service awsS3Service)
        {
            _awsS3Service = awsS3Service;
        }

        /// <summary>
        /// Get pre-signed URLs for direct upload from the client
        /// </summary>
        /// <returns>List of Pre-signed URLs, bucket, and object keys</returns>
        [HttpPost("s3-presigned-upload-urls")]
        [ResponseCache(NoStore = true, Duration = 0)]
        public async Task<ActionResult<List<object>>> GetS3PresignedUploadUrlsAsync([FromBody] Dictionary<string, string> payload)
        {
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
    }
}
