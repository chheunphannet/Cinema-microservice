using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cinema.Foundation.Email;

public class MailpitEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailpitEmailService> _logger;

    public string ProviderName => "Mailpit";

    public MailpitEmailService(IOptions<EmailOptions> options, ILogger<MailpitEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mailpitHost = string.IsNullOrWhiteSpace(_options.Mailpit.Host) ? "mailpit" : _options.Mailpit.Host;
        var mailpitPort = _options.Mailpit.Port > 0 ? _options.Mailpit.Port : 1025;

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

            if (message.Attachments != null && message.Attachments.Count > 0)
            {
                foreach (var att in message.Attachments)
                {
                    var ms = new MemoryStream(att.Content);
                    var attachment = new Attachment(ms, att.FileName, att.ContentType);
                    mailMessage.Attachments.Add(attachment);
                }
            }

            using var smtp = new SmtpClient(mailpitHost, mailpitPort);
            smtp.EnableSsl = false;
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtp.Timeout = 10000;

            await smtp.SendMailAsync(mailMessage, cancellationToken);

            var messageId = $"mailpit-{Guid.NewGuid():N}";
            _logger.LogInformation("Successfully dispatched email to Mailpit at {Host}:{Port} for recipient {Recipient}. Subject: {Subject}, MessageId: {MessageId}",
                mailpitHost, mailpitPort, message.ToEmail, message.Subject, messageId);

            return new EmailDispatchResult(true, messageId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch email via Mailpit at {Host}:{Port} for recipient {Recipient}",
                mailpitHost, mailpitPort, message.ToEmail);
            return new EmailDispatchResult(false, null, ex.Message);
        }
    }
}
