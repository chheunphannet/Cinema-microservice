-- Delete only when this request owns the hold (exact match or holdId prefix).
local current = redis.call('GET', KEYS[1])
if current and (current == ARGV[1] or string.sub(current, 1, string.len(ARGV[1])) == ARGV[1]) then
    return redis.call('DEL', KEYS[1])
end
return 0

