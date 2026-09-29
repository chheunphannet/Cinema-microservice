using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Cinema.Foundation.Security;

public interface IJwtTokenService
{
    string GenerateToken(Guid userId, string username, string displayName, string role, Guid? branchId, TimeSpan? expiry = null, IEnumerable<Claim>? extraClaims = null);
    ClaimsPrincipal? ValidateToken(string token);
}

public class JwtTokenService : IJwtTokenService
{
    public const string DefaultSecretKey = "CinemaPosSuperSecretDevelopmentJwtKey2026!Min32Bytes";
    public const string DefaultIssuer = "cinemapos";
    public const string DefaultAudience = "cinemapos-clients";

    private readonly byte[]? _keyBytes;
    private readonly RsaSecurityKey? _rsaKey;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenService(string? secretKey = null, string? issuer = null, string? audience = null, RsaSecurityKey? rsaKey = null)
    {
        if (rsaKey != null)
        {
            _rsaKey = rsaKey;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new ArgumentException("JWT Secret Key or RSA Key is required.", nameof(secretKey));
            _keyBytes = Encoding.UTF8.GetBytes(secretKey);
        }
        
        _issuer = string.IsNullOrWhiteSpace(issuer) ? DefaultIssuer : issuer;
        _audience = string.IsNullOrWhiteSpace(audience) ? DefaultAudience : audience;
    }

    public string GenerateToken(Guid userId, string username, string displayName, string role, Guid? branchId, TimeSpan? expiry = null, IEnumerable<Claim>? extraClaims = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var credentials = _rsaKey != null 
            ? new SigningCredentials(_rsaKey, SecurityAlgorithms.RsaSha256)
            : new SigningCredentials(new SymmetricSecurityKey(_keyBytes!), SecurityAlgorithms.HmacSha256Signature);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim("name", displayName),
            new Claim("email", username),
            new Claim("display_name", displayName),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role),
            new Claim("branch_id", branchId?.ToString() ?? string.Empty),
            new Claim("branchId", branchId?.ToString() ?? string.Empty)
        };

        if (extraClaims != null)
        {
            foreach (var extra in extraClaims)
            {
                if (!claims.Any(c => c.Type == extra.Type && c.Value == extra.Value))
                {
                    claims.Add(extra);
                }
            }
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(15)),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = credentials
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _rsaKey != null ? (SecurityKey)_rsaKey : new SymmetricSecurityKey(_keyBytes!),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
