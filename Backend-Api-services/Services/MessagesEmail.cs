using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Backend_Api_services.Services
{
    public class MessagesEmail
    {
        private readonly ILogger<MessagesEmail> _logger;

        public MessagesEmail(ILogger<MessagesEmail> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Sends an email using a template with placeholders, inline images, and attachments.
        /// </summary>
        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string templatePath,
            Dictionary<string, string>? placeholders = null,
            List<string>? inlineImages = null,
            List<string>? attachmentPaths = null)
        {
            _logger.LogInformation("Sending email to {ToEmail}", toEmail);

            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse("your eamil"));
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = subject;

                var builder = new BodyBuilder();

                // Load and process HTML template
                if (!File.Exists(templatePath))
                {
                    throw new FileNotFoundException("Email template not found.", templatePath);
                }

                string emailBody = await File.ReadAllTextAsync(templatePath);

                // Replace placeholders
                if (placeholders != null)
                {
                    foreach (var placeholder in placeholders)
                    {
                        emailBody = emailBody.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value);
                    }
                }

                // Embed inline images
                if (inlineImages != null)
                {
                    foreach (var imagePath in inlineImages)
                    {
                        if (File.Exists(imagePath))
                        {
                            var image = builder.LinkedResources.Add(imagePath);
                            image.ContentId = Path.GetFileNameWithoutExtension(imagePath);
                            image.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                        }
                        else
                        {
                            _logger.LogWarning("Inline image file not found: {ImagePath}", imagePath);
                        }
                    }
                }

                builder.HtmlBody = emailBody;

                // Add attachments
                if (attachmentPaths != null)
                {
                    foreach (var path in attachmentPaths)
                    {
                        try
                        {
                            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                            {
                                var localPath = await DownloadFileAsync(path);
                                builder.Attachments.Add(localPath);
                            }
                            else if (File.Exists(path))
                            {
                                builder.Attachments.Add(path);
                            }
                            else
                            {
                                _logger.LogWarning("Attachment file not found: {Path}", path);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to attach file: {Path}", path);
                        }
                    }
                }

                email.Body = builder.ToMessageBody();

                using (var smtp = new SmtpClient())
                {
                    smtp.Connect("smtp.gmail.com", 587, false);
                    smtp.Authenticate("your eamil", "your app password");
                    await smtp.SendAsync(email);
                    smtp.Disconnect(true);
                }

                _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while sending email to {ToEmail}", toEmail);
            }
        }

        /// <summary>
        /// Overload for simple email sending without placeholders or inline images.
        /// </summary>
        public async Task SendEmailAsync(string toEmail, string subject, string body, List<string>? attachmentPaths = null)
        {
            var placeholders = new Dictionary<string, string>
            {
                { "SUBJECT", subject },
                { "BODY", body }
            };

            await SendEmailAsync(
                toEmail: toEmail,
                subject: subject,
                templatePath: "./Templates/EmailTemplate.html",
                placeholders: placeholders,
                attachmentPaths: attachmentPaths
            );
        }

        /// <summary>
        /// Downloads a file from a URL.
        /// </summary>
        private async Task<string> DownloadFileAsync(string url)
        {
            using var httpClient = new HttpClient();
            var fileName = Path.GetFileName(new Uri(url).LocalPath);
            var localFilePath = Path.Combine(Path.GetTempPath(), fileName);

            using (var response = await httpClient.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();
                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(localFilePath, fileBytes);
            }

            _logger.LogInformation("File downloaded to {LocalPath}", localFilePath);
            return localFilePath;
        }
    }
}
