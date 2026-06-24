using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OCC.API.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IOptions<SmtpSettings> settings, ILogger<SmtpEmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toString, string subject, string body)
        {
            try
            {
                using var client = new SmtpClient(_settings.Host, _settings.Port)
                {
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                    EnableSsl = _settings.EnableSsl
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toString);

                _logger.LogInformation("Sending email to {To} with subject '{Subject}' via SMTP host {Host} on port {Port}...", toString, subject, _settings.Host, _settings.Port);
                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email successfully sent to {To}.", toString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To} via SMTP", toString);
                throw;
            }
        }
    }
}
