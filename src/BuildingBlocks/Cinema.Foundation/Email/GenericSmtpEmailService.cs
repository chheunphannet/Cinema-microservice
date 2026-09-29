using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cinema.Foundation.Email;

public class GenericSmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<GenericSmtpEmailService> _logger;

    public string ProviderName => "Smtp";

    public GenericSmtpEmailService(IOptions<EmailOptions> options, ILogger<GenericSmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var smtpConfig = _options.Smtp;
        if (string.IsNullOrWhiteSpace(smtpConfig.Host))
        {
            return new EmailDispatchResult(false, null, "SMTP host is not configured.");
        }

        try
        {
            using var mailMessage = new MailMessage();
            mailMessage.From = new MailAddress(_options.SenderEmail, _options.SenderName);
            mailMessage.To.Add(new MailAddress(message.ToEmail, message.ToName));
            mailMessage.Subject = message.Subject;
            mailMessage.SubjectEncoding = Encoding.UTF8;

            if (!string.IsNullOrWhiteSpace(message.PlainTextBody))
            {
                var plainView = AlternateView.CreateAlternateViewFromString(message.PlainTextBody, Encoding.UTF8, MediaTypeNames.Text.Plain);
                mailMessage.AlternateViews.Add(plainView);
            }

            var htmlView = AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html);
            mailMessage.AlternateViews.Add(htmlView);
            mailMessage.IsBodyHtml = true;

            if (message.Attachments != null)
            {
                foreach (var att in message.Attachments)
                {
                    mailMessage.Attachments.Add(new Attachment(new MemoryStream(att.Content), att.FileName, att.ContentType));
                }
            }

            using var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port);
            client.EnableSsl = smtpConfig.EnableSsl;
            if (!string.IsNullOrWhiteSpace(smtpConfig.Username))
            {
                client.Credentials = new NetworkCredential(smtpConfig.Username, smtpConfig.Password);
            }

            await client.SendMailAsync(mailMessage, cancellationToken);
            var messageId = $"smtp-{Guid.NewGuid():N}";
            _logger.LogInformation("Sent email via Generic SMTP to {Recipient}. Subject: {Subject}", message.ToEmail, message.Subject);
            return new EmailDispatchResult(true, messageId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SMTP to {Recipient}", message.ToEmail);
            return new EmailDispatchResult(false, null, ex.Message);
        }
    }
}
