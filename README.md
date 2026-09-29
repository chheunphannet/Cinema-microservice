# Multi-Branch Cinema POS & Ticketing System

An enterprise-grade, distributed microservices platform designed for cinema chain management, real-time ticket reservations, box office point-of-sale (POS) operations, customer loyalty, and multi-branch catalog management.

Built with ASP.NET Core on .NET 8, PostgreSQL, Redis, RabbitMQ, YARP Gateway, and OpenTelemetry.

---

## Architecture Overview

The system is decomposed into domain-aligned microservices following Clean Architecture and Domain-Driven Design principles. Shared cross-cutting concerns reside in a foundation library (`Cinema.Foundation`).

### Core Microservices

- **Gateway.Api** (Port `8080`): Built with Microsoft YARP (Yet Another Reverse Proxy). Handles unified routing, request dispatching, developer portal, and centralized Swagger documentation aggregation.
- **Catalog.Api** (Port `8081`): Manages branches, auditoriums, seating layouts, movie catalogs, schedules, and digital media assets via S3/MinIO.
- **Reservation.Api** (Port `8082`): Manages seat selection and reservation lifecycle. Uses Redis distributed locking with Lua scripts for atomic 10-minute holds, fencing tokens, and ACID PostgreSQL transaction confirmation.
- **Pos.Api** (Port `8083`): Powers front-desk cashiers and till management. Handles cashier shift floats (Z-reports), split payments (cash, card, KHQR), concession combos, and reliable transactional outbox event publishing.
- **Ticket.Api** (Port `8084`): Issues cryptographically verifiable digital tickets, handles ESC/POS thermal printing with strict single-print invariants, and validates gate QR check-ins.
- **Identity.Api** (Port `8085`): Issues JWT tokens and exposes JWKS endpoints. Manages branch staff authentication, supervisor overrides, cashier PIN logins, customer social authentication (Google OAuth), and role-based access control (RBAC).
- **Loyalty.Api** (Port `8088`): Tracks customer reward points, membership tiers, milestone rewards, and processes asynchronous order events consumed from RabbitMQ.
- **Cinema.DbMigrator**: Dedicated migration container managing forward-only database schema definitions and bootstrap seeding upon startup.

### Client Applications

- **Admin UI**: Web-based administration dashboard built with Astro, TypeScript, and Tailwind CSS for cinema managers and catalog administrators (`Admin UI/`).
- **POS Client UI**: Windows desktop box-office client application built with WPF and .NET Core (`POS client UI/`).

### Infrastructure Services

- **PostgreSQL 16**: Primary and replica replication setup ensuring data consistency, ACID transactions, and double-booking prevention.
- **Redis 7**: High-performance cache and distributed lock provider utilizing atomic Lua scripts and LRU eviction.
- **RabbitMQ 3**: AMQP event bus for asynchronous integration events and transactional outbox message processing.
- **MinIO**: High-performance S3-compatible object storage for movie posters, icons, and media files.
- **Jaeger**: Distributed tracing backend supporting OpenTelemetry (OTLP).
- **Seq**: Structured log ingestion and search server.
- **Mailpit**: Development SMTP server and web interface for inspecting outgoing booking emails and ticket notifications.

---

## Quick Start

### Prerequisites

- Docker Desktop (or Docker Engine with Docker Compose v2)
- .NET 8 SDK (for local development and testing)
- PowerShell or Windows Command Prompt

### Launching the Infrastructure and Services

Choose one of the following methods to spin up the complete environment:

#### Option A: Windows Batch
```cmd
run.bat
```

#### Option B: PowerShell
```powershell
.\run.ps1
```

#### Option C: Docker Compose Direct
```bash
docker compose up --build -d
```

Note: If a `.env` file does not exist, the startup scripts will automatically copy `.env.example` to `.env`.

---

## Central Endpoints and Management Portals

Once the containers are started, the following services and web interfaces are accessible on localhost:

### Application and Gateway Endpoints

| Service / Portal | Port | URL | Description |
|---|---|---|---|
| Central Gateway & Portal | 8080 | http://localhost:8080 | Landing dashboard with links to all services and consoles |
| Unified Swagger UI Hub | 8080 | http://localhost:8080/swagger | Aggregated OpenAPI documentation for all microservices |
| Catalog Service | 8081 | http://localhost:8081/swagger | Movies, branches, auditoriums, and seat layouts |
| Reservation Service | 8082 | http://localhost:8082/swagger | Atomic seat holds, holds expiration, and booking confirmation |
| POS Service | 8083 | http://localhost:8083/swagger | Cashier shifts, split payments, orders, and concessions |
| Ticket Service | 8084 | http://localhost:8084/swagger | Ticket issuance, thermal printing, and gate check-in |
| Identity Service | 8085 | http://localhost:8085/swagger | Staff PIN login, JWT/JWKS, customer authentication, and RBAC |
| Loyalty Service | 8088 | http://localhost:8088/swagger | Points, tiers, rewards, and redemption management |

### Infrastructure Management Consoles

| Tool | Port | URL | Description |
|---|---|---|---|
| PostgreSQL Web UI (pgweb) | 8086 | http://localhost:8086 | Direct SQL query console and relational schema browser |
| Redis Commander | 8087 | http://localhost:8087 | Key-value browser for distributed locks, TTLs, and cache keys |
| RabbitMQ Management | 15672 | http://localhost:15672 | Message broker queues, exchanges, and consumers interface |
| Jaeger Tracing UI | 16186 | http://localhost:16186 | Distributed request trace analyzer and dependency graph |
| Seq Structured Logs | 5341 | http://localhost:5341 | Centralized structured log query and monitoring interface |
| MinIO Storage Console | 9001 | http://localhost:9001 | Object storage console for media assets and posters |
| Mailpit Web UI | 8025 | http://localhost:8025 | Local email inbox to inspect outgoing confirmation messages |

---

## Core System Invariants

### 1. Atomic Seat Reservation and Double-Booking Guard
- **Advisory Distributed Locking**: Redis Lua scripts execute atomic lock acquisition with an automatic 10-minute expiration.
- **Monotonic Fencing Tokens**: Prevent stale or out-of-order requests from overriding valid holds.
- **PostgreSQL ACID Guarantee**: Database unique constraints (`reservations.confirmed_seats`) act as the definitive defense against double booking during payment settlement.

### 2. Transactional Outbox Pattern
- Database writes and outgoing event dispatches are committed within the same database transaction.
- Background worker processes asynchronously publish unpublished events to RabbitMQ, ensuring at-least-once delivery without distributed two-phase commit overhead.

### 3. POS Box Office and Ticket Invariants
- **Shift Reconciliation**: Cashiers must open shifts with a documented opening float and close shifts with a verified Z-report reconciliation.
- **Single-Print Invariant**: Box-office tickets have a strict physical print state. Once an original ticket stub has been printed, reprint attempts require supervisor authorization and are explicitly watermarked.
- **QR Code Gate Check-In**: Cryptographically signed ticket payloads ensure tickets cannot be forged and can only be redeemed once at the gate.


---

## Development and Testing

### Running Tests

Execute the unit test and integration test suites using the .NET CLI:

```powershell
# Run unit tests
dotnet test tests/Cinema.UnitTests/Cinema.UnitTests.csproj -c Release

# Run integration tests (requires Docker daemon for Testcontainers)
dotnet test tests/Cinema.IntegrationTests/Cinema.IntegrationTests.csproj -c Release

# Build entire solution
dotnet build CinemaPos.sln -c Release
```

### Validating Docker Compose Configuration

```powershell
docker compose config
```

---

## Repository Structure

```
Cinema-microservice/
├── .github/                      # CI/CD workflow automation
├── Admin UI/                     # Web admin dashboard (Astro + Tailwind CSS)
├── POS client UI/                # Desktop cashier application (WPF + .NET Core)
├── infra/
│   ├── postgres/                 # PostgreSQL initialization and schema migrations
│   └── redis/                    # Redis configuration and Lua scripts
├── src/
│   ├── BuildingBlocks/
│   │   └── Cinema.Foundation/    # Shared logging, auth, Redis, and event bus utilities
│   ├── Cinema.DbMigrator/        # Database migration runner and seed data
│   └── Services/
│       ├── Catalog.Api/          # Catalog and auditorium layouts service
│       ├── Gateway.Api/          # Central YARP gateway and portal
│       ├── Identity.Api/         # Authentication and RBAC service
│       ├── Loyalty.Api/          # Loyalty points and rewards service
│       ├── Pos.Api/              # Cashier, orders, and payment processing service
│       ├── Reservation.Api/      # Seat allocation and hold service
│       └── Ticket.Api/           # Ticket issuance and validation service
├── tests/
│   ├── Cinema.UnitTests/         # Invariant, security, and logic unit tests
│   └── Cinema.IntegrationTests/  # Real infrastructure tests via Testcontainers
├── docker-compose.yml            # Multi-container orchestration definition
├── docker-compose.prod.yml       # Production-tailored container specification
├── run.bat                       # Windows one-click startup script
├── run.ps1                       # PowerShell startup script
└── README.md                     # Project documentation
```

---

## License

This project is developed for educational and enterprise architectural demonstration purposes.
