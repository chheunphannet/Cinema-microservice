using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cinema.Foundation.Email;

public class AmazonSesEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<AmazonSesEmailService> _logger;

    public string ProviderName => "AmazonSes";

    public AmazonSesEmailService(IOptions<EmailOptions> options, ILogger<AmazonSesEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var ses = _options.AmazonSes;
        var region = string.IsNullOrWhiteSpace(ses.Region) ? "ap-southeast-1" : ses.Region;
        var host = $"email-smtp.{region}.amazonaws.com";

        try
        {
            using var mailMessage = new MailMessage();
            mailMessage.From = new MailAddress(_options.SenderEmail, _options.SenderName);
            mailMessage.To.Add(new MailAddress(message.ToEmail, message.ToName));
            mailMessage.Subject = message.Subject;
            mailMessage.SubjectEncoding = Encoding.UTF8;

            if (!string.IsNullOrWhiteSpace(message.PlainTextBody))
            {
                mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.PlainTextBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
            }

            mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));
            mailMessage.IsBodyHtml = true;

            if (message.Attachments != null)
            {
                foreach (var att in message.Attachments)
                {
                    mailMessage.Attachments.Add(new Attachment(new MemoryStream(att.Content), att.FileName, att.ContentType));
                }
            }

            using var client = new SmtpClient(host, 587);
            client.EnableSsl = true;
            if (!string.IsNullOrWhiteSpace(ses.AccessKey))
            {
                client.Credentials = new NetworkCredential(ses.AccessKey, ses.SecretKey);
            }

            await client.SendMailAsync(mailMessage, cancellationToken);
            var messageId = $"ses-{Guid.NewGuid():N}";
            _logger.LogInformation("Sent email via Amazon SES ({Region}) to {Recipient}", region, message.ToEmail);
            return new EmailDispatchResult(true, messageId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via Amazon SES to {Recipient}", message.ToEmail);
            return new EmailDispatchResult(false, null, ex.Message);
        }
    }
}
