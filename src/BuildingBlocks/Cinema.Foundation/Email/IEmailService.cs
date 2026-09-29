namespace Cinema.Foundation.Email;

public interface IEmailService
{
    string ProviderName { get; }
    Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
