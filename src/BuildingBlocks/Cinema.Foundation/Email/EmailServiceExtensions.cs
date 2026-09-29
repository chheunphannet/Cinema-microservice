using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.Foundation.Email;

public static class EmailServiceExtensions
{
    public static IServiceCollection AddCinemaEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var emailSection = configuration.GetSection(EmailOptions.SectionName);
        services.Configure<EmailOptions>(emailSection);

        var provider = emailSection["Provider"]?.Trim();
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = "Mailpit";
        }

        switch (provider.ToLowerInvariant())
        {
            case "mailpit":
                services.AddTransient<IEmailService, MailpitEmailService>();
                break;

            case "resend":
                services.AddHttpClient<IEmailService, ResendEmailService>();
                break;

            case "amazonses":
                services.AddTransient<IEmailService, AmazonSesEmailService>();
                break;

            case "smtp":
                services.AddTransient<IEmailService, GenericSmtpEmailService>();
                break;

            default:
                // Fallback to Mailpit for local safety
                services.AddTransient<IEmailService, MailpitEmailService>();
                break;
        }

        return services;
    }
}
