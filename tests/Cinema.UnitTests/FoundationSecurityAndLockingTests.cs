using System.Security.Claims;
using Cinema.Foundation.Redis;
using Cinema.Foundation.Security;
using Xunit;

namespace Cinema.UnitTests;

public class FoundationSecurityAndLockingTests
{
    [Fact]
    public void PasswordHasher_ShouldCorrectlyHashAndVerify()
    {
        const string pin = "1234";
        var hash = PasswordHasher.Hash(pin);

        Assert.NotNull(hash);
        Assert.True(hash.Length >= 64); // PBKDF2 iterations.salt.hash format

        // Valid pin matches
        Assert.True(PasswordHasher.Verify("1234", hash));

        // Invalid pin fails
        Assert.False(PasswordHasher.Verify("9999", hash));
        Assert.False(PasswordHasher.Verify("", hash));
    }

    [Fact]
    public void PasswordHasher_ShouldMatchDatabaseSeedHashes()
    {
        // Verified against infra/postgres/init.sql seeds
        const string cashierPin = "1234";
        const string cashierExpectedHash = "03ac674216f3e15c761ee1a5e255f067953623c8b388b4459e13f978d7c846f4";
        Assert.True(PasswordHasher.Verify(cashierPin, cashierExpectedHash));

        const string supervisorPin = "9999";
        const string supervisorExpectedHash = "888df25ae35772424a560c7152a1de794440e0ea5cfee62828333a456a506e05";
        Assert.True(PasswordHasher.Verify(supervisorPin, supervisorExpectedHash));
    }

    [Fact]
    public void JwtTokenService_ShouldGenerateAndValidateTokenWithClaims()
    {
        var jwtService = new JwtTokenService(JwtTokenService.DefaultSecretKey);
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        const string username = "cashier1";
        const string displayName = "Lead Cashier";
        const string role = "cashier";

        var token = jwtService.GenerateToken(userId, username, displayName, role, branchId);
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var principal = jwtService.ValidateToken(token);
        Assert.NotNull(principal);

        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var nameClaim = principal.FindFirst(ClaimTypes.Name)?.Value;
        var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;
        var branchClaim = principal.FindFirst("branch_id")?.Value;

        Assert.Equal(userId.ToString(), subClaim);
        Assert.Equal(username, nameClaim);
        Assert.Equal(role, roleClaim);
        Assert.Equal(branchId.ToString(), branchClaim);
    }

    [Fact]
    public void JwtTokenService_ShouldRejectTamperedToken()
    {
        var jwtService = new JwtTokenService(JwtTokenService.DefaultSecretKey);
        var token = jwtService.GenerateToken(Guid.NewGuid(), "user", "User", "cashier", null);

        // Tamper with payload
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        var tamperedToken = $"{parts[0]}.{parts[1]}tampered.{parts[2]}";
        var result = jwtService.ValidateToken(tamperedToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task SeatLockService_ShouldAcquireAndDetectCollision()
    {
        var lockService = new SeatLockService(redis: null); // Test in-memory fallback
        var showtimeId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var holdId1 = Guid.NewGuid();
        var holdId2 = Guid.NewGuid();
        var fencingToken1 = 1000L;
        var fencingToken2 = 2000L;
        var ttl = TimeSpan.FromMinutes(10);

        // First hold succeeds
        var (success1, val1) = await lockService.AcquireSeatHoldAsync(showtimeId, seatId, holdId1, fencingToken1, ttl);
        Assert.True(success1);
        Assert.Equal($"{holdId1}:{fencingToken1}", val1);

        // Second hold for SAME showtime and seat must COLLIDE and fail
        var (success2, val2) = await lockService.AcquireSeatHoldAsync(showtimeId, seatId, holdId2, fencingToken2, ttl);
        Assert.False(success2);
        Assert.Equal(val1, val2);

        // Releasing with wrong hold ID fails
        var wrongRelease = await lockService.ReleaseSeatHoldAsync(showtimeId, seatId, "wrong-hold-id");
        Assert.False(wrongRelease);

        // Releasing with correct hold ID succeeds
        var validRelease = await lockService.ReleaseSeatHoldAsync(showtimeId, seatId, holdId1.ToString());
        Assert.True(validRelease);

        // Now seat can be acquired again
        var (success3, val3) = await lockService.AcquireSeatHoldAsync(showtimeId, seatId, holdId2, fencingToken2, ttl);
        Assert.True(success3);
        Assert.Equal($"{holdId2}:{fencingToken2}", val3);

        // Get current hold
        var currentHold = await lockService.GetSeatHoldAsync(showtimeId, seatId);
        Assert.Equal($"{holdId2}:{fencingToken2}", currentHold);

        // Release hold
        await lockService.ReleaseSeatHoldAsync(showtimeId, seatId, holdId2.ToString());
        var releasedHold = await lockService.GetSeatHoldAsync(showtimeId, seatId);
        Assert.Null(releasedHold);

        // Expired hold test
        var (expSuccess, expVal) = await lockService.AcquireSeatHoldAsync(showtimeId, seatId, Guid.NewGuid(), 3000L, TimeSpan.FromMilliseconds(1));
        Assert.True(expSuccess);
        await Task.Delay(20);
        var expiredVal = await lockService.GetSeatHoldAsync(showtimeId, seatId);
        Assert.Null(expiredVal);
    }
}
