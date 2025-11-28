using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace ARS.Services
{
    public class GmailEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GmailEmailService> _logger;

        public GmailEmailService(IConfiguration configuration, ILogger<GmailEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            Console.WriteLine($"[EMAIL DEBUG] SendAsync called - To: {to}, Subject: {subject}");
            
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var senderEmail = _configuration["Email:SenderEmail"];
                var senderPassword = _configuration["Email:SenderPassword"];
                var senderName = _configuration["Email:SenderName"] ?? "ARS Airlines";

                Console.WriteLine($"[EMAIL DEBUG] SMTP Config - Host: {smtpHost}, Port: {smtpPort}, From: {senderEmail}");

                if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                {
                    _logger.LogWarning("Email credentials not configured. Skipping email to {To}", to);
                    Console.WriteLine($"[EMAIL NOT SENT] Email credentials missing!");
                    return;
                }

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(senderEmail, senderPassword)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };

                mailMessage.To.Add(to);

                Console.WriteLine($"[EMAIL DEBUG] About to send email...");
                await smtpClient.SendMailAsync(mailMessage);
                
                _logger.LogInformation("Email sent successfully to {To}", to);
                Console.WriteLine($"[EMAIL SENT] ✓ Successfully sent to {to}, Subject: {subject}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
                Console.WriteLine($"[EMAIL ERROR] Failed to send to {to}: {ex.Message}");
                Console.WriteLine($"[EMAIL ERROR] Stack trace: {ex.StackTrace}");
            }
        }
    }
}
