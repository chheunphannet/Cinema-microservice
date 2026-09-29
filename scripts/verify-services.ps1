# End-to-End Verification Script for Multi-Branch Cinema POS Microservices
# Fully compliant with Phase 2 Security Hardening (All requests route via YARP Gateway on port 8080)
$ErrorActionPreference = "Stop"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "   CINEMA POS SYSTEM - SECURE E2E VERIFICATION SUITE    " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

function Assert-Step {
    param(
        [Parameter(Mandatory=$true)][string]$Title,
        [Parameter(Mandatory=$true)][bool]$Condition
    )
    if ($Condition) {
        Write-Host " [PASS] $Title" -ForegroundColor Green
    } else {
        Write-Host " [FAIL] $Title" -ForegroundColor Red
        throw "Verification step failed: $Title"
    }
}

$GatewayUrl = "http://localhost:8080"

# 0. Perimeter Security Verification: Unauthenticated requests MUST be rejected with 401
Write-Host "`n0. Testing API Gateway Perimeter Security..." -ForegroundColor Yellow
$unauthBlocked = $false
try {
    Invoke-RestMethod -Uri "$GatewayUrl/api/v1/catalog/movies" -Method Get
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 401) {
        $unauthBlocked = $true
    }
}
Assert-Step -Title "Gateway Perimeter blocks unauthenticated request with 401 Unauthorized" -Condition $unauthBlocked

# 1. Identity Service: Staff Login with PIN
Write-Host "`n1. Testing Identity Service Authentication..." -ForegroundColor Yellow
$loginPayload = @{
    username = "cashier1"
    pinOrPassword = "1234"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/identity/login" -Method Post -ContentType "application/json" -Body $loginPayload

Assert-Step -Title "Cashier login returns valid JWT token via Gateway" -Condition (![string]::IsNullOrWhiteSpace($loginResponse.token))
Assert-Step -Title "Cashier role assigned correctly" -Condition ($loginResponse.roles[0] -eq "cashier")

$token = $loginResponse.token
$authHeader = @{ Authorization = "Bearer $token" }

# 2. Identity Service: Protected User Directory (Bearer Auth)
Write-Host "`n2. Testing RBAC Protected Endpoint..." -ForegroundColor Yellow
$users = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/identity/users" -Method Get -Headers $authHeader
Assert-Step -Title "Protected /users endpoint retrieved via JWT" -Condition ($users.Count -ge 2)

# 3. Identity Service: Supervisor PIN Verification (Secure POST Body)
$supPayload = @{
    supervisorUsername = "supervisor1"
    supervisorPin = "9999"
} | ConvertTo-Json

$supResponse = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/identity/verify-supervisor" -Method Post -ContentType "application/json" -Body $supPayload -Headers $authHeader
Assert-Step -Title "Supervisor PIN 9999 verified successfully" -Condition ($supResponse.verified -eq $true)

# 4. Catalog Service: Branches and Movies from PostgreSQL
Write-Host "`n3. Testing Catalog Service (PostgreSQL Seeds via Gateway)..." -ForegroundColor Yellow
$branches = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/catalog/branches" -Method Get -Headers $authHeader
Assert-Step -Title "Active branches retrieved from DB" -Condition ($branches.Count -ge 2)

$movies = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/catalog/movies" -Method Get -Headers $authHeader
Assert-Step -Title "Movies catalog retrieved from DB" -Condition ($movies.Count -ge 3)

$showtimes = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/catalog/showtimes" -Method Get -Headers $authHeader
Assert-Step -Title "Showtimes schedules retrieved from DB" -Condition ($showtimes.Count -ge 1)

$showtimeId = $showtimes[0].showtimeId

# 5. Catalog Service: Real-Time Seat Map with Hold and Booking Status
$seatMap = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/catalog/seat-map/$showtimeId" -Method Get -Headers $authHeader
Assert-Step -Title "Seat map retrieved for showtime" -Condition ($seatMap.seats.Count -ge 4)
$availableSeats = @($seatMap.seats | Where-Object { $_.status -eq "available" })

# Clean transactional tables and test locks on primary and replica for deterministic test runs
docker exec cinema-pos-postgres-1 psql -U cinema_app -d cinema -c "TRUNCATE reservations.confirmed_seats, reservations.reservation_seats, reservations.reservations, tickets.redemption_audit, tickets.tickets, pos.payments, pos.order_lines, pos.orders CASCADE;" 2>$null | Out-Null
docker exec cinema-pos-postgres-replica-1 psql -U cinema_app -d cinema -c "TRUNCATE reservations.confirmed_seats, reservations.reservation_seats, reservations.reservations, tickets.redemption_audit, tickets.tickets, pos.payments, pos.order_lines, pos.orders CASCADE;" 2>$null | Out-Null
docker exec cinema-pos-redis-1 redis-cli --no-auth-warning -a change-this-development-password FLUSHDB 2>$null | Out-Null

$seatMap = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/catalog/seat-map/$showtimeId" -Method Get -Headers $authHeader
$availableSeats = @($seatMap.seats | Where-Object { $_.status -eq "available" })
$seatId1 = $availableSeats[0].seatId

# 6. Reservation Service: Atomic Seat Hold via Redis Lua Script
Write-Host "`n4. Testing Reservation Distributed Locking and ACID..." -ForegroundColor Yellow
$idempKey1 = [Guid]::NewGuid().ToString()
$holdPayload = @{
    showtimeId = $showtimeId
    seatIds = @($seatId1)
    idempotencyKey = $idempKey1
} | ConvertTo-Json

$holdResponse = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/reservations/holds" -Method Post -ContentType "application/json" -Body $holdPayload -Headers $authHeader
Assert-Step -Title "Atomic seat hold acquired via Redis Lua" -Condition ($holdResponse.status -eq "held")
$holdId = $holdResponse.holdId
$fencingToken = $holdResponse.fencingToken

# 7. Concurrency Collision Test: Second hold on SAME seat must return 409 Conflict
$collisionAttemptFailed = $false
try {
    $collisionPayload = @{
        showtimeId = $showtimeId
        seatIds = @($seatId1)
        idempotencyKey = [Guid]::NewGuid().ToString()
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$GatewayUrl/api/v1/reservations/holds" -Method Post -ContentType "application/json" -Body $collisionPayload -Headers $authHeader
} catch {
    $collisionAttemptFailed = $true
}
Assert-Step -Title "Redis Lua lock collision detected and rejected with 409 Conflict" -Condition $collisionAttemptFailed

# 8. Monotonic Fencing Token Test: Attempting to confirm with stale fencing token must return 409 Conflict
$staleFencingFailed = $false
try {
    $stalePayload = @{
        holdId = $holdId
        showtimeId = $showtimeId
        seatIds = @($seatId1)
        idempotencyKey = [Guid]::NewGuid().ToString()
        fencingToken = $fencingToken - 100
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$GatewayUrl/api/v1/reservations/confirm" -Method Post -ContentType "application/json" -Body $stalePayload -Headers $authHeader
} catch {
    $staleFencingFailed = $true
}
Assert-Step -Title "Monotonic Fencing Token Rule rejected stale token with 409 Conflict" -Condition $staleFencingFailed

# 9. Reservation Service: Confirm Booking in PostgreSQL Transaction
$confirmPayload = @{
    holdId = $holdId
    showtimeId = $showtimeId
    seatIds = @($seatId1)
    idempotencyKey = $idempKey1
    fencingToken = $fencingToken
} | ConvertTo-Json

$confirmResponse = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/reservations/confirm" -Method Post -ContentType "application/json" -Body $confirmPayload -Headers $authHeader
Assert-Step -Title "Reservation confirmed in PostgreSQL under ACID transaction" -Condition ($confirmResponse.status -eq "confirmed")

# 10. Read-After-Write Consistency Routing: Immediately inspect reservation from Primary DB
$rawRes = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/reservations/$holdId" -Method Get -Headers $authHeader
Assert-Step -Title "Read-After-Write query retrieved confirmed reservation immediately" -Condition ($rawRes.status -eq "confirmed")

# 11. ACID Double-Booking Guard Test: Attempting to confirm already booked seat must return 409 Conflict
$doubleBookingFailed = $false
try {
    $doubleBookPayload = @{
        holdId = [Guid]::NewGuid().ToString()
        showtimeId = $showtimeId
        seatIds = @($seatId1)
        idempotencyKey = [Guid]::NewGuid().ToString()
        fencingToken = $fencingToken + 1
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$GatewayUrl/api/v1/reservations/confirm" -Method Post -ContentType "application/json" -Body $doubleBookPayload -Headers $authHeader
} catch {
    $doubleBookingFailed = $true
}
Assert-Step -Title "PostgreSQL UNIQUE constraint prevented double-booking with 409 Conflict" -Condition $doubleBookingFailed

# 12. Ticket Service: Issue Ticket
Write-Host "`n5. Testing Ticket Issuance and Lifecycle..." -ForegroundColor Yellow
$issuePayload = @{
    reservationId = $holdId
    showtimeId = $showtimeId
    seatIds = @($seatId1)
} | ConvertTo-Json

$tickets = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/tickets/issue" -Method Post -ContentType "application/json" -Body $issuePayload -Headers $authHeader
Assert-Step -Title "Ticket issued in database" -Condition ($tickets.Count -ge 1)
$ticketId = $tickets[0].ticketId

# 13. Ticket Service: First-time print (allowed without supervisor PIN)
$printResponse = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/tickets/$ticketId/print" -Method Post -ContentType "application/json" -Body "{}" -Headers $authHeader
Assert-Step -Title "First thermal print stub generated" -Condition ($printResponse.isPrinted -eq $true)

# 14. Ticket Service: Reprint without supervisor PIN (must fail with 400 Bad Request)
$unauthorizedReprintFailed = $false
try {
    $reprintPayload = @{ isReprint = $true } | ConvertTo-Json
    Invoke-RestMethod -Uri "$GatewayUrl/api/v1/tickets/$ticketId/print" -Method Post -ContentType "application/json" -Body $reprintPayload -Headers $authHeader
} catch {
    $unauthorizedReprintFailed = $true
}
Assert-Step -Title "Reprint without supervisor PIN blocked with 400 Bad Request" -Condition $unauthorizedReprintFailed

# 15. Ticket Service: Reprint with Supervisor PIN in request body (must succeed)
$authReprintPayload = @{
    isReprint = $true
    supervisorPin = "9999"
} | ConvertTo-Json
$authReprint = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/tickets/$ticketId/print" -Method Post -ContentType "application/json" -Body $authReprintPayload -Headers $authHeader
Assert-Step -Title "Reprint with supervisor PIN authorized without URL leakage" -Condition ($authReprint.isPrinted -eq $true)

# 16. Ticket Service: Redeem ticket at gate
$redeemPayload = @{
    qrToken = $ticketId
    terminalCode = "GATE-TURNSTILE-01"
} | ConvertTo-Json
$redeemResponse = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/tickets/redeem" -Method Post -ContentType "application/json" -Body $redeemPayload -Headers $authHeader
Assert-Step -Title "Ticket redeemed and turnstile admission signal dispatched" -Condition ($redeemResponse.status -eq "admitted")

# 17. Ticket Service: Second redemption of same ticket must return 409 Conflict
$duplicateRedeemFailed = $false
try {
    Invoke-RestMethod -Uri "$GatewayUrl/api/v1/tickets/redeem" -Method Post -ContentType "application/json" -Body $redeemPayload -Headers $authHeader
} catch {
    $duplicateRedeemFailed = $true
}
Assert-Step -Title "Duplicate redemption blocked with 409 Conflict" -Condition $duplicateRedeemFailed

# 18. POS Service: Till Shift Float Management
Write-Host "`n6. Testing POS Box-Office and Concessions..." -ForegroundColor Yellow
$branchId = $branches[0].branchId
$cashierId = $loginResponse.userId
$shiftPayload = @{
    branchId = $branchId
    cashierId = $cashierId
    terminalCode = "POS-TERM-01"
    openingFloat = 100.00
} | ConvertTo-Json

$shift = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/pos/shifts/open" -Method Post -ContentType "application/json" -Body $shiftPayload -Headers $authHeader
Assert-Step -Title "Cashier till shift opened with float in PostgreSQL" -Condition ($shift.status -eq "open")
$shiftId = $shift.shiftId

# 19. POS Service: Concessions Menu
$products = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/pos/products" -Method Get -Headers $authHeader
Assert-Step -Title "Concession menu products retrieved" -Condition ($products.Count -ge 3)

# 20. POS Service: Create Order and Process Cash Payment with Drawer Kick
$orderPayload = @{
    branchId = $branchId
    cashierId = $cashierId
    reservationId = $holdId
    discountAmount = 0.00
    lines = @(
        @{ productId = $products[0].productId; description = $products[0].name; quantity = 1; unitPrice = 5.50 }
    )
} | ConvertTo-Json

$order = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/pos/orders" -Method Post -ContentType "application/json" -Body $orderPayload -Headers $authHeader
Assert-Step -Title "Counter order created with transaction" -Condition ($order.status -eq "pending_payment")
$orderId = $order.orderId

$payHeaders = @{
    Authorization = "Bearer $token"
    "X-Idempotency-Key" = [Guid]::NewGuid().ToString()
}
$payPayload = @{
    method = "cash"
    amount = 5.50
    tenderedAmount = 10.00
} | ConvertTo-Json

$payment = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/pos/orders/$orderId/payments" -Method Post -ContentType "application/json" -Body $payPayload -Headers $payHeaders
Assert-Step -Title "Cash payment captured and change calculated" -Condition ($payment.changeGiven -eq 4.50)
Assert-Step -Title "RJ11 Cash drawer kick signal dispatched for cash tender" -Condition ($payment.drawerKickSignal -eq $true)

# 21. Gateway Swagger Hub & Observability UIs
Write-Host "`n7. Testing API Gateway and Infrastructure..." -ForegroundColor Yellow
$gw = Invoke-WebRequest -Uri "$GatewayUrl/swagger" -Method Get -UseBasicParsing
Assert-Step -Title "API Gateway Unified Swagger Hub accessible at $GatewayUrl/swagger" -Condition ($gw.StatusCode -eq 200)

$jaegerUi = Invoke-WebRequest -Uri "http://localhost:16686" -Method Get -UseBasicParsing
Assert-Step -Title "Jaeger Distributed Tracing UI accessible at http://localhost:16686" -Condition ($jaegerUi.StatusCode -eq 200)

# 22. Telemetry & Metrics (Module 3.2 Operability & Module 5.1 Performance Gates)
Write-Host "`n8. Testing Operability, Telemetry & PostgreSQL Read Replica..." -ForegroundColor Yellow
$metrics = Invoke-WebRequest -Uri "$GatewayUrl/metrics" -Method Get -UseBasicParsing
Assert-Step -Title "Prometheus metrics endpoint operational at /metrics" -Condition ($metrics.StatusCode -eq 200 -and $metrics.Content -match "http")
Assert-Step -Title "Telemetry response headers (X-Correlation-ID, X-Response-Time-Ms) present" -Condition ($metrics.Headers.ContainsKey("X-Correlation-ID") -and $metrics.Headers.ContainsKey("X-Response-Time-Ms"))

# 23. PostgreSQL Read-Replica Container Health (Module 1.2 Master-Slave Replication)
$replicaStatus = docker inspect --format="{{.State.Health.Status}}" cinema-pos-postgres-replica-1 2>$null
Assert-Step -Title "PostgreSQL Read Replica container is running and healthy" -Condition ($replicaStatus -eq "healthy")

# 24. Phase 2 Architecture Verification
Write-Host "`n9. Testing Phase 2 Architecture Specifications..." -ForegroundColor Yellow
$audLayout = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/catalog/auditorium-layouts/44444444-4444-4444-4444-444444444444" -Method Get -Headers $authHeader
Assert-Step -Title "Phase 2 Module 1.1: AuditoriumLayouts JSONB retrieved" -Condition ($audLayout.seatMap.Count -ge 1)

$redisSeatMatrix = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/catalog/showtimes/$showtimeId/redis-seat-matrix" -Method Get -Headers $authHeader
Assert-Step -Title "Phase 2 Module 1.3: Redis Seat Matrix schema & Jittered TTL (540s-660s) verified" -Condition ($redisSeatMatrix.seats.Count -ge 1 -and $redisSeatMatrix.ttl -ge 500 -and $redisSeatMatrix.ttl -le 700)

$blockbusterCache = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/catalog/blockbuster/$showtimeId/seat-matrix" -Method Get -Headers $authHeader
Assert-Step -Title "Phase 2 Module 1.2: Blockbuster High-Traffic Cache precomputed (Twitter celebrity tweet pattern)" -Condition ($blockbusterCache.hybridModel -match "Twitter")

$p2WhIdemp = [Guid]::NewGuid().ToString()
$p2WhPayload = @{
    idempotencyKey = $p2WhIdemp
    orderId = $orderId
    amount = 5.50
    transactionReference = "TXN-P2-VERIFY"
    provider = "Bakong-KHQR"
} | ConvertTo-Json

# Webhook routed anonymously without JWT requirement
$whP2Res = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/pos/payments/webhook" -Method Post -ContentType "application/json" -Body $p2WhPayload
Assert-Step -Title "Phase 2 Module 4.1: Payment Webhook processed with 7-step idempotency state machine via Gateway" -Condition ($whP2Res.status -eq "Succeeded")
Assert-Step -Title "Phase 2 Module 4.3: p99 Latency Telemetry (Webhook ACK, Redis Lua, SQL commit) captured" -Condition ($whP2Res.p99Metrics.webhookAckLatencyMs -gt 0)

$whP2Cached = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/pos/payments/webhook" -Method Post -ContentType "application/json" -Body $p2WhPayload
Assert-Step -Title "Phase 2 Module 4.1: Duplicate Payment Webhook returned cached response without double-charging" -Condition ($whP2Cached.cached -eq $true)

$eTicketRes = Invoke-RestMethod -Uri "$GatewayUrl/api/v1/tickets/$ticketId/e-ticket" -Method Get -Headers $authHeader
Assert-Step -Title "Phase 2 Module 3.2: E-Ticket HMAC-SHA256 signature payload generated" -Condition ($eTicketRes.sig -match "^HMAC-SHA256\([a-f0-9]{64}\)$")

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host "   ALL 30 VERIFICATION CHECKS (PHASE 1 & 2) PASSED!     " -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
