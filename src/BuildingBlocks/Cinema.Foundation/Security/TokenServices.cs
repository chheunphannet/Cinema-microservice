using System.Security.Cryptography;
using Cinema.Foundation.Data;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;

namespace Cinema.Foundation.Security;

public interface ITokenRevocationService
{
    /// <summary>Blacklist an access token by its JTI (JWT ID). TTL = remaining token lifetime.</summary>
    Task RevokeAccessTokenAsync(string jti, TimeSpan remainingLifetime);
    
    /// <summary>Check if an access token JTI has been revoked.</summary>
    Task<bool> IsRevokedAsync(string jti);
}

public interface IRefreshTokenService
{
    /// <summary>Generate and store a new refresh token for a user.</summary>
    Task<string> GenerateRefreshTokenAsync(Guid userId, string username, string role, Guid? branchId);
    
    /// <summary>Validate and rotate a refresh token. Returns null if invalid/expired/revoked.</summary>
    Task<RefreshTokenResult?> RotateRefreshTokenAsync(string refreshToken);
    
    /// <summary>Revoke all refresh tokens for a user (e.g., on logout or password change).</summary>
    Task RevokeAllUserTokensAsync(Guid userId);
}

public sealed record RefreshTokenResult(Guid UserId, string Username, string Role, Guid? BranchId);

public class TokenRevocationService : ITokenRevocationService
{
    private readonly IDistributedCache _cache;
    private const string Prefix = "revoked:jti:";

    public TokenRevocationService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task RevokeAccessTokenAsync(string jti, TimeSpan remainingLifetime)
    {
        if (remainingLifetime <= TimeSpan.Zero) return;
        await _cache.SetStringAsync($"{Prefix}{jti}", "1", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = remainingLifetime
        });
    }

    public async Task<bool> IsRevokedAsync(string jti)
    {
        var val = await _cache.GetStringAsync($"{Prefix}{jti}");
        return val != null;
    }
}

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IDbConnectionFactory _dbFactory;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public RefreshTokenService(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<string> GenerateRefreshTokenAsync(Guid userId, string username, string role, Guid? branchId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = HashToken(token);

        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            INSERT INTO identity.refresh_tokens (token_hash, user_id, username, role, branch_id, expires_at, created_at, is_revoked)
            VALUES (@TokenHash, @UserId, @Username, @Role, @BranchId, @ExpiresAt, now(), false)";

        await conn.ExecuteAsync(sql, new
        {
            TokenHash = tokenHash,
            UserId = userId,
            Username = username,
            Role = role,
            BranchId = branchId,
            ExpiresAt = DateTimeOffset.UtcNow.Add(RefreshTokenLifetime)
        });

        return token;
    }

    public async Task<RefreshTokenResult?> RotateRefreshTokenAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);

        using var conn = _dbFactory.CreateConnection();
        const string sql = @"
            UPDATE identity.refresh_tokens 
            SET is_revoked = true, revoked_at = now()
            WHERE token_hash = @TokenHash AND is_revoked = false AND expires_at > now()
            RETURNING user_id, username, role, branch_id";

        var row = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { TokenHash = tokenHash });
        if (row == null) return null;

        return new RefreshTokenResult(
            (Guid)row.user_id,
            (string)row.username,
            (string)row.role,
            (Guid?)row.branch_id
        );
    }

    public async Task RevokeAllUserTokensAsync(Guid userId)
    {
        using var conn = _dbFactory.CreateConnection();
        const string sql = "UPDATE identity.refresh_tokens SET is_revoked = true, revoked_at = now() WHERE user_id = @UserId AND is_revoked = false";
        await conn.ExecuteAsync(sql, new { UserId = userId });
    }

    private static string HashToken(string token)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
