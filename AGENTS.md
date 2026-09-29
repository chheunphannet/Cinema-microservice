# Cinema POS — Engineering Rules

## Project context

- Stack: ASP.NET Core Minimal APIs on .NET 8, Dapper/Npgsql with PostgreSQL,
  Redis distributed locking/cache, RabbitMQ integration events, YARP gateway,
  OpenTelemetry/Serilog, Docker Compose, xUnit and Testcontainers.
- Services: `Catalog.Api`, `Reservation.Api`, `Pos.Api`, `Ticket.Api`,
  `Identity.Api`, `Loyalty.Api`, and `Gateway.Api`.
- Shared cross-cutting code belongs in `src/BuildingBlocks/Cinema.Foundation`.
- Database schema and SQL migrations belong in `infra/postgres`.

## Feature completion rule

Do not describe a backend feature as complete merely because its endpoint works
locally. Before moving to another feature, complete the following checks that
apply to the change.

### 1. Design and API contract

- Keep public HTTP APIs under `/api/v1` and use clear request/response DTOs.
- Update endpoint summaries/descriptions and the OpenAPI contract when an API
  changes. Do not silently break a POS or web-UI consumer.
- Validate request data at the boundary and return suitable HTTP status codes
  and `ProblemDetails` errors; do not expose exception details or secrets.
- Preserve idempotency for all state-changing, retryable operations. Use and
  document `X-Idempotency-Key` where a client or payment provider can retry.

### 2. Security and authorization

- Default all staff and administrative endpoints to authenticated. Use explicit
  authorization policies/role and branch checks for privileged operations;
  authentication alone is insufficient.
- An anonymous endpoint must be deliberately public, rate-limited where
  appropriate, and must not disclose or mutate data based only on a guessable
  ID. Customer access must use an authenticated identity or a signed,
  expiring, purpose-limited token.
- Never add production secrets, private keys, payment credentials, connection
  strings, or real customer data to source control, logs, Swagger examples, or
  Docker defaults. Read secrets from configuration/secret stores.
- Payment webhooks must fail closed if their secret/configuration is absent,
  verify signatures in constant time, and reject replayed requests.

### 3. Data correctness and concurrency

- All multi-step PostgreSQL writes use a transaction and have a defined
  rollback path.
- Preserve the booking invariants: Redis holds are advisory and expiring;
  PostgreSQL constraints/transactions remain the final double-booking guard.
- Do not trust price, role, staff ID, branch ID, or payment state received from
  a UI. Recalculate/authorize on the server.
- Make Dapper SQL parameterized. Never build SQL using user-provided strings.
- For schema changes, add a forward-only, ordered, idempotent SQL migration in
  `infra/postgres`; do not edit historical migrations. Include required
  indexes, constraints, and backfill/rollout notes.

### 4. Messaging and external effects

- Events that cause tickets, email, loyalty, or payment state changes must be
  safe under duplicate delivery and retries.
- Do not replace a failed production RabbitMQ delivery with an in-memory
  fallback. Record/retry/reconcile durable failures.
- For a new event, define its routing key, payload version, producer,
  consumer, idempotency key, retry behavior, and dead-letter/reconciliation
  plan.

### 5. Observability and operations

- Add structured logs without tokens, PINs, passwords, card data, webhook
  signatures, or customer PII. Include correlation ID and relevant entity IDs.
- Add metrics/traces for new critical paths and make errors actionable.
- Keep health checks meaningful: liveness must not depend on external systems;
  readiness must verify the dependencies required to accept traffic.
- Any new configuration key needs a safe development example and documented
  production source. No insecure production fallback value.

### 6. Tests and verification

- Add or update unit tests for business rules, authorization, validation, and
  failure paths.
- Add integration tests for database constraints, Redis locking/idempotency,
  and message behavior when the feature crosses those boundaries.
- For booking/payment/ticket workflows, test duplicate requests, concurrent
  requests, timeout/retry behavior, and service-restart behavior.
- Before handoff, run the narrow tests for the feature and then:

  ```powershell
  dotnet build CinemaPos.sln -c Release
  dotnet test tests/Cinema.UnitTests/Cinema.UnitTests.csproj -c Release --no-build
  dotnet test tests/Cinema.IntegrationTests/Cinema.IntegrationTests.csproj -c Release --no-build
  docker compose config
  ```

- Report commands that were not run and why. Do not claim coverage, latency,
  security, or production readiness without evidence.

### 7. Review and handoff

- Self-review the changed files before asking for review.
- In the handoff, state: behavior delivered, API/database/event changes,
  authorization impact, tests run, known risks, and deployment/migration
  ordering.
- A feature is ready for POS UI or web UI integration only when its API
  contract is stable, auth behavior is specified, error cases are documented,
  and a deployed staging environment supports the workflow.

## Change discipline

- Make the smallest coherent change; avoid unrelated refactors.
- Keep services within their bounded context. Reuse `Cinema.Foundation` only
  for genuinely shared infrastructure concerns.
- Do not modify generated `bin/`, `obj/`, or `publish/` output.
- Do not run destructive database, Docker volume, or Git commands without
  explicit user approval.
