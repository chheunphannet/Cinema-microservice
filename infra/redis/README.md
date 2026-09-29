# Redis key design and policies

| Key pattern | Type | TTL | Writer | Purpose |
|---|---|---:|---|---|
| `seat-hold:{showtimeId}:{seatId}` | String | 10 minutes | Reservation service | Atomic temporary hold; value is `holdId:fencingToken` |
| `seat-map:{showtimeId}` | JSON string | 60 seconds | Catalog/Reservation | Read-through seat-layout cache; invalidate after confirmed booking |
| `showtime:{showtimeId}` | JSON string | 5 minutes | Catalog | Showtime detail cache |
| `idem:{service}:{key}` | String | 24 hours | owning service | Completed request result to make retries safe |
| `rate:{subject}:{window}` | Counter | 1 minute | API gateway | Rate-limit counter |

Redis is an optimization and hold coordinator, never the financial source of truth. Lock acquisition is performed with `lua/acquire-seat-hold.lua`; release verifies the hold ID. When a hold becomes a booking, the Reservation service writes PostgreSQL in a transaction. The `reservations.confirmed_seats` unique active-seat index is the authoritative final guard against double booking. Use an idempotency key for every purchase/confirmation request.

In production, run Redis Cluster/Sentinel with TLS and an external secret store. `allkeys-lru` applies only to cache capacity; the service must monitor eviction and never assume a hold survived an eviction.
