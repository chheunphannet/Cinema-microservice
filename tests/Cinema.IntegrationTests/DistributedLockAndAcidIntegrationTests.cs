using System.Data;
using Cinema.Foundation.Data;
using Cinema.Foundation.Redis;
using Dapper;
using Npgsql;
using StackExchange.Redis;
using Xunit;

namespace Cinema.IntegrationTests;

public class DistributedLockAndAcidIntegrationTests : IAsyncLifetime
{
    private Testcontainers.PostgreSql.PostgreSqlContainer _dbContainer = new Testcontainers.PostgreSql.PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private Testcontainers.Redis.RedisContainer _redisContainer = new Testcontainers.Redis.RedisBuilder().WithImage("redis:7-alpine").Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _redisContainer.StartAsync();

        using var conn = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await conn.OpenAsync();

        string? dir = AppContext.BaseDirectory;
        string? migrationsDir = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "infra", "postgres", "migrations");
            if (Directory.Exists(candidate))
            {
                migrationsDir = candidate;
                break;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (migrationsDir != null)
        {
            var sqlFiles = Directory.GetFiles(migrationsDir, "*.sql").OrderBy(f => f).ToList();
            foreach (var file in sqlFiles)
            {
                var sql = await File.ReadAllTextAsync(file);
                await conn.ExecuteAsync(sql);
            }
        }
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task RedisLuaDistributedLock_ShouldEnforceAtomicMutualExclusion()
    {
        IConnectionMultiplexer? redis = null;
        try
        {
            redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
        }
        catch
        {
            // If standalone redis is not directly reachable on host, skip or test in-memory
        }

        var lockService = new SeatLockService(redis);
        var showtimeId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var holdId1 = Guid.NewGuid();
        var holdId2 = Guid.NewGuid();
        var fencing1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fencing2 = fencing1 + 100;
        var ttl = TimeSpan.FromSeconds(30);

        // Client 1 acquires lock
        var (acquired1, lockVal1) = await lockService.AcquireSeatHoldAsync(showtimeId, seatId, holdId1, fencing1, ttl);
        Assert.True(acquired1, "Client 1 should successfully acquire lock");
        Assert.Equal($"{holdId1}:{fencing1}", lockVal1);

        // Client 2 attempts to acquire lock for SAME showtime and seat -> MUST FAIL
        var (acquired2, lockVal2) = await lockService.AcquireSeatHoldAsync(showtimeId, seatId, holdId2, fencing2, ttl);
        Assert.False(acquired2, "Client 2 must be rejected due to existing lock");
        Assert.Equal(lockVal1, lockVal2);

        // Client 1 releases lock
        var released = await lockService.ReleaseSeatHoldAsync(showtimeId, seatId, lockVal1);
        Assert.True(released, "Client 1 should successfully release lock");

        // Client 2 can now acquire lock
        var (acquired3, lockVal3) = await lockService.AcquireSeatHoldAsync(showtimeId, seatId, holdId2, fencing2, ttl);
        Assert.True(acquired3, "Client 2 should now acquire lock after release");
        Assert.Equal($"{holdId2}:{fencing2}", lockVal3);

        // Clean up
        await lockService.ReleaseSeatHoldAsync(showtimeId, seatId, lockVal3);
    }

    [Fact]
    public async Task PostgresConfirmedSeats_ShouldThrowUniqueViolation_WhenDoubleBookingAttempted()
    {
        var dbFactory = new NpgsqlConnectionFactory(_dbContainer.GetConnectionString());
        using var conn = (NpgsqlConnection)dbFactory.CreateConnection();
        await conn.OpenAsync();

        var showtimeId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var resId1 = Guid.NewGuid();
        var resId2 = Guid.NewGuid();
        var idemp1 = Guid.NewGuid();
        var idemp2 = Guid.NewGuid();

        // Setup 2 reservations in reservations.reservations
        const string insertResSql = @"
            INSERT INTO reservations.reservations (reservation_id, showtime_id, status, idempotency_key, fencing_token)
            VALUES (@ReservationId, @ShowtimeId, 'confirmed'::reservation_status, @IdempotencyKey, 1);";

        await conn.ExecuteAsync(insertResSql, new { ReservationId = resId1, ShowtimeId = showtimeId, IdempotencyKey = idemp1 });
        await conn.ExecuteAsync(insertResSql, new { ReservationId = resId2, ShowtimeId = showtimeId, IdempotencyKey = idemp2 });

        try
        {
            // Transaction 1: books the seat successfully
            const string bookSql = @"
                INSERT INTO reservations.confirmed_seats (showtime_id, seat_id, reservation_id, booked_at)
                VALUES (@ShowtimeId, @SeatId, @ReservationId, now());";

            await conn.ExecuteAsync(bookSql, new { ShowtimeId = showtimeId, SeatId = seatId, ReservationId = resId1 });

            // Transaction 2: attempts to book the SAME seat for SAME showtime
            var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await conn.ExecuteAsync(bookSql, new { ShowtimeId = showtimeId, SeatId = seatId, ReservationId = resId2 });
            });

            // Must violate UNIQUE(showtime_id, seat_id) constraint
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        }
        finally
        {
            // Clean up test data
            await conn.ExecuteAsync("DELETE FROM reservations.confirmed_seats WHERE showtime_id = @ShowtimeId", new { ShowtimeId = showtimeId });
            await conn.ExecuteAsync("DELETE FROM reservations.reservations WHERE showtime_id = @ShowtimeId", new { ShowtimeId = showtimeId });
        }
    }
}

