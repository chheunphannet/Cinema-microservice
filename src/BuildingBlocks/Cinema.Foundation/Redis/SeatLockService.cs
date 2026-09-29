using System.Collections.Concurrent;
using StackExchange.Redis;

namespace Cinema.Foundation.Redis;

public interface ISeatLockService
{
    Task<(bool Success, string LockValue)> AcquireSeatHoldAsync(Guid showtimeId, Guid seatId, Guid holdId, long fencingToken, TimeSpan ttl);
    Task<bool> ReleaseSeatHoldAsync(Guid showtimeId, Guid seatId, string lockValue);
    Task<string?> GetSeatHoldAsync(Guid showtimeId, Guid seatId);
}

public class SeatLockService : ISeatLockService
{
    private readonly IConnectionMultiplexer? _redis;
    private static readonly ConcurrentDictionary<string, (string Value, DateTimeOffset ExpiresAt)> _memoryLocks = new();

    private const string AcquireLua = @"
        local current_owner = redis.call('GET', KEYS[1])
        if not current_owner or current_owner == ARGV[1] or string.sub(current_owner, 1, string.len(ARGV[1])) == ARGV[1] then
            local value = ARGV[1] .. ':' .. ARGV[2]
            redis.call('PSETEX', KEYS[1], ARGV[3], value)
            return {1, value}
        else
            return {0, current_owner}
        end
    ";

    private const string ReleaseLua = @"
        local current = redis.call('GET', KEYS[1])
        if current then
            if current == ARGV[1] or string.sub(current, 1, string.len(ARGV[1])) == ARGV[1] then
                return redis.call('DEL', KEYS[1])
            end
        end
        return 0
    ";

    public SeatLockService(IConnectionMultiplexer? redis = null)
    {
        _redis = redis;
    }

    private static string GetKey(Guid showtimeId, Guid seatId) => $"seat-hold:{showtimeId}:{seatId}";

    public async Task<(bool Success, string LockValue)> AcquireSeatHoldAsync(
        Guid showtimeId, Guid seatId, Guid holdId, long fencingToken, TimeSpan ttl)
    {
        var key = GetKey(showtimeId, seatId);
        var expectedVal = $"{holdId}:{fencingToken}";

        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var keys = new RedisKey[] { key };
            var args = new RedisValue[] { holdId.ToString(), fencingToken.ToString(), (long)ttl.TotalMilliseconds };
            
            var result = (RedisResult[]?)await db.ScriptEvaluateAsync(AcquireLua, keys, args);
            if (result != null && result.Length == 2)
            {
                var success = (int)result[0] == 1;
                var val = (string?)result[1] ?? "";
                return (success, val);
            }
            return (false, "EvaluationFailed");
        }

        // In-memory fallback if Redis multiplexer is not active
        var now = DateTimeOffset.UtcNow;
        if (_memoryLocks.TryGetValue(key, out var existing))
        {
            if (existing.ExpiresAt > now)
            {
                if (existing.Value.StartsWith(holdId.ToString()))
                {
                    _memoryLocks[key] = (expectedVal, now.Add(ttl));
                    return (true, expectedVal);
                }
                return (false, existing.Value);
            }
            _memoryLocks.TryRemove(key, out _);
        }

        var entry = (expectedVal, now.Add(ttl));
        if (_memoryLocks.TryAdd(key, entry))
        {
            return (true, expectedVal);
        }

        return (false, _memoryLocks.TryGetValue(key, out var val2) ? val2.Value : "Collision");
    }

    public async Task<bool> ReleaseSeatHoldAsync(Guid showtimeId, Guid seatId, string lockValue)
    {
        var key = GetKey(showtimeId, seatId);

        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var keys = new RedisKey[] { key };
            var args = new RedisValue[] { lockValue };
            var result = (int)await db.ScriptEvaluateAsync(ReleaseLua, keys, args);
            return result == 1;
        }

        if (_memoryLocks.TryGetValue(key, out var existing))
        {
            if (existing.Value == lockValue || existing.Value.StartsWith(lockValue, StringComparison.OrdinalIgnoreCase))
            {
                return _memoryLocks.TryRemove(key, out _);
            }
        }
        return false;
    }

    public async Task<string?> GetSeatHoldAsync(Guid showtimeId, Guid seatId)
    {
        var key = GetKey(showtimeId, seatId);
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            return await db.StringGetAsync(key);
        }

        if (_memoryLocks.TryGetValue(key, out var existing))
        {
            if (existing.ExpiresAt > DateTimeOffset.UtcNow)
                return existing.Value;
            _memoryLocks.TryRemove(key, out _);
        }
        return null;
    }
}
