using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Cinema.Foundation.Security;

public static class SignedTicketUrlService
{
    public const string DefaultSecretKey = "super-secret-cinema-eticket-hmac-key-2026-min32chars!";
    public const string DefaultPublicGatewayUrl = "http://localhost:8080";

    public static string GetSecretKey(IConfiguration config)
    {
        var secret = config["Tickets:HmacSecret"] ?? config["Jwt:Secret"];
        
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (env != "Development")
        {
            if (string.IsNullOrWhiteSpace(secret) || secret == DefaultSecretKey)
            {
                throw new InvalidOperationException("Production block: Tickets:HmacSecret must be explicitly configured and cannot be the default value.");
            }
        }
        
        return secret ?? DefaultSecretKey;
    }

    public static string GetPublicGatewayUrl(IConfiguration config)
    {
        var url = config["Gateway:PublicUrl"] 
               ?? config["Services:GatewayUrl"] 
               ?? DefaultPublicGatewayUrl;
        return url.TrimEnd('/');
    }

    public static string GenerateSignature(Guid ticketId, long expiresAtUnix, string secretKey)
    {
        var effectiveKey = string.IsNullOrWhiteSpace(secretKey) ? DefaultSecretKey : secretKey;
        var payload = $"{ticketId:D}:{expiresAtUnix}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(effectiveKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool ValidateSignature(Guid ticketId, long expiresAtUnix, string? signature, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nowUnix > expiresAtUnix)
        {
            return false; // Expired ticket pass link
        }

        var expectedSignature = GenerateSignature(ticketId, expiresAtUnix, secretKey);
        
        try
        {
            var cleanSig = signature.Trim().ToLowerInvariant();
            if (cleanSig.StartsWith("hmac-sha256("))
            {
                cleanSig = cleanSig.Replace("hmac-sha256(", "").TrimEnd(')');
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(cleanSig),
                Encoding.UTF8.GetBytes(expectedSignature)
            );
        }
        catch
        {
            return false;
        }
    }

    public static string BuildSignedETicketUrl(string baseUrl, Guid ticketId, TimeSpan validity, string secretKey)
    {
        var expiresAtUnix = DateTimeOffset.UtcNow.Add(validity).ToUnixTimeSeconds();
        var signature = GenerateSignature(ticketId, expiresAtUnix, secretKey);
        var cleanBase = (string.IsNullOrWhiteSpace(baseUrl) ? DefaultPublicGatewayUrl : baseUrl).TrimEnd('/');
        return $"{cleanBase}/api/v1/tickets/e-ticket/{ticketId}?exp={expiresAtUnix}&sig={signature}";
    }

    public static string BuildSignedETicketUrl(IConfiguration config, Guid ticketId, TimeSpan? validity = null)
    {
        var baseUrl = GetPublicGatewayUrl(config);
        var secretKey = GetSecretKey(config);
        return BuildSignedETicketUrl(baseUrl, ticketId, validity ?? TimeSpan.FromDays(7), secretKey);
    }

    public static string GenerateReservationSignature(Guid reservationId, long expiresAtUnix, string secretKey)
    {
        var effectiveKey = string.IsNullOrWhiteSpace(secretKey) ? DefaultSecretKey : secretKey;
        var payload = $"RES:{reservationId:D}:{expiresAtUnix}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(effectiveKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool ValidateReservationSignature(Guid reservationId, long expiresAtUnix, string? signature, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nowUnix > expiresAtUnix)
        {
            return false;
        }

        var expectedSignature = GenerateReservationSignature(reservationId, expiresAtUnix, secretKey);
        try
        {
            var cleanSig = signature.Trim().ToLowerInvariant();
            if (cleanSig.StartsWith("hmac-sha256("))
            {
                cleanSig = cleanSig.Replace("hmac-sha256(", "").TrimEnd(')');
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(cleanSig),
                Encoding.UTF8.GetBytes(expectedSignature)
            );
        }
        catch
        {
            return false;
        }
    }

    public static string BuildSignedReservationUrl(string baseUrl, Guid reservationId, TimeSpan validity, string secretKey)
    {
        var expiresAtUnix = DateTimeOffset.UtcNow.Add(validity).ToUnixTimeSeconds();
        var signature = GenerateReservationSignature(reservationId, expiresAtUnix, secretKey);
        var cleanBase = (string.IsNullOrWhiteSpace(baseUrl) ? DefaultPublicGatewayUrl : baseUrl).TrimEnd('/');
        return $"{cleanBase}/api/v1/tickets/reservation/{reservationId}?exp={expiresAtUnix}&sig={signature}";
    }

    public static string BuildSignedReservationUrl(IConfiguration config, Guid reservationId, TimeSpan? validity = null)
    {
        var baseUrl = GetPublicGatewayUrl(config);
        var secretKey = GetSecretKey(config);
        return BuildSignedReservationUrl(baseUrl, reservationId, validity ?? TimeSpan.FromDays(7), secretKey);
    }
}

