# Multi-Branch Cinema POS & Ticketing System — Phase 1 Runner Script
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "Multi-Branch Cinema POS & Ticketing System — Phase 1 Complete Stack" -ForegroundColor Green
Write-Host "=======================================================================" -ForegroundColor Cyan

if (-not (Test-Path ".env")) {
    Write-Host "[.env] Creating .env from .env.example..." -ForegroundColor Yellow
    Copy-Item ".env.example" ".env"
}

Write-Host ""
Write-Host "Starting Microservices Fleet, YARP Gateway, PostgreSQL, Redis, and Web UIs..." -ForegroundColor Yellow
Write-Host ""
Write-Host "GATEWAY & CENTRAL DOCUMENTATION:" -ForegroundColor Cyan
Write-Host "  - Unified Developer Portal:   http://localhost:8080" -ForegroundColor White
Write-Host "  - Unified Swagger UI:         http://localhost:8080/swagger" -ForegroundColor White
Write-Host "  - Prometheus Metrics:         http://localhost:8080/metrics" -ForegroundColor White
Write-Host ""
Write-Host "DATABASE, REPLICA, CACHE & MESSAGE BROKER:" -ForegroundColor Cyan
Write-Host "  - PostgreSQL Primary (Write): localhost:5433 (cinema_app / cinema)" -ForegroundColor White
Write-Host "  - PostgreSQL Read Replica:    localhost:5434 (cinema_app / cinema)" -ForegroundColor White
Write-Host "  - PostgreSQL Web UI (pgweb):  http://localhost:8086" -ForegroundColor White
Write-Host "  - Redis Web UI (Commander):   http://localhost:8087" -ForegroundColor White
Write-Host "  - RabbitMQ Web UI (Broker):   http://localhost:15672 (User: cinema_app, Pass: change-this-development-password)" -ForegroundColor White
Write-Host ""
Write-Host "DIRECT MICROSERVICE SWAGGER APIs:" -ForegroundColor Cyan
Write-Host "  - Catalog API:                http://localhost:8081/swagger" -ForegroundColor Gray
Write-Host "  - Reservation API:            http://localhost:8082/swagger" -ForegroundColor Gray
Write-Host "  - POS API:                    http://localhost:8083/swagger" -ForegroundColor Gray
Write-Host "  - Ticket API:                 http://localhost:8084/swagger" -ForegroundColor Gray
Write-Host "  - Identity API:               http://localhost:8085/swagger" -ForegroundColor Gray
Write-Host ""
Write-Host "OPERATIONAL SCRIPTS:" -ForegroundColor Cyan
Write-Host "  - Automated Fast Rollback:    powershell -ExecutionPolicy Bypass -File scripts/rollback.ps1" -ForegroundColor Gray
Write-Host "  - End-to-End Verification:    powershell -ExecutionPolicy Bypass -File scripts/verify-services.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C anytime to stop the containers." -ForegroundColor Yellow
Write-Host "=======================================================================" -ForegroundColor Cyan

docker compose up --build
