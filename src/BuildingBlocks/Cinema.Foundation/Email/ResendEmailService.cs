using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cinema.Foundation.Email;

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public string ProviderName => "Resend";

    public ResendEmailService(HttpClient httpClient, IOptions<EmailOptions> options, ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.Resend.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Resend API Key is not configured in Email:Resend:ApiKey. Email dispatch skipped.");
            return new EmailDispatchResult(false, null, "Resend API Key is not configured.");
        }

        try
        {
            var endpoint = string.IsNullOrWhiteSpace(_options.Resend.Endpoint) ? "https://api.resend.com/emails" : _options.Resend.Endpoint;

            object payload;
            if (message.Attachments != null && message.Attachments.Count > 0)
            {
                var attachments = message.Attachments.Select(a => new
                {
                    filename = a.FileName,
                    content = Convert.ToBase64String(a.Content)
                }).ToList();

                payload = new
                {
                    from = $"{_options.SenderName} <{_options.SenderEmail}>",
                    to = new[] { message.ToEmail },
                    subject = message.Subject,
                    html = message.HtmlBody,
                    text = message.PlainTextBody,
                    attachments
                };
            }
            else
            {
                payload = new
                {
                    from = $"{_options.SenderName} <{_options.SenderEmail}>",
                    to = new[] { message.ToEmail },
                    subject = message.Subject,
                    html = message.HtmlBody,
                    text = message.PlainTextBody
                };
            }

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Resend API returned error status {StatusCode}: {Response}", response.StatusCode, responseContent);
                return new EmailDispatchResult(false, null, $"Resend API error ({response.StatusCode}): {responseContent}");
            }

            using var doc = JsonDocument.Parse(responseContent);
            var messageId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : Guid.NewGuid().ToString();

            _logger.LogInformation("Successfully sent email via Resend API to {Recipient}. Resend MessageId: {MessageId}", message.ToEmail, messageId);
            return new EmailDispatchResult(true, messageId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via Resend API to {Recipient}", message.ToEmail);
            return new EmailDispatchResult(false, null, ex.Message);
        }
    }
}
