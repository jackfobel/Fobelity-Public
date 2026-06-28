//using DomainModels.Email.Interfaces;
//using System.Net;
//using System.Net.Mail;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;
//using DomainModels.Email.Models;

//namespace BackendServices.Helpers
//{
//  public class EmailService : IEmailService
//  {
//    private readonly EmailSettings _settings;
//    private readonly ILogger<EmailService> _logger;

//    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
//    {
//      _settings = settings.Value;
//      _logger = logger;
//    }

//    public async Task SendEmailAsync(string subject, string body, string? to = null)
//    {
//      try
//      {
//        using var client = new SmtpClient(_settings.SmtpServer, _settings.Port)
//        {
//          Credentials = new NetworkCredential(_settings.Username, _settings.Password),
//          EnableSsl = _settings.UseSsl
//        };

//        var mailMessage = new MailMessage
//        {
//          From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
//          Subject = subject,
//          Body = body,
//          IsBodyHtml = false
//        };

//        mailMessage.To.Add(to ?? _settings.DefaultRecipient);

//        await client.SendMailAsync(mailMessage);
//        _logger.LogInformation($"Email sent to {(to ?? _settings.DefaultRecipient)}: {subject}");
//      }
//      catch (Exception ex)
//      {
//        _logger.LogError(ex, "Failed to send email.");
//      }
//    }
//  }

//}
