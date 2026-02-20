using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace LP.Server.Services
{
    public interface IEmailService
    {
        Task SendConfirmationEmailAsync(string toEmail, string confirmationLink);
    }

    public class EmailService : IEmailService
    {
        private readonly SmtpConfig _config;

        public EmailService(IOptions<SmtpConfig> options)
        {
            _config = options.Value;
        }

        public async Task SendConfirmationEmailAsync(string toEmail, string confirmationLink)
        {
            using var client = new SmtpClient(_config.Host, _config.Port);
            client.Credentials = new NetworkCredential(_config.Username, _config.Password);
            client.EnableSsl = _config.EnableSsl;

            var message = new MailMessage
            {
                From = new MailAddress(_config.FromEmail, _config.FromName),
                Subject = "Подтверждение регистрации",
                IsBodyHtml = true,
                Body = $@"
                    <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <h2>Добро пожаловать!</h2>
                            <p>Для завершения регистрации перейдите по ссылке:</p>
                            <a href='{confirmationLink}' style='display: inline-block; padding: 10px 20px; background-color: #e91e63; color: white; text-decoration: none; border-radius: 5px;'>Подтвердить email</a>
                            <p>Если вы не регистрировались, проигнорируйте это письмо.</p>
                        </body>
                    </html>"
            };

            message.To.Add(toEmail);
            await client.SendMailAsync(message);
        }
    }

    public class SmtpConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
        public bool EnableSsl { get; set; }
    }
}