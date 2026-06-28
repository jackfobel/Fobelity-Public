using System.Threading.Tasks;
using Azure.Security.KeyVault.Secrets;
using DomainModels.Email.Interfaces;
using DomainModels.Email.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BackendServices.Helpers
{
  public class MailKitService : IEmailService
  {
    private readonly EmailSettings _settings;
    private readonly ILogger<MailKitService> _logger;
    private readonly SecretClient _secretClient;
    private string? _cachedPassword;

    public MailKitService(
      IOptions<EmailSettings> options,
      ILogger<MailKitService> logger,
      SecretClient secretClient)
    {
      _settings = options.Value;
      _logger = logger;
      _secretClient = secretClient;
    }

    public async Task SendEmailAsync(string subject, string body, string? to = null)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(_cachedPassword))
        {
          _logger.LogInformation("ℹ️ Retrieving smtp-pass from Key Vault...");
          var secret = await _secretClient.GetSecretAsync("smtp-pass");
          _cachedPassword = secret.Value.Value;
        }

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        email.To.Add(MailboxAddress.Parse(to ?? _settings.DefaultRecipient));
        email.Subject = subject;

        // Compose HTML and plain text body
        var plainText = "Your email client does not support HTML content.";
        var htmlBody = body; // assumes `body` is HTML when passed in

        var builder = new BodyBuilder
        {
          HtmlBody = htmlBody,
          TextBody = plainText
        };

        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_settings.SmtpServer, _settings.Port,
            _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

        await smtp.AuthenticateAsync(_settings.Username, _cachedPassword);
        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);

        _logger.LogInformation("ℹ️ Email sent successfully.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "❌ Failed to send email via MailKit");
      }
    }



  }
}
