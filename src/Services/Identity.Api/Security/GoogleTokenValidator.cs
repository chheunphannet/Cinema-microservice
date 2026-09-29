using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Api.Security;

public sealed record GoogleTokenPayload(
    string Subject,
    string Email,
    string? Name,
    string? GivenName,
    string? FamilyName,
    string? Picture,
    bool EmailVerified
);

public interface IGoogleTokenValidator
{
    Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleTokenValidator> _logger;
    private readonly string? _clientId;
    private readonly bool _isMockMode;

    public GoogleTokenValidator(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GoogleTokenValidator> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _clientId = configuration["GoogleAuth:ClientId"];

        // Dev / mock mode if ClientId is not provided, empty, or explicitly set to dev/mock/test
        _isMockMode = string.IsNullOrWhiteSpace(_clientId) ||
                      _clientId.Equals("mock", StringComparison.OrdinalIgnoreCase) ||
                      _clientId.Equals("dev", StringComparison.OrdinalIgnoreCase) ||
                      _clientId.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                      _clientId.Equals("dev-mock", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        idToken = idToken.Trim();

        // Check if we are in mock/dev mode.
        // SECURITY CRITICAL: Mock token bypass must NEVER be allowed in live production mode (_isMockMode == false).
        if (_isMockMode)
        {
            return ValidateMockToken(idToken);
        }

        // Live Google Token Validation using Google Tokeninfo endpoint
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            var url = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}";
            var response = await _httpClient.GetAsync(url, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google tokeninfo validation failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeoutCts.Token), cancellationToken: timeoutCts.Token);
            var root = doc.RootElement;

            // Check audience matches our client ID if configured
            if (!string.IsNullOrWhiteSpace(_clientId) && root.TryGetProperty("aud", out var audElement))
            {
                var aud = audElement.GetString();
                bool audMatch = string.Equals(aud, _clientId, StringComparison.Ordinal);
                if (!audMatch && root.TryGetProperty("azp", out var azpElement))
                {
                    audMatch = string.Equals(azpElement.GetString(), _clientId, StringComparison.Ordinal);
                }

                if (!audMatch)
                {
                    _logger.LogWarning("Google token audience mismatch: expected {Expected}, got {Actual}", _clientId, aud);
                    return null;
                }
            }

            if (!root.TryGetProperty("sub", out var subElement) || !root.TryGetProperty("email", out var emailElement))
            {
                return null;
            }

            var sub = subElement.GetString();
            var email = emailElement.GetString();
            if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            var givenName = root.TryGetProperty("given_name", out var gn) ? gn.GetString() : null;
            var familyName = root.TryGetProperty("family_name", out var fn) ? fn.GetString() : null;
            var picture = root.TryGetProperty("picture", out var pic) ? pic.GetString() : null;

            bool emailVerified = false;
            if (root.TryGetProperty("email_verified", out var ev))
            {
                if (ev.ValueKind == JsonValueKind.True) emailVerified = true;
                else if (ev.ValueKind == JsonValueKind.String) bool.TryParse(ev.GetString(), out emailVerified);
            }

            return new GoogleTokenPayload(sub, email, name, givenName, familyName, picture, emailVerified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during live Google token validation");
            return null;
        }
    }

    private GoogleTokenPayload? ValidateMockToken(string token)
    {
        try
        {
            // 1. Try reading as standard JWT
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwt = handler.ReadJwtToken(token);

                // Check token expiration
                if (jwt.ValidTo != DateTime.MinValue && jwt.ValidTo < DateTime.UtcNow)
                {
                    _logger.LogWarning("Mock Google JWT token has expired at {ValidTo}", jwt.ValidTo);
                    return null;
                }

                var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;
                var email = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email || c.Type == ClaimTypes.Email || c.Type == "email")?.Value;

                if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email))
                {
                    return null;
                }

                var name = jwt.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value;
                var givenName = jwt.Claims.FirstOrDefault(c => c.Type == "given_name" || c.Type == ClaimTypes.GivenName)?.Value;
                var familyName = jwt.Claims.FirstOrDefault(c => c.Type == "family_name" || c.Type == ClaimTypes.Surname)?.Value;
                var picture = jwt.Claims.FirstOrDefault(c => c.Type == "picture")?.Value;

                var emailVerifiedClaim = jwt.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value;
                bool emailVerified = true;
                if (!string.IsNullOrWhiteSpace(emailVerifiedClaim))
                {
                    _ = bool.TryParse(emailVerifiedClaim, out emailVerified);
                }

                return new GoogleTokenPayload(sub, email, name, givenName, familyName, picture, emailVerified);
            }

            // 2. Try reading as JSON string
            var trimmed = token.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                // Check expiry if specified in JSON
                if (root.TryGetProperty("exp", out var expElement) && expElement.TryGetInt64(out var expUnix))
                {
                    var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                    if (expDate < DateTimeOffset.UtcNow)
                    {
                        _logger.LogWarning("Mock JSON Google token has expired at {ExpDate}", expDate);
                        return null;
                    }
                }

                if (root.TryGetProperty("sub", out var subEl) && root.TryGetProperty("email", out var emailEl))
                {
                    var sub = subEl.GetString();
                    var email = emailEl.GetString();
                    if (!string.IsNullOrWhiteSpace(sub) && !string.IsNullOrWhiteSpace(email))
                    {
                        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
                        var givenName = root.TryGetProperty("given_name", out var gn) ? gn.GetString() : null;
                        var familyName = root.TryGetProperty("family_name", out var fn) ? fn.GetString() : null;
                        var picture = root.TryGetProperty("picture", out var p) ? p.GetString() : null;
                        bool emailVerified = true;
                        if (root.TryGetProperty("email_verified", out var ev))
                        {
                            if (ev.ValueKind == JsonValueKind.True) emailVerified = true;
                            else if (ev.ValueKind == JsonValueKind.False) emailVerified = false;
                            else if (ev.ValueKind == JsonValueKind.String) bool.TryParse(ev.GetString(), out emailVerified);
                        }
                        return new GoogleTokenPayload(sub, email, name, givenName, familyName, picture, emailVerified);
                    }
                }
            }

            // 3. Try reading as formatted string mock-google-id|email|name|picture|emailVerified
            if (token.StartsWith("mock-", StringComparison.OrdinalIgnoreCase) || token.StartsWith("test-", StringComparison.OrdinalIgnoreCase))
            {
                var parts = token.Split('|');
                if (parts.Length >= 2)
                {
                    var sub = parts[0];
                    var email = parts[1];
                    var name = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2] : null;
                    var picture = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : null;
                    bool emailVerified = true;
                    if (parts.Length > 4 && bool.TryParse(parts[4], out var ev))
                    {
                        emailVerified = ev;
                    }
                    return new GoogleTokenPayload(sub, email, name, null, null, picture, emailVerified);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse mock Google token");
            return null;
        }
    }
}
