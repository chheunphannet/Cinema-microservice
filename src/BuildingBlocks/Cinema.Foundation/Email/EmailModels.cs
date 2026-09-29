namespace Cinema.Foundation.Email;

public record EmailAttachment(string FileName, byte[] Content, string ContentType);

public record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null,
    List<EmailAttachment>? Attachments = null
);

public record EmailDispatchResult(
    bool Success, 
    string? MessageId = null, 
    string? ErrorMessage = null
);

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Mailpit"; // "Mailpit" | "Resend" | "AmazonSes" | "Smtp"
    public string SenderEmail { get; set; } = "tickets@cinemacity.local";
    public string SenderName { get; set; } = "Cinema City Box Office";

    public MailpitOptions Mailpit { get; set; } = new();
    public ResendOptions Resend { get; set; } = new();
    public AmazonSesOptions AmazonSes { get; set; } = new();
    public SmtpOptions Smtp { get; set; } = new();
}

public class MailpitOptions
{
    public string Host { get; set; } = "mailpit";
    public int Port { get; set; } = 1025;
}

public class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api.resend.com/emails";
}

public class AmazonSesOptions
{
    public string Region { get; set; } = "ap-southeast-1";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}

public class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
}
