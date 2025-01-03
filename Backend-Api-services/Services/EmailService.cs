using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Backend_Api_services.Services
{
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            _logger.LogInformation("Sending email to {ToEmail}", toEmail);

            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse("your eamil")); // Replace with your email
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = subject;

                var builder = new BodyBuilder
                {
                    HtmlBody = body
                };
                email.Body = builder.ToMessageBody();

                using (var smtp = new MailKit.Net.Smtp.SmtpClient()) // Fully qualified SmtpClient
                {
                    smtp.Connect("smtp.gmail.com", 587, false); // Replace with your SMTP server details
                    smtp.Authenticate("your eamil", "your app password"); // Replace with your email and app password

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

        public async Task SendVerificationEmailAsync(string toEmail, string verificationCode)
        {
            var subject = "Your Verification Code";
            string emailBody;

            try
            {
                // Load the HTML template
                var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "VerificationEmailTemplate.html");
                emailBody = await File.ReadAllTextAsync(templatePath);

                // Replace the placeholder with formatted verification code
                var formattedCode = string.Join("", verificationCode.Select(c =>
                    $"<td style='background-color: #F45F67; color: #fff; font-size: 28px; font-weight: bold; text-align: center; padding: 10px 15px; border-radius: 5px; margin: 0 5px;'>{c}</td>"
                ));

                // Replace placeholder
                emailBody = emailBody.Replace("{{CODE_PLACEHOLDER}}", formattedCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load or process the email template.");
                throw new InvalidOperationException("Could not generate the email body.", ex);
            }

            // Send the email
            await SendEmailAsync(toEmail, subject, emailBody);
        }
        public async Task SendResetPasswordEmailAsync(string toEmail, string resetCode)
        {
            var subject = "Password Reset Request";
            string emailBody;

            try
            {
                // Load the HTML template
                var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "ResetPasswordTemplate.html");
                emailBody = await File.ReadAllTextAsync(templatePath);

                // Replace the placeholder with the actual reset code
                emailBody = emailBody.Replace("{{RESET_CODE_PLACEHOLDER}}", resetCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load or process the email template.");
                throw new InvalidOperationException("Could not generate the email body.", ex);
            }

            // Send the email
            await SendEmailAsync(toEmail, subject, emailBody);
        }

    }
}
