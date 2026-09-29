# Verification script for Milestone 5.3: Pricing & Multi-Branch Inventory
$ErrorActionPreference = "Stop"

Write-Host "=== 1. Logging in as Admin ===" -ForegroundColor Cyan
$loginBody = '{"username":"admin1","pinOrPassword":"admin123"}'
$loginResp = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/identity/login" -Method Post -ContentType "application/json" -Body $loginBody
$token = $loginResp.token
Write-Host "Token obtained successfully." -ForegroundColor Green

$headers = @{
    "Authorization" = "Bearer $token"
}

Write-Host "`n=== 2. Testing Ticket Types & Price Cards ===" -ForegroundColor Cyan
# 2.1 Get Ticket Types
$ticketTypes = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/pricing/ticket-types" -Method Get -Headers $headers
Write-Host "Found $($ticketTypes.Count) ticket types." -ForegroundColor Green
$adult = $ticketTypes | Where-Object { $_.code -eq "ADULT" }

$uniqueCode = "STUDENT_$([Guid]::NewGuid().ToString().Substring(0,4).ToUpper())"
$newType = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/pricing/ticket-types" -Method Post -Headers $headers -ContentType "application/json" -Body "{`"code`":`"$uniqueCode`",`"name`":`"Student Pass`"}"
Write-Host "Created Ticket Type: $($newType.code) ($($newType.ticketTypeId))" -ForegroundColor Green

# 2.3 Get Price Cards
$priceCards = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/pricing/price-cards" -Method Get -Headers $headers
Write-Host "Found $($priceCards.Count) price cards." -ForegroundColor Green
$firstCard = $priceCards[0]

# 2.4 Get Price Card Details
$cardDetail = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/pricing/price-cards/$($firstCard.priceCardId)" -Method Get -Headers $headers
Write-Host "Price Card '$($cardDetail.name)' has $($cardDetail.entries.Count) matrix entries." -ForegroundColor Green

# 2.5 List Pricing Rules
$rules = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/pricing/rules" -Method Get -Headers $headers
Write-Host "Found $($rules.Count) dynamic pricing rules & surcharges." -ForegroundColor Green
foreach ($r in $rules) {
    Write-Host " - [$($r.ruleType)] $($r.name): $($r.adjustmentValue) ($($r.adjustmentType))" -ForegroundColor Yellow
}

# 2.6 Calculate Dynamic Price (Wednesday Matinee with 3D Surcharge)
$calcReq = @{
    priceCardId = $firstCard.priceCardId
    ticketTypeCode = "ADULT"
    seatType = "standard"
    showtimeStartsAt = "2026-09-23T10:30:00Z" # Wednesday 10:30 AM
    screenTypeCode = "3D"
} | ConvertTo-Json

$calcResp = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/pricing/calculate" -Method Post -Headers $headers -ContentType "application/json" -Body $calcReq
Write-Host "Dynamic Price Calculation for Wednesday 10:30 AM 3D:" -ForegroundColor Green
Write-Host "  Base Price: $($calcResp.basePrice)"
Write-Host "  Adjustments: $($calcResp.adjustments.Count)"
foreach ($adj in $calcResp.adjustments) {
    Write-Host "   * $($adj.ruleName): $($adj.amount)"
}
Write-Host "  Final Price: $($calcResp.finalPrice)" -ForegroundColor Cyan

Write-Host "`n=== 3. Testing Multi-Branch Inventory & Suppliers ===" -ForegroundColor Cyan
$branch1Id = "11111111-1111-1111-1111-111111111111"

# 3.1 Get Branch Stock
$stock = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/inventory/branches/$branch1Id/stock" -Method Get -Headers $headers
Write-Host "Branch 1 has $($stock.Count) inventory items." -ForegroundColor Green
$popcorn = $stock | Where-Object { $_.sku -eq "SKU-POPCORN-L" }
Write-Host "Popcorn current stock: $($popcorn.stockQuantity)" -ForegroundColor Green

# 3.2 Adjust Stock (+30)
$adjustReq = '{"quantityDelta": 30, "reason": "Supplier Restock"}'
$adjustResp = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/inventory/branches/$branch1Id/products/$($popcorn.productId)/adjust" -Method Post -Headers $headers -ContentType "application/json" -Body $adjustReq
Write-Host "Adjusted stock: New quantity = $($adjustResp.stockQuantity)" -ForegroundColor Green

# 3.3 Log Spoilage / Wastage
$wastageReq = @{
    productId = $popcorn.productId
    quantity = 2
    reason = "damaged"
    unitCost = 1.50
    notes = "Crushed packaging during unloading"
} | ConvertTo-Json
$wastageResp = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/inventory/branches/$branch1Id/wastage" -Method Post -Headers $headers -ContentType "application/json" -Body $wastageReq
Write-Host "Wastage logged: ID $($wastageResp.wastageId), Cost Loss: $($wastageResp.costLoss)" -ForegroundColor Green

# 3.4 Out-of-Stock Toggle
$toggleReq = '{"isOutOfStock": true}'
$toggleResp = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/inventory/branches/$branch1Id/products/$($popcorn.productId)/availability" -Method Patch -Headers $headers -ContentType "application/json" -Body $toggleReq
Write-Host "Toggled Out of Stock: $($toggleResp.isOutOfStock)" -ForegroundColor Yellow

# Toggle back
$toggleReq2 = '{"isOutOfStock": false}'
$toggleResp2 = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/inventory/branches/$branch1Id/products/$($popcorn.productId)/availability" -Method Patch -Headers $headers -ContentType "application/json" -Body $toggleReq2
Write-Host "Restored Availability: $($toggleResp2.isOutOfStock)" -ForegroundColor Green

# 3.5 Suppliers & Purchase Orders
$suppliers = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/inventory/suppliers" -Method Get -Headers $headers
Write-Host "Found $($suppliers.Count) suppliers: $($suppliers[0].name)" -ForegroundColor Green

$poReq = @{
    poNumber = "PO-LIVE-$([Guid]::NewGuid().ToString().Substring(0,8))"
    supplierId = $suppliers[0].supplierId
    branchId = $branch1Id
    notes = "Live verification order"
    lines = @(
        @{
            productId = $popcorn.productId
            quantity = 50
            unitCost = 2.00
        }
    )
} | ConvertTo-Json

$poResp = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/inventory/purchase-orders" -Method Post -Headers $headers -ContentType "application/json" -Body $poReq
Write-Host "Created PO: $($poResp.poNumber) TotalCost: $($poResp.totalCost) Status: $($poResp.status)" -ForegroundColor Green

# Receive PO (auto-restock)
$recvReq = '{"status": "received"}'
$recvResp = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/inventory/purchase-orders/$($poResp.poId)/status" -Method Patch -Headers $headers -ContentType "application/json" -Body $recvReq
Write-Host "Updated PO to 'received': Status = $($recvResp.status)" -ForegroundColor Green

# Verify Restocked Stock
$finalStock = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/admin/inventory/branches/$branch1Id/products/$($popcorn.productId)" -Method Get -Headers $headers
Write-Host "Final Popcorn Stock after PO receipt: $($finalStock.stockQuantity)" -ForegroundColor Green

Write-Host "`n>>> ALL MILESTONE 5.3 ENDPOINTS VERIFIED END-TO-END! <<<" -ForegroundColor Green
