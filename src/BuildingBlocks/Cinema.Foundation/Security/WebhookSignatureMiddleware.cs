using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cinema.Foundation.Security;

public class WebhookSignatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookSignatureMiddleware> _logger;

    public WebhookSignatureMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<WebhookSignatureMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var secret = _configuration["Webhooks:PaymentSecret"];
        if (env != "Development" && string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Production block: Webhooks:PaymentSecret must be configured.");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to webhook endpoints
        if (!context.Request.Path.StartsWithSegments("/api/v1/pos/payments/webhook"))
        {
            await _next(context);
            return;
        }

        var secret = _configuration["Webhooks:PaymentSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("Webhook secret not configured, skipping signature validation.");
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Signature", out var signatureHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing X-Signature header.");
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Timestamp", out var timestampHeader) || 
            !long.TryParse(timestampHeader, out var timestamp))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing or invalid X-Timestamp header.");
            return;
        }

        // Replay prevention: reject if older than 5 minutes or too far in the future
        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var now = DateTimeOffset.UtcNow;
        if (now - requestTime > TimeSpan.FromMinutes(5) || requestTime - now > TimeSpan.FromMinutes(1))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Webhook request timestamp is invalid (replay protection or clock drift).");
            return;
        }

        // Nonce check (Single-Print Constraint)
        if (context.Request.Headers.TryGetValue("X-Nonce", out var nonceHeader) && !string.IsNullOrWhiteSpace(nonceHeader))
        {
            var cache = context.RequestServices.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            if (cache != null)
            {
                var nonceKey = $"webhook:nonce:{nonceHeader}";
                var existing = await cache.GetStringAsync(nonceKey);
                if (existing != null)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Nonce already used.");
                    return;
                }
                await cache.SetStringAsync(nonceKey, "1", new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });
            }
        }

        // Read and hash body
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        // Support Single-Print request fingerprinting: timestamp + URI + body (or legacy timestamp + body)
        var payloadWithUri = $"{timestamp}.{context.Request.Path}.{body}";
        var payloadLegacy = $"{timestamp}.{body}";
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashWithUri = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadWithUri))).ToLowerInvariant();
        var hashLegacy = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadLegacy))).ToLowerInvariant();

        var providedSig = signatureHeader.ToString().ToLowerInvariant();
        var matchUri = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hashWithUri), Encoding.UTF8.GetBytes(providedSig));
        var matchLegacy = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hashLegacy), Encoding.UTF8.GetBytes(providedSig));

        if (!matchUri && !matchLegacy)
        {
            _logger.LogWarning("Invalid webhook signature.");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid signature.");
            return;
        }

        await _next(context);
    }
}
