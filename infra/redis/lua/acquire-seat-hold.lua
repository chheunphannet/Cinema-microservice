-- KEYS[1] = seat-hold:{showtimeId}:{seatId}; ARGV[1] = holdId/userId; ARGV[2] = fencing token; ARGV[3] = TTL milliseconds
local current_owner = redis.call('GET', KEYS[1])
if not current_owner or current_owner == ARGV[1] or string.sub(current_owner, 1, string.len(ARGV[1])) == ARGV[1] then
    local value = ARGV[1] .. ':' .. ARGV[2]
    redis.call('PSETEX', KEYS[1], ARGV[3], value)
    return {1, value}
else
    return {0, current_owner}
end
