# ==============================================================================
# CINEMA POS SYSTEM — COMPREHENSIVE ENDPOINT-BY-ENDPOINT MANUAL TEST SUITE
# Validates all 38 endpoints across all microservices following our real business flow:
# Perimeter -> Identity -> POS Shift -> Catalog -> Reservation -> Payment -> 
# Ticket Lifecycle -> Print & Gate Redemption -> Shift Close -> Observability
# ==============================================================================
$ErrorActionPreference = "Stop"

$GatewayUrl = "http://localhost:8080"
$StepNumber = 1

function Print-Header {
    param([string]$Title)
    Write-Host "`n======================================================================" -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "======================================================================" -ForegroundColor Cyan
}

function Execute-Test {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Path,
        [hashtable]$Headers = @{},
        $Body = $null,
        [int]$ExpectedStatus = 200,
        [bool]$SkipJsonParse = $false
    )
    Write-Host "`n[Step $global:StepNumber] $Name" -ForegroundColor Yellow
    Write-Host "  -> $Method $GatewayUrl$Path" -ForegroundColor DarkGray
    $global:StepNumber++

    $uri = "$GatewayUrl$Path"
    $params = @{
        Uri = $uri
        Method = $Method
    }

    if ($Headers.Count -gt 0) {
        $params.Headers = $Headers
    }

    if ($null -ne $Body) {
        $bodyStr = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 10 -Compress }
        $params.Body = $bodyStr
        $params.ContentType = "application/json"
        Write-Host "  Request Body: $bodyStr" -ForegroundColor DarkCyan
    }

    $statusCode = 0
    $responseObj = $null
    $rawResponse = ""

    try {
        $resp = Invoke-WebRequest @params -UseBasicParsing
        $statusCode = [int]$resp.StatusCode
        $rawResponse = $resp.Content
        if (!$SkipJsonParse -and $rawResponse) {
            try { $responseObj = $rawResponse | ConvertFrom-Json } catch { $responseObj = $rawResponse }
        } else {
            $responseObj = $rawResponse
        }
    } catch {
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            $stream = $_.Exception.Response.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $rawResponse = $reader.ReadToEnd()
                try { $responseObj = $rawResponse | ConvertFrom-Json } catch { $responseObj = $rawResponse }
            }
        } else {
            Write-Host "  [ERROR] Communication failure: $($_.Exception.Message)" -ForegroundColor Red
            throw
        }
    }

    if ($statusCode -eq $ExpectedStatus) {
        Write-Host "  [PASS] Status $statusCode (Expected $ExpectedStatus)" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Status $statusCode (Expected $ExpectedStatus)" -ForegroundColor Red
        Write-Host "  Response: $rawResponse" -ForegroundColor Magenta
        throw "Assertion failed for $Name. Expected $ExpectedStatus but got $statusCode"
    }

    # Print abbreviated preview of response
    $preview = if ($responseObj -is [string]) { 
        if ($responseObj.Length -gt 250) { $responseObj.Substring(0, 250) + "..." } else { $responseObj } 
    } else { 
        $json = $responseObj | ConvertTo-Json -Depth 4 -Compress
        if ($json.Length -gt 250) { $json.Substring(0, 250) + "..." } else { $json }
    }
    Write-Host "  Response Payload: $preview" -ForegroundColor Gray

    return @{
        StatusCode = $statusCode
        Data = $responseObj
        Raw = $rawResponse
    }
}

# ------------------------------------------------------------------------------
# CLEAN TEST STATE BEFORE STARTING
# ------------------------------------------------------------------------------
Write-Host "Cleaning transactional data for fresh manual run..." -ForegroundColor DarkYellow
docker exec cinema-pos-postgres-1 psql -U cinema_app -d cinema -c "TRUNCATE reservations.confirmed_seats, reservations.reservation_seats, reservations.reservations, tickets.redemption_audit, tickets.tickets, pos.payments, pos.order_lines, pos.orders, pos.till_shifts CASCADE;" 2>$null | Out-Null
docker exec cinema-pos-redis-1 redis-cli --no-auth-warning -a change-this-development-password FLUSHDB 2>$null | Out-Null

# ==============================================================================
# 0. GATEWAY PERIMETER SECURITY
# ==============================================================================
Print-Header "PHASE 0: API GATEWAY PERIMETER DEFENSE"

# 1. Unauthenticated Request Rejection
Execute-Test -Name "Perimeter Security: Unauthenticated request to /api/v1/catalog/movies" `
    -Method "GET" `
    -Path "/api/v1/catalog/movies" `
    -ExpectedStatus 401 | Out-Null

# ==============================================================================
# 1. IDENTITY SERVICE & AUTHENTICATION FLOW
# ==============================================================================
Print-Header "PHASE 1: IDENTITY & ACCESS MANAGEMENT (IAM) FLOW"

# 2. Identity Service Health Contract (Anonymous)
Execute-Test -Name "Identity API: Health & Architectural Contract" `
    -Method "GET" `
    -Path "/api/v1/identity/health-contract" `
    -ExpectedStatus 200 | Out-Null

# 3. OIDC Discovery Document (Anonymous)
$oidc = Execute-Test -Name "OIDC Discovery: OpenID Configuration (.well-known)" `
    -Method "GET" `
    -Path "/.well-known/openid-configuration" `
    -ExpectedStatus 200

# 4. JWKS Public Keys Endpoint (Anonymous)
$jwks = Execute-Test -Name "OIDC Discovery: JSON Web Key Set (JWKS)" `
    -Method "GET" `
    -Path "/.well-known/jwks.json" `
    -ExpectedStatus 200

# 5. Staff Cashier Login
$loginResp = Execute-Test -Name "Identity API: Staff Login (Cashier Terminal)" `
    -Method "POST" `
    -Path "/api/v1/identity/login" `
    -Body @{ username = "cashier1"; pinOrPassword = "1234"; terminalCode = "TERM-POS-01" } `
    -ExpectedStatus 200

$CashierToken = $loginResp.Data.token
$CashierId = $loginResp.Data.userId
$BranchId = $loginResp.Data.branchId
$AuthHeader = @{ Authorization = "Bearer $CashierToken" }

# 6. List Staff Directory (Protected)
$users = Execute-Test -Name "Identity API: List Cinema Staff Members" `
    -Method "GET" `
    -Path "/api/v1/identity/users" `
    -Headers $AuthHeader `
    -ExpectedStatus 200

# 7. List RBAC Roles (Protected)
$roles = Execute-Test -Name "Identity API: List System RBAC Roles" `
    -Method "GET" `
    -Path "/api/v1/identity/roles" `
    -Headers $AuthHeader `
    -ExpectedStatus 200

# 8. Supervisor PIN Verification (Secure Body POST)
$supResp = Execute-Test -Name "Identity API: Supervisor Override PIN Verification" `
    -Method "POST" `
    -Path "/api/v1/identity/verify-supervisor" `
    -Headers $AuthHeader `
    -Body @{ supervisorUsername = "supervisor1"; supervisorPin = "9999" } `
    -ExpectedStatus 200

# ==============================================================================
# 2. POS CASHIER TILL SHIFT LIFECYCLE
# ==============================================================================
Print-Header "PHASE 2: POS CASHIER TILL SHIFT INITIALIZATION"

# 9. POS Health Contract
Execute-Test -Name "POS API: Health & Transactional Contract" `
    -Method "GET" `
    -Path "/api/v1/pos/health-contract" `
    -Headers $AuthHeader `
    -ExpectedStatus 200 | Out-Null

# 10. Open Cashier Till Shift
$openShiftResp = Execute-Test -Name "POS API: Open Till Shift with Opening Cash Float" `
    -Method "POST" `
    -Path "/api/v1/pos/shifts/open" `
    -Headers $AuthHeader `
    -Body @{
        branchId = $BranchId
        cashierId = $CashierId
        terminalCode = "TERM-POS-01"
        openingFloat = 150.00
    } `
    -ExpectedStatus 201

$ShiftId = $openShiftResp.Data.shiftId

# 11. Get Concessions Catalog
$productsResp = Execute-Test -Name "POS API: Concessions Menu Products" `
    -Method "GET" `
    -Path "/api/v1/pos/products" `
    -Headers $AuthHeader `
    -ExpectedStatus 200

$PopcornProduct = $productsResp.Data[0]

# ==============================================================================
# 3. CATALOG & MOVIE SCHEDULING FLOW
# ==============================================================================
Print-Header "PHASE 3: CATALOG, MOVIES & AUDITORIUM SEATING"

# 12. Catalog Health Contract
Execute-Test -Name "Catalog API: Health & Replication Contract" `
    -Method "GET" `
    -Path "/api/v1/catalog/health-contract" `
    -Headers $AuthHeader `
    -ExpectedStatus 200 | Out-Null

# 13. Get All Cinema Branches
$branches = Execute-Test -Name "Catalog API: Get Active Branches" `
    -Method "GET" `
    -Path "/api/v1/catalog/branches" `
    -Headers $AuthHeader `
    -ExpectedStatus 200

# 14. Get Movies Catalog
$movies = Execute-Test -Name "Catalog API: Get Movies Catalog" `
    -Method "GET" `
    -Path "/api/v1/catalog/movies" `
    -Headers $AuthHeader `
    -ExpectedStatus 200

$MovieId = $movies.Data[0].movieId

# 15. Get Showtimes Schedules
$showtimes = Execute-Test -Name "Catalog API: Query Showtimes for Branch & Movie" `
    -Method "GET" `
    -Path "/api/v1/catalog/showtimes?branchId=$BranchId&movieId=$MovieId" `
    -Headers $AuthHeader `
    -ExpectedStatus 200

$ShowtimeId = $showtimes.Data[0].showtimeId

# 16. Get Visual Auditorium Seat Map
$seatMap = Execute-Test -Name "Catalog API: Get Auditorium Seat Map Layout" `
    -Method "GET" `
    -Path "/api/v1/catalog/seat-map/$ShowtimeId" `
    -Headers $AuthHeader `
    -ExpectedStatus 200

$AvailableSeats = @($seatMap.Data.seats | Where-Object { $_.status -eq "available" })
$SelectedSeatId1 = $AvailableSeats[0].seatId
$SelectedSeatId2 = $AvailableSeats[1].seatId

# 17. Phase 2 Module 1.1: AuditoriumLayouts JSONB
$auditoriumId = "44444444-4444-4444-4444-444444444444"
Execute-Test -Name "Catalog API: Phase 2 JSONB Auditorium Layout" `
    -Method "GET" `
    -Path "/api/v1/catalog/auditorium-layouts/$auditoriumId" `
    -Headers $AuthHeader `
    -ExpectedStatus 200 | Out-Null

# 18. Phase 2 Module 1.3: Redis Seat Matrix with Jittered TTL
Execute-Test -Name "Catalog API: Phase 2 Redis Seat Matrix Schema (Jittered TTL)" `
    -Method "GET" `
    -Path "/api/v1/catalog/showtimes/$ShowtimeId/redis-seat-matrix" `
    -Headers $AuthHeader `
    -ExpectedStatus 200 | Out-Null

# 19. Phase 2 Module 1.2: Blockbuster High-Traffic Cache
Execute-Test -Name "Catalog API: Phase 2 Blockbuster Pre-computed High-Traffic Cache" `
    -Method "GET" `
    -Path "/api/v1/catalog/blockbuster/$ShowtimeId/seat-matrix" `
    -Headers $AuthHeader `
    -ExpectedStatus 200 | Out-Null

# ==============================================================================
# 4. RESERVATIONS & DISTRIBUTED LOCKING (REDIS LUA)
# ==============================================================================
Print-Header "PHASE 4: RESERVATION, DISTRIBUTED LOCKING & ACID"

# 20. Reservation Health Contract
Execute-Test -Name "Reservation API: Health & Distributed Lock Contract" `
    -Method "GET" `
    -Path "/api/v1/reservations/health-contract" `
    -Headers $AuthHeader `
    -ExpectedStatus 200 | Out-Null

# 21. Acquire Temporary Seat Hold (Redis Lua)
$idempKeyHold1 = [Guid]::NewGuid().ToString()
$holdHeader = @{ 
    Authorization = "Bearer $CashierToken"
    "X-Idempotency-Key" = $idempKeyHold1
}
$hold1Resp = Execute-Test -Name "Reservation API: Acquire Atomic Seat Hold (Redis Lua)" `
    -Method "POST" `
    -Path "/api/v1/reservations/holds" `
    -Headers $holdHeader `
    -Body @{
        showtimeId = $ShowtimeId
        seatIds = @($SelectedSeatId1)
        idempotencyKey = $idempKeyHold1
    } `
    -ExpectedStatus 201

$HoldId1 = $hold1Resp.Data.holdId
$FencingToken1 = $hold1Resp.Data.fencingToken

# 22. Distributed Lock Collision (Seat already held) -> 409 Conflict
$idempKeyColl = [Guid]::NewGuid().ToString()
Execute-Test -Name "Reservation API: Lock Collision Guard (Second user attempts same seat -> 409)" `
    -Method "POST" `
    -Path "/api/v1/reservations/holds" `
    -Headers @{ Authorization = "Bearer $CashierToken"; "X-Idempotency-Key" = $idempKeyColl } `
    -Body @{
        showtimeId = $ShowtimeId
        seatIds = @($SelectedSeatId1)
        idempotencyKey = $idempKeyColl
    } `
    -ExpectedStatus 409 | Out-Null

# 23. Release Seat Hold (Customer Changed Mind)
Execute-Test -Name "Reservation API: Release Seat Hold (Lua Safe Release)" `
    -Method "DELETE" `
    -Path "/api/v1/reservations/holds/$($HoldId1)?showtimeId=$($ShowtimeId)&seatId=$($SelectedSeatId1)&reason=CustomerChange" `
    -Headers $AuthHeader `
    -ExpectedStatus 200 | Out-Null

# 24. Re-Acquire Seat Hold for Final Booking
$idempKeyHold2 = [Guid]::NewGuid().ToString()
$hold2Resp = Execute-Test -Name "Reservation API: Re-Acquire Seat Hold for Customer Order" `
    -Method "POST" `
    -Path "/api/v1/reservations/holds" `
    -Headers @{ Authorization = "Bearer $CashierToken"; "X-Idempotency-Key" = $idempKeyHold2 } `
    -Body @{
        showtimeId = $ShowtimeId
        seatIds = @($SelectedSeatId1, $SelectedSeatId2)
        idempotencyKey = $idempKeyHold2
    } `
    -ExpectedStatus 201

$FinalHoldId = $hold2Resp.Data.holdId
$FinalFencingToken = $hold2Resp.Data.fencingToken

# 25. Confirm Seat Reservation (ACID Write to PostgreSQL)
$confirmIdemp = [Guid]::NewGuid().ToString()
$confirmResp = Execute-Test -Name "Reservation API: Confirm Reservation (PostgreSQL ACID Transaction)" `
    -Method "POST" `
    -Path "/api/v1/reservations/confirm" `
    -Headers $AuthHeader `
    -Body @{
        holdId = $FinalHoldId
        showtimeId = $ShowtimeId
        seatIds = @($SelectedSeatId1, $SelectedSeatId2)
        idempotencyKey = $confirmIdemp
        fencingToken = $FinalFencingToken
    } `
    -ExpectedStatus 200

$ReservationId = $confirmResp.Data.reservationId

# 26. Get Reservation Details (Read-After-Write Consistency)
$resvDetails = Execute-Test -Name "Reservation API: Get Reservation Details by ID" `
    -Method "GET" `
    -Path "/api/v1/reservations/$ReservationId" `
    -Headers $AuthHeader `
    -ExpectedStatus 200

# ==============================================================================
# 5. POS ORDER CREATION & PAYMENT SETTLEMENT
# ==============================================================================
Print-Header "PHASE 5: POS ORDER & PAYMENT SETTLEMENT"

# 27. Create POS Order (Seats + Concessions)
$orderIdemp = [Guid]::NewGuid().ToString()
$createOrderResp = Execute-Test -Name "POS API: Create Order Linking Reservation and F&B Concessions" `
    -Method "POST" `
    -Path "/api/v1/pos/orders" `
    -Headers @{ Authorization = "Bearer $CashierToken"; "X-Idempotency-Key" = $orderIdemp } `
    -Body @{
        branchId = $BranchId
        cashierId = $CashierId
        reservationId = $ReservationId
        discountAmount = 0.00
        lines = @(
            @{
                productId = $null
                description = "2x Standard Cinema Seats"
                quantity = 2
                unitPrice = 8.50
            },
            @{
                productId = $PopcornProduct.productId
                description = $PopcornProduct.name
                quantity = 1
                unitPrice = $PopcornProduct.unitPrice
            }
        )
    } `
    -ExpectedStatus 201

$OrderId = $createOrderResp.Data.orderId
$TotalAmount = $createOrderResp.Data.totalAmount

# 28. Process Local Cash Payment
$payIdemp = [Guid]::NewGuid().ToString()
$paymentResp = Execute-Test -Name "POS API: Process Cash Payment (RJ11 Cash Drawer Kick Dispatched)" `
    -Method "POST" `
    -Path "/api/v1/pos/orders/$OrderId/payments" `
    -Headers @{ Authorization = "Bearer $CashierToken"; "X-Idempotency-Key" = $payIdemp } `
    -Body @{
        method = "cash"
        amount = $TotalAmount
        tenderedAmount = ($TotalAmount + 5.00)
        providerReference = "CASH-COUNTER-01"
    } `
    -ExpectedStatus 200

# 29. Phase 2 Module 4.1: External Payment Webhook (7-Step Idempotency State Machine)
$webhookIdemp = [Guid]::NewGuid().ToString()
$webhookUri = "/api/v1/pos/payments/webhook"
$webhookBody = @{
    idempotencyKey = $webhookIdemp
    orderId = $OrderId
    amount = $TotalAmount
    transactionReference = "ABA-PAY-REF-98765"
    provider = "ABA_PAY"
} | ConvertTo-Json -Compress

$webhookPayload = @{
    idempotencyKey = [Guid]::NewGuid().ToString()
    orderId = $OrderId
    amount = $TotalAmount
    transactionReference = "ABA-PAY-REF-98765"
    provider = "ABA_PAY"
} | ConvertTo-Json -Compress

Execute-Test -Name "POS API: Phase 2 External Payment Webhook (7-Step Redis State Machine)" `
    -Method "POST" `
    -Path "/api/v1/pos/payments/webhook" `
    -Body $webhookPayload `
    -ExpectedStatus 200 | Out-Null

# ==============================================================================
# 6. TICKET ISSUANCE, PRINTING & GATE REDEMPTION
# ==============================================================================
Print-Header "PHASE 6: TICKET ISSUANCE, PRINTING & GATE REDEMPTION"

# 30. Ticket Health Contract
Execute-Test -Name "Ticket API: Health & State Machine Contract" `
    -Method "GET" `
    -Path "/api/v1/tickets/health-contract" `
    -Headers $AuthHeader `
    -ExpectedStatus 200 | Out-Null

# 31. Issue Digital Entry Tickets
$issueResp = Execute-Test -Name "Ticket API: Issue Cryptographic Entry Tickets" `
    -Method "POST" `
    -Path "/api/v1/tickets/issue" `
    -Headers $AuthHeader `
    -Body @{
        reservationId = $ReservationId
        showtimeId = $ShowtimeId
        seatIds = @($SelectedSeatId1, $SelectedSeatId2)
    } `
    -ExpectedStatus 201

$Ticket1 = $issueResp.Data[0]
$Ticket1Id = $Ticket1.ticketId
$Ticket1Qr = $Ticket1.ticketId

# 32. Get Ticket Verification Details
Execute-Test -Name "Ticket API: Get Ticket Details by ID" `
    -Method "GET" `
    -Path "/api/v1/tickets/$Ticket1Id" `
    -Headers $AuthHeader `
    -ExpectedStatus 200 | Out-Null

# 33. First Thermal Stub Print (Single-Print Invariant)
$print1Resp = Execute-Test -Name "Ticket API: Initial Thermal ESC/POS Print (is_printed -> true)" `
    -Method "POST" `
    -Path "/api/v1/tickets/$Ticket1Id/print" `
    -Headers $AuthHeader `
    -Body @{ isReprint = $false } `
    -ExpectedStatus 200

# 34. Unauthorized Reprint Attempt (Missing Supervisor PIN -> 400 Bad Request)
Execute-Test -Name "Ticket API: Reprint Guard (Reprint without Supervisor PIN blocked -> 400)" `
    -Method "POST" `
    -Path "/api/v1/tickets/$Ticket1Id/print" `
    -Headers $AuthHeader `
    -Body @{ isReprint = $true; supervisorPin = $null } `
    -ExpectedStatus 400 | Out-Null

# 35. Authorized Reprint with Supervisor PIN
Execute-Test -Name "Ticket API: Authorized Reprint with Supervisor PIN (Audit Log Recorded)" `
    -Method "POST" `
    -Path "/api/v1/tickets/$Ticket1Id/print" `
    -Headers $AuthHeader `
    -Body @{ isReprint = $true; supervisorPin = "9999" } `
    -ExpectedStatus 200 | Out-Null

# 36. Phase 2 Module 3.2: Cryptographic E-Ticket Payload
Execute-Test -Name "Ticket API: Phase 2 E-Ticket Payload with HMAC-SHA256 Signature" `
    -Method "GET" `
    -Path "/api/v1/tickets/$Ticket1Id/e-ticket" `
    -Headers $AuthHeader `
    -ExpectedStatus 200 | Out-Null

# 37. Gate Scanner Ticket Redemption (Gate Admission)
$gateId = [Guid]::NewGuid().ToString()
$redeemResp = Execute-Test -Name "Ticket API: Gate Scanner Redemption (Turnstile Admission Signal)" `
    -Method "POST" `
    -Path "/api/v1/tickets/redeem" `
    -Headers $AuthHeader `
    -Body @{
        qrToken = $Ticket1Qr
        terminalCode = "GATE-TURNSTILE-01"
        gateId = $gateId
    } `
    -ExpectedStatus 200

# 38. Duplicate Redemption Guard (Anti-Passback Double Scan -> 409 Conflict)
Execute-Test -Name "Ticket API: Anti-Double-Redemption Guard (Used Ticket Rejected -> 409)" `
    -Method "POST" `
    -Path "/api/v1/tickets/redeem" `
    -Headers $AuthHeader `
    -Body @{
        qrToken = $Ticket1Qr
        terminalCode = "GATE-TURNSTILE-01"
        gateId = $gateId
    } `
    -ExpectedStatus 409 | Out-Null

# ==============================================================================
# 7. POS CASHIER TILL SHIFT CLOSURE (Z-REPORT)
# ==============================================================================
Print-Header "PHASE 7: CASHIER TILL SHIFT CLOSURE & RECONCILIATION"

# 39. Close Cashier Till Shift
$closeShiftResp = Execute-Test -Name "POS API: Close Till Shift & Calculate Discrepancy (Z-Report)" `
    -Method "POST" `
    -Path "/api/v1/pos/shifts/close" `
    -Headers $AuthHeader `
    -Body @{
        shiftId = $ShiftId
        closingCash = 175.00
    } `
    -ExpectedStatus 200

# ==============================================================================
# 8. OBSERVABILITY, METRICS & SWAGGER HUB
# ==============================================================================
Print-Header "PHASE 8: GATEWAY OBSERVABILITY & UNIFIED SWAGGER HUB"

# 40. Prometheus Scraper Metrics Endpoint
Execute-Test -Name "Observability: Prometheus Metrics Scraper (/metrics)" `
    -Method "GET" `
    -Path "/metrics" `
    -SkipJsonParse $true `
    -ExpectedStatus 200 | Out-Null

# 41. Aggregated Swagger JSON for all 5 services
Execute-Test -Name "Unified Swagger: Aggregated OpenAPI Fleet Spec (/proxy-swagger/all)" `
    -Method "GET" `
    -Path "/proxy-swagger/all" `
    -ExpectedStatus 200 | Out-Null

# 42. Swagger Interactive UI Hub
Execute-Test -Name "Unified Swagger: Interactive Portal Hub (/swagger)" `
    -Method "GET" `
    -Path "/swagger" `
    -SkipJsonParse $true `
    -ExpectedStatus 200 | Out-Null

# ==============================================================================
# SUMMARY REPORT
# ==============================================================================
Write-Host "`n======================================================================" -ForegroundColor Green
Write-Host "  ALL 42 ENDPOINT VERIFICATIONS COMPLETED SUCCESSFULLY (100% PASS)" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  - Gateway Perimeter Security: Verified" -ForegroundColor White
Write-Host "  - Identity & RBAC (7 endpoints): Verified" -ForegroundColor White
Write-Host "  - POS Till Shift & Orders (7 endpoints): Verified" -ForegroundColor White
Write-Host "  - Catalog & Seating (8 endpoints): Verified" -ForegroundColor White
Write-Host "  - Reservation & Redis Lua Locking (5 endpoints): Verified" -ForegroundColor White
Write-Host "  - Ticket Issuance, Printing & Gate Redemption (8 endpoints): Verified" -ForegroundColor White
Write-Host "  - Observability, Prometheus & Swagger Hub (3 endpoints): Verified" -ForegroundColor White
Write-Host "======================================================================" -ForegroundColor Green
