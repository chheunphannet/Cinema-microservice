# Multi-Branch Cinema POS & Ticketing System — Phase 1 Foundation

Phase 1 Technical Architecture & Foundation for the **Multi-Branch Cinema Ticketing & Point of Sale (POS) Management System**.
This implementation strictly adheres to the **Phase 1 Technical Architecture Specification** and **Software Engineering Assignment Phase-1 Planning Document**.

---

## 🚀 Run with One Command

You can run the entire microservices stack (Gateway + 5 Microservices + PostgreSQL + Redis + Database & Cache Web UIs) using a single command:

### Option A: Windows Batch / Double-Click
```cmd
run.bat
```

### Option B: PowerShell
```powershell
.\run.ps1
```

### Option C: Docker Compose Direct
```bash
docker compose up --build
```

*(If `.env` does not exist, the scripts automatically copy `.env.example` to `.env`)*

---

## 🌐 Central Endpoints & Web UIs

Once started, everything is accessible on localhost:

### 1. 🎬 Central Gateway & API Documentation Portal
| Hub / Service | URL | Description |
|---|---|---|
| **Developer Landing Portal** | [http://localhost:8080](http://localhost:8080) | Interactive dashboard linking to all services, Web UIs, and documentation |
| **Unified Swagger UI Hub** | [http://localhost:8080/swagger](http://localhost:8080/swagger) | Centralized OpenAPI docs for all 5 microservices powered by YARP |

### 2. 🖥️ Database & Cache Web Management UIs
| Tool | URL | Description |
|---|---|---|
| **PostgreSQL Web UI (pgweb)** | [http://localhost:8086](http://localhost:8086) | Web GUI directly connected to PostgreSQL 16. Inspect canonical tables (`Branches`, `Movies`, `Seats`, `Transactions`), schemas (`catalog`, `reservations`, etc.), and run SQL queries. |
| **Redis Web UI (redis-commander)** | [http://localhost:8087](http://localhost:8087) | Web GUI connected to Redis 7. Inspect seat hold locks (`seat-hold:*`), TTL countdowns, read-through showtime caches, and Lua script keys. |

### 3. 📡 Direct Microservice Endpoints
Each microservice can also be accessed directly on its dedicated port (opening `/` automatically redirects to `/swagger`):

| Microservice | Port | Direct Swagger UI Link | OpenAPI JSON Specification | Responsibility |
|---|:---:|---|---|---|
| **Catalog API** | `8081` | [http://localhost:8081/swagger](http://localhost:8081/swagger) | `http://localhost:8081/swagger/v1/swagger.json` | Branches, auditoriums, movies catalog, showtimes schedules, visual seat maps |
| **Reservation API** | `8082` | [http://localhost:8082/swagger](http://localhost:8082/swagger) | `http://localhost:8082/swagger/v1/swagger.json` | Atomic seat holds (Redis Lua), 10-min TTL, fencing tokens, ACID booking confirmation |
| **POS API** | `8083` | [http://localhost:8083/swagger](http://localhost:8083/swagger) | `http://localhost:8083/swagger/v1/swagger.json` | Till shift float opening/closing (Z-report), split payments (cash/card/KHQR), concessions |
| **Ticket API** | `8084` | [http://localhost:8084/swagger](http://localhost:8084/swagger) | `http://localhost:8084/swagger/v1/swagger.json` | Digital ticket issuance, ESC/POS thermal stub printing (single-print invariant), gate QR redemption |
| **Identity API** | `8085` | [http://localhost:8085/swagger](http://localhost:8085/swagger) | `http://localhost:8085/swagger/v1/swagger.json` | Cashier PIN / credentials terminal login, branch staff directory, RBAC roles |

---

## ☕ Spring Boot Developer Guide: How Spring Cloud Maps to ASP.NET Core

If you are coming from a Spring Boot background, here is how the architectural building blocks map directly to .NET:

| Spring Boot / Spring Cloud Pattern | Modern ASP.NET Core / .NET 8+ Equivalent | How It Works in This Project |
|---|---|---|
| **Spring Cloud Gateway** (`RouteLocatorBuilder`) | **YARP (Yet Another Reverse Proxy)** (`Yarp.ReverseProxy`) | Microsoft's official high-performance gateway. Configured in `src/Services/Gateway.Api` to dispatch routes (e.g. `/api/v1/catalog/**`) to internal service containers. |
| **Netflix Eureka / Spring Cloud Discovery** (`@EnableEurekaServer`, `@EnableDiscoveryClient`) | **Container DNS & .NET Aspire Service Discovery** (`Microsoft.Extensions.ServiceDiscovery`) | Modern cloud-native pattern: Microservices communicate via Docker/Kubernetes container DNS (e.g., `http://catalog-api:8080`, `http://redis:6379`), eliminating JVM registry sync lag. |
| **Spring Cloud Config Server** (Git-backed config + `@RefreshScope`) | **`IConfiguration` & `IOptionsMonitor<T>`** + **Azure App Configuration / Consul** | Built into ASP.NET Core runtime. Configurations update live on file/environment changes without restarting the application process. |
| **Spring Data JPA / Hibernate** (Entities, `@Repository`) | **Entity Framework Core / Dapper / Npgsql** | Strongly-typed object-relational mapping and raw SQL DDL execution against PostgreSQL. |
| **Spring Cache + RedisTemplate** (`@Cacheable`) | **`IDistributedCache` + `StackExchange.Redis`** | Official caching interface in `Cinema.Foundation`, providing Redis distributed caching and custom Lua atomic locking scripts. |
| **Spring Actuator** (`/actuator/health`, `/actuator/metrics`) | **ASP.NET Core Health Checks** (`app.MapHealthChecks("/health")`) | Built-in readiness/liveness endpoints consumed by container orchestrators. |

---

## 🏗️ Phase 1 Architecture Compliance Checklist

### Module 1: PostgreSQL DDL Schema Definition
- **Canonical Module 1.1 Tables**: Implemented in [`infra/postgres/init.sql`](infra/postgres/init.sql) (`Branches`, `Movies`, `Seats`, `Transactions`) with `"uuid-ossp"` and `pgcrypto`.
- **Microservices Partitioned Schemas**: Implemented under `identity`, `catalog`, `reservations`, `pos`, and `tickets` schemas for clean bounded contexts.
- **Master-Slave Replication Model**: Writes execute on primary database; reads route to read replica with Read-After-Write consistency on primary.
- **Double-Booking Elimination**: Final ACID constraint via PostgreSQL unique constraints (`reservations.confirmed_seats`).

### Module 2: Redis Caching & Concurrency Policies
- **Read-Through Caching**: Cached seat availability and showtimes with low TTL and auto-invalidation on bookings.
- **Distributed Locking via Lua**: Atomic seat lock acquisition ([`infra/redis/lua/acquire-seat-hold.lua`](infra/redis/lua/acquire-seat-hold.lua)) and safe release ([`infra/redis/lua/release-seat-hold.lua`](infra/redis/lua/release-seat-hold.lua)).
- **Idempotency Keys**: Client-generated UUIDs on all write transactions to guard against network retries.
- **Monotonic Fencing Tokens**: Rejects stale delayed requests.
- **LRU Eviction Policy**: Configured `maxmemory-policy allkeys-lru` in [`infra/redis/redis.conf`](infra/redis/redis.conf).

### Module 3: ASP.NET Core Microservices Service Scaffolding
- **Stateless Web Tier**: Zero local in-memory session state; horizontal scale-out ready.
- **Distributed Cache Integration**: ASP.NET Core `IDistributedCache` wired to Redis in [`ServiceDefaults.cs`](src/BuildingBlocks/Cinema.Foundation/ServiceDefaults.cs).
- **Interactive Documentation**: Swashbuckle OpenAPI 3.0 enabled across all services with documentation and models.
- **API Gateway**: YARP reverse proxy dispatches requests from port `8080` to backend services.

### Module 4: Docker Containerization Strategy
- **Infrastructure Containers**: `docker-compose.yml` provisions PostgreSQL 16 Alpine and Redis 7 Alpine with automated health checks, plus `pgweb` (PostgreSQL UI) and `redis-commander` (Redis UI).
- **Testcontainers Workflow**: Integration test suite in [`tests/Cinema.IntegrationTests/`](tests/Cinema.IntegrationTests/) uses `GenericContainer` and `LogMessageWaitStrategy`.

### Module 5: CI/CD Pipeline & Quality Automation
- **Unified Solution**: [`CinemaPos.sln`](CinemaPos.sln) contains all building blocks, microservices, gateway, and test projects.
- **Automated Tests**:
  - `tests/Cinema.UnitTests/`: Business invariant tests (seat TTL, fencing tokens, one-time ticket printing, POS change & drawer kick).
  - `tests/Cinema.IntegrationTests/`: Testcontainers real infrastructure test.
- **GitHub Actions Workflow**: [`.github/workflows/ci.yml`](.github/workflows/ci.yml) validates build, tests, and container manifests on every PR/push.

---

## 🧪 Running Tests Locally

```bash
# Run unit tests
dotnet test tests/Cinema.UnitTests/Cinema.UnitTests.csproj

# Run all tests in solution
dotnet test CinemaPos.sln
```


