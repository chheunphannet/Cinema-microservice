@echo off
setlocal enabledelayedexpansion

echo =======================================================================
echo   Cinema Microservices Platform and Box-Office Stack (Phase 1-3)
echo =======================================================================
echo.

:: 1. Verify Docker Engine is running
docker info >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker daemon is not running!
    echo Please start Docker Desktop and wait until it is fully initialized,
    echo then run this script again.
    echo.
    pause
    exit /b 1
)

:: 2. Ensure .env exists
if not exist .env (
    echo [.env] Creating .env from .env.example...
    copy .env.example .env >nul
)

:: 3. Determine if a rebuild is requested
set REBUILD=0
if "%1"=="--build" set REBUILD=1
if "%1"=="-b" set REBUILD=1

if %REBUILD% equ 1 (
    echo [BUILD] Rebuilding microservice images sequentially to ensure NuGet stability...
    docker compose build catalog-api
    docker compose build reservation-api
    docker compose build pos-api
    docker compose build ticket-api
    docker compose build identity-api
    docker compose build api-gateway
)

echo [START] Starting all 15 containers in detached mode...
docker compose up -d
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Failed to start containers via Docker Compose!
    echo Please check the error messages above.
    echo.
    pause
    exit /b 1
)

echo.
echo =======================================================================
echo   MICROSERVICES AND MANAGEMENT DASHBOARD
echo =======================================================================
echo   - Interactive Web Portal:       http://localhost:8080
echo   - Unified Swagger API Docs:     http://localhost:8080/swagger
echo   - Mailpit Email and QR Inbox:   http://localhost:8025
echo   - Seq Centralized Log Stream:   http://localhost:5341
echo   - Jaeger Distributed Tracing:   http://localhost:16686
echo   - RabbitMQ Management UI:       http://localhost:15672 (cinema_app / change-this-development-password)
echo   - Redis Commander (Locks/TTL):  http://localhost:8087
echo   - PgWeb Database Manager:       http://localhost:8086
echo   - Gateway Prometheus Metrics:   http://localhost:8080/metrics
echo.
echo =======================================================================
echo   CONTAINER HEALTH STATUS
echo =======================================================================
docker compose ps
echo.
echo =======================================================================
echo   Commands:
echo     - View live logs:     docker compose logs -f
echo     - Stop all services:  docker compose down
echo     - Rebuild images:     run.bat --build
echo =======================================================================
echo.
set /p VIEW_LOGS="Do you want to follow live logs now? (Y/N, default N): "
if /i "%VIEW_LOGS%"=="Y" (
    docker compose logs -f
) else (
    echo.
    echo Containers are running in the background. Press any key to close this window.
    pause >nul
)
