# Phase 3 Technical Architecture Specification
## Public Web Portal, Guest Checkout & Config-Driven Pluggable Email Notifications

**Document Version:** 1.0.0  
**Target Environment:** .NET 8, PostgreSQL 16, Redis 7, RabbitMQ 3.13, YARP Gateway, Mailpit / Cloud Email Providers  
**Target Audience:** Solution Architects, Backend Engineers, Frontend Engineers, DevOps & QA  
**Status:** Approved for Implementation  
**Companion Phase Documents:**
- Phase 1: Canonical Database & Shared Foundations
- Phase 2: Multi-Branch Cinema POS Backend Services

---

## 1. Executive Summary & Objectives

### 1.1 Business Context
The Cinema microservices platform in Phase 1 and Phase 2 successfully established the multi-branch box-office Point-Of-Sale (POS) system for on-site cashiers, complete with till shift management, physical receipt printing, hardware drawer kick signals, and staff RBAC (Role-Based Access Control) authentication.

However, modern cinema operations rely heavily on direct-to-consumer digital channels:
1. **Public Web Browsing:** Customers must be able to explore movies, genres, trailers, showtimes, auditoriums, and seat availability without having to register or log in.
2. **Frictionless Guest Checkout:** Modern e-commerce statistics indicate that forced account registration causes over 30% of cart abandonment. Spontaneous moviegoers must be permitted to reserve seats and complete purchases simply by entering an **email address** (and optional phone number), receiving their digital tickets immediately.
3. **Turnstile Admission without Account:** Guest ticket holders must be able to display their barcode/QR code at cinema gates or turnstiles using secure, cryptographically signed URLs without needing a password or login session.
4. **Config-Driven Pluggable Email System:** Notification dispatching must support a seamless **one-line configuration switch** in `appsettings.json` between **Open Source Local Testing** (Mailpit Docker container with visual web inbox) and **Enterprise Cloud Delivery** (Amazon SES, Resend, Postmark, SendGrid).

### 1.2 Key Architectural Goals
| Metric / Feature | Specification |
| :--- | :--- |
| **Catalog Browsing** | 100% Anonymous public access via YARP reverse proxy with sub-20ms Redis cached responses. |
| **Guest Checkout Friction** | Under 60 seconds from seat selection to payment confirmation; only email required. |
| **Inventory Guard** | Anti-hoarding distributed locks in Redis (8-minute TTL) with strict IP-based rate limiting. |
| **Gate Pass Security** | HMAC-SHA256 tamper-proof signed URLs preventing IDOR (Insecure Direct Object Reference). |
| **Email Architecture** | Factory DI pattern in .NET 8; switchable via `appsettings.json` `Email:Provider` setting. |
| **Local Dev Experience** | Fully offline, zero-cost, spam-safe verification via Mailpit (`http://localhost:8025`). |
| **Message Decoupling** | Asynchronous RabbitMQ publish/subscribe (`ticket.issued` topic exchange); checkout response does not wait for email transmission. |

---

## 2. System Architecture Overview

### 2.1 Dual-Channel Topology Diagram
```mermaid
flowchart TD
    subgraph Clients["Channel Entrypoints"]
        WebGuest["Public Web Customer (Guest / Anonymous)"]
        MobileApp["Customer Mobile App"]
        CashierPOS["Staff Box-Office POS Terminal (Bearer JWT)"]
    end

    subgraph Edge["API Gateway Layer (YARP)"]
        Yarp["YARP API Gateway (:8080)"]
        RateLimiter["IP Rate Limiter & Anti-Bot WAF-Lite"]
        AuthPolicy["Policy Engine (Anonymous vs Default JWT)"]
    end

    subgraph Services["Core Microservices (:8081 - :8085)"]
        CatalogSvc["Catalog Service (:8081)"]
        ReservationSvc["Reservation Service (:8082)"]
        PosSvc["POS & Web Checkout Service (:8083)"]
        TicketSvc["Ticket & Gate Service (:8084)"]
        IdentitySvc["Identity Service (:8085)"]
    end

    subgraph Messaging["Asynchronous Event Spine"]
        RabbitMQ["RabbitMQ Topic Exchange (cinema.events)"]
    end

    subgraph Notifications["Pluggable Notification Subsystem"]
        EmailWorker["Ticket Notification Background Worker"]
        EmailFactory["Cinema Email Factory (IEmailSender)"]
        MailpitDev["Mailpit Docker SMTP (:1025) / Web UI (:8025) [Local Dev]"]
        CloudProd["Cloud Service (Resend / AWS SES / Postmark) [Production]"]
    end

    subgraph Persistence["Storage & Caching"]
        Postgres[(PostgreSQL 16 Multi-Schema)]
        Redis[(Redis 7 Distributed Locks & Cache)]
        Seq[(Seq Structured Logging :5341)]
    end

    WebGuest -->|HTTP Requests| Yarp
    MobileApp -->|HTTP Requests| Yarp
    CashierPOS -->|HTTP Bearer Auth| Yarp

    Yarp --> RateLimiter --> AuthPolicy
    AuthPolicy -->|Anonymous| CatalogSvc
    AuthPolicy -->|Anonymous + Rate Limit| ReservationSvc
    AuthPolicy -->|Anonymous Web Checkout| PosSvc
    AuthPolicy -->|HMAC Signed URL / Staff Auth| TicketSvc
    AuthPolicy -->|Staff Login| IdentitySvc

    CatalogSvc --> Redis
    ReservationSvc --> Redis
    ReservationSvc --> Postgres
    PosSvc --> Postgres

    PosSvc -->|Publish ticket.issued| RabbitMQ
    RabbitMQ -->|Consume ticket.issued| EmailWorker
    EmailWorker --> EmailFactory

    EmailFactory -.->|Email:Provider = Mailpit| MailpitDev
    EmailFactory -.->|Email:Provider = Resend / AmazonSes| CloudProd

    EmailWorker -.->|Logs & Drawer/Delivery Traces| Seq
```

---

## 3. Gateway Route Policy & Security Specifications

### 3.1 Public vs Authenticated Route Rules
In Phase 2, `Gateway.Api/Program.cs` applied `AuthorizationPolicy = "default"` across all routes, rejecting any request missing a cashier/staff JWT Bearer token with `401 Unauthorized`. 

For Phase 3, YARP route policies are decoupled into **Public Anonymous Routes** (with strict DDoS/hoarding rate limiters) and **Protected Staff Routes**:

| Path Pattern | Target Cluster | Auth Policy | Rate Limiting Policy | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| `GET /api/v1/catalog/**` | `catalog-cluster` | `anonymous` | Global (100 req/10s) | Public movie browsing, schedules, auditoriums |
| `POST /api/v1/reservations/holds` | `reservation-cluster` | `anonymous` | Anti-Hoard (10 req/min/IP) | Anonymous guest seat temporary hold |
| `DELETE /api/v1/reservations/holds/**` | `reservation-cluster` | `anonymous` | Anti-Hoard (10 req/min/IP) | Release guest seat hold on cart cancel |
| `POST /api/v1/checkout/guest` | `pos-cluster` | `anonymous` | Checkout Limit (5 req/min/IP) | Guest checkout & payment initiation |
| `GET /api/v1/tickets/e-ticket/{id}` | `ticket-cluster` | `anonymous` | Standard (30 req/min/IP) | Signed URL gate pass viewer (HMAC validated) |
| `POST /api/v1/pos/payments/webhook` | `pos-cluster` | `anonymous` | Webhook Signature Check | Async payment gateway notification (KHQR/Stripe) |
| `POST /api/v1/pos/**` | `pos-cluster` | `default` (JWT) | Staff (300 req/min) | Staff box-office tills, shifts, cash drawer |
| `POST /api/v1/tickets/redeem` | `ticket-cluster` | `default` (JWT) | Gate Scanner (120 req/min) | Turnstile & gate staff barcode scan check-in |
| `POST /api/v1/identity/login` | `identity-cluster`| `anonymous` | Strict Auth (5 req/min/IP) | Cashier/Manager PIN login |

### 3.2 Inventory Hoarding & Bot Mitigation
Because anyone on the internet can call `POST /api/v1/reservations/holds`, attackers or rival theaters could attempt to lock all seats for blockbusters (Denial of Inventory). 

Mitigation mechanisms implemented in Phase 3:
1. **Aggressive Redis TTL:** Guest holds expire automatically after **8 minutes** (480 seconds). POS in-person cashier holds remain at 10 minutes.
2. **IP Partitioned Rate Limiter:** ASP.NET Core RateLimiter partitions by `context.Connection.RemoteIpAddress` (or `X-Forwarded-For` from reverse proxy). Maximum 10 hold requests per IP per minute.
3. **Idempotency Fingerprinting:** Every hold request requires an `X-Idempotency-Key` UUID. Repeated calls return the same hold without consuming new locks.
4. **Session Seat Cap:** Maximum of 8 seats per single reservation payload.

---

## 4. Database Schema Evolution (PostgreSQL)

To support guest checkout alongside existing registered accounts and staff POS checkouts, the database undergoes a zero-downtime additive migration.

### 4.1 Schema Migration (`003-guest-checkout.sql`)

```sql
-- =========================================================================
-- Schema Migration: Phase 3 Guest Checkout & Digital Delivery
-- =========================================================================

-- 1. Dual-Identity Enhancements for Reservations
ALTER TABLE reservations.reservations 
    ADD COLUMN IF NOT EXISTS guest_email VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS guest_phone VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS guest_name VARCHAR(150) NULL,
    ADD COLUMN IF NOT EXISTS is_guest BOOLEAN NOT NULL DEFAULT false;

-- Ensure either registered customer_id OR guest_email is supplied upon confirmation
ALTER TABLE reservations.reservations 
    DROP CONSTRAINT IF EXISTS chk_reservation_identity;

ALTER TABLE reservations.reservations 
    ADD CONSTRAINT chk_reservation_identity CHECK (
        (is_guest = false) OR 
        (is_guest = true AND guest_email IS NOT NULL)
    );

CREATE INDEX IF NOT EXISTS ix_reservations_guest_email 
    ON reservations.reservations(guest_email) 
    WHERE guest_email IS NOT NULL;

-- 2. POS Order Channel & Guest Attribution
ALTER TABLE pos.orders 
    ADD COLUMN IF NOT EXISTS channel VARCHAR(20) NOT NULL DEFAULT 'pos',
    ADD COLUMN IF NOT EXISTS customer_email VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS customer_phone VARCHAR(50) NULL;

ALTER TABLE pos.orders 
    DROP CONSTRAINT IF EXISTS chk_order_channel;

ALTER TABLE pos.orders 
    ADD CONSTRAINT chk_order_channel CHECK (
        channel IN ('pos', 'web', 'kiosk', 'mobile')
    );

-- For web orders, cashier_id is null; for pos orders, cashier_id is required upon completion
ALTER TABLE pos.orders 
    DROP CONSTRAINT IF EXISTS chk_order_cashier_channel;

ALTER TABLE pos.orders 
    ADD CONSTRAINT chk_order_cashier_channel CHECK (
        (channel = 'web') OR 
        (channel = 'pos' AND status = 'draft') OR 
        (channel = 'pos' AND cashier_id IS NOT NULL)
    );

-- 3. Digital Ticket Delivery Tracking
ALTER TABLE tickets.tickets 
    ADD COLUMN IF NOT EXISTS delivery_channel VARCHAR(20) NOT NULL DEFAULT 'print',
    ADD COLUMN IF NOT EXISTS email_recipient VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS email_sent BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS email_sent_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS email_message_id VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS access_hmac_secret VARCHAR(64) NULL;

CREATE INDEX IF NOT EXISTS ix_tickets_email_sent 
    ON tickets.tickets(email_sent) 
    WHERE email_sent = false;
```

---

## 5. Cryptographic E-Ticket Access Architecture (Zero-Login)

### 5.1 The Zero-Login Admission Problem
When a guest buys tickets online, they have no password or JWT token. When they arrive at the theater, they need to present a digital ticket on their phone screen for turnstile gate admission.
- **Vulnerability (IDOR):** If ticket URLs use simple IDs like `/tickets/12345`, anyone can increment the ID and steal entrance to other patrons' movies.
- **Solution:** **Cryptographically Signed URLs (HMAC-SHA256)**.

### 5.2 HMAC Signed Token Flow
```mermaid
sequenceDiagram
    autonumber
    actor Guest as Guest Mobile Browser
    participant Gate as API Gateway / Ticket Service
    participant Scanner as Turnstile Barcode Scanner
    participant DB as PostgreSQL (tickets.tickets)

    Note over Gate: Order completed; E-Ticket link generated
    Gate->>Gate: Generate HMAC-SHA256(ticket_id + ":" + exp, ServerSecret)
    Gate-->>Guest: Sends email with link: /e-ticket/{id}?exp=1789250000&sig=9a8b7c...

    Guest->>Gate: GET /api/v1/tickets/e-ticket/{id}?exp=1789250000&sig=9a8b7c...
    Gate->>Gate: Verify HMAC-SHA256 & verify exp > now()
    alt Signature Invalid or Expired
        Gate-->>Guest: 403 Forbidden ("Invalid or tampered ticket pass")
    else Signature Valid
        Gate->>DB: Fetch ticket, movie, auditorium, seat, and QR token
        Gate-->>Guest: 200 OK (Rendered Mobile Pass with dynamic QR)
    end

    Note over Guest,Scanner: Guest arrives at cinema gate
    Scanner->>Gate: POST /api/v1/tickets/redeem (QR Token + Staff Scanner JWT)
    Gate->>DB: Atomic UPDATE status = 'used' WHERE status = 'active'
    Gate-->>Scanner: 200 OK: Turnstile opens!
```

### 5.3 HMAC Verification Implementation
```csharp
public static class SignedTicketUrlService
{
    public static string GenerateSignature(Guid ticketId, long expiresAtUnix, string secretKey)
    {
        var payload = $"{ticketId}:{expiresAtUnix}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool ValidateSignature(Guid ticketId, long expiresAtUnix, string signature, string secretKey)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAtUnix)
            return false; // Expired pass

        var expectedSignature = GenerateSignature(ticketId, expiresAtUnix, secretKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expectedSignature)
        );
    }
}
```

---

## 6. Config-Driven Pluggable Email Notification Architecture

### 6.1 The 1-Line Configuration Switch
Enterprise production systems must run cost-free, spam-safe, and offline during development and automated CI/CD runs, but switch seamlessly to cloud SMTP/APIs in production.

By using the **Strategy & Factory Pattern** in .NET 8, switching between local open-source testing and production cloud email requires modifying **exactly one line** in `appsettings.json`:

```json
{
  "Email": {
    "Provider": "Mailpit", // <-- SWITCH HERE: "Mailpit" | "Resend" | "AmazonSes" | "Postmark" | "Smtp"
    
    "SenderEmail": "tickets@cinemacity.local",
    "SenderName": "Cinema City Digital Ticketing",

    "Mailpit": {
      "Host": "mailpit",
      "Port": 1025
    },

    "Resend": {
      "ApiKey": "re_1234567890abcdef"
    },

    "AmazonSes": {
      "Region": "ap-southeast-1",
      "AccessKey": "AKIAXXXXXXXXXXXXXXXX",
      "SecretKey": "YYYYYYYYYYYYYYYYYYYY"
    },

    "Smtp": {
      "Host": "smtp.postmarkapp.com",
      "Port": 587,
      "Username": "pm-token-xxx",
      "Password": "pm-token-xxx",
      "EnableSsl": true
    }
  }
}
```

### 6.2 Provider Comparison Matrix
| Feature | Local Dev: Mailpit | Cloud: Resend | Cloud: Amazon SES | Cloud: Postmark |
| :--- | :--- | :--- | :--- | :--- |
| **Cost** | 100% Free / Open Source | Free tier 3,000/mo ($20/mo after) | $0.10 per 1,000 emails | Free tier 100/mo ($15/mo after) |
| **Setup Complexity** | Zero credentials; 1 Docker container | 1 API Key | AWS IAM Policy & Domain Verification | 1 Server API Token |
| **Local Web UI** | Yes (`http://localhost:8025`) | Cloud Dashboard | CloudWatch / CloudTrail | Cloud Dashboard |
| **Spam/Blacklist Risk**| Zero (intercepted locally) | Managed IP pools | Requires warm-up & domain DNS | Best-in-class deliverability |
| **Protocol** | Standard SMTP (Port 1025) | HTTPS REST API | AWS SDK v3 / HTTPS | HTTPS REST API |

### 6.3 Core C# Interfaces & Dependency Injection

#### 1. Abstraction Contract (`IEmailService`)
```csharp
namespace Cinema.Foundation.Email;

public record EmailAttachment(string FileName, byte[] Content, string ContentType);

public record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null,
    List<EmailAttachment>? Attachments = null
);

public interface IEmailService
{
    Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public record EmailDispatchResult(bool Success, string? MessageId, string? ErrorMessage);
```

#### 2. Service Collection Registration Factory
```csharp
public static class EmailServiceExtensions
{
    public static IServiceCollection AddCinemaEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var emailSection = configuration.GetSection("Email");
        var provider = emailSection["Provider"]?.Trim() ?? "Mailpit";

        switch (provider.ToLowerInvariant())
        {
            case "mailpit":
                services.Configure<MailpitOptions>(emailSection.GetSection("Mailpit"));
                services.AddTransient<IEmailService, MailpitEmailService>();
                break;

            case "resend":
                services.Configure<ResendOptions>(emailSection.GetSection("Resend"));
                services.AddHttpClient<IEmailService, ResendEmailService>();
                break;

            case "amazonses":
                services.Configure<AmazonSesOptions>(emailSection.GetSection("AmazonSes"));
                services.AddTransient<IEmailService, AmazonSesEmailService>();
                break;

            case "smtp":
                services.Configure<SmtpOptions>(emailSection.GetSection("Smtp"));
                services.AddTransient<IEmailService, GenericSmtpEmailService>();
                break;

            default:
                throw new InvalidOperationException($"Unsupported Email:Provider '{provider}'. Expected 'Mailpit', 'Resend', 'AmazonSes', or 'Smtp'.");
        }

        return services;
    }
}
```

#### 3. Mailpit Driver (Using MailKit)
```csharp
public class MailpitEmailService : IEmailService
{
    private readonly MailpitOptions _options;
    private readonly IConfiguration _config;
    private readonly ILogger<MailpitEmailService> _logger;

    public MailpitEmailService(IOptions<MailpitOptions> options, IConfiguration config, ILogger<MailpitEmailService> logger)
    {
        _options = options.Value;
        _config = config;
        _logger = logger;
    }

    public async Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_config["Email:SenderName"] ?? "Cinema City", _config["Email:SenderEmail"] ?? "tickets@cinemacity.local"));
        mimeMessage.To.Add(new MailboxAddress(message.ToName, message.ToEmail));
        mimeMessage.Subject = message.Subject;

        var builder = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.PlainTextBody };
        if (message.Attachments != null)
        {
            foreach (var att in message.Attachments)
            {
                builder.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.ContentType));
            }
        }
        mimeMessage.Body = builder.ToMessageBody();

        using var client = new MailKit.Net.Smtp.SmtpClient();
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.None, cancellationToken);
            var response = await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email dispatched to Mailpit successfully for {Recipient}. Subject: {Subject}", message.ToEmail, message.Subject);
            return new EmailDispatchResult(true, Guid.NewGuid().ToString(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via Mailpit at {Host}:{Port}", _options.Host, _options.Port);
            return new EmailDispatchResult(false, null, ex.Message);
        }
    }
}
```

---

## 7. Asynchronous Event-Driven Email Pipeline

### 7.1 Event Flow & Sequence
Email sending involves network I/O and HTML/QR rendering that must never block the synchronous HTTP checkout response.

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Guest Customer
    participant CheckoutApi as POS / Checkout API
    participant Rabbit as RabbitMQ (cinema.events)
    participant Worker as Notification Worker (Ticket.Api)
    participant QR as QR Generator (QRCoder)
    participant EmailSvc as IEmailService (Mailpit/Resend)
    participant Mailpit as Mailpit Web UI (:8025)

    Customer->>CheckoutApi: POST /api/v1/checkout/guest (Email, Card/Payment Ref, ReservationId)
    CheckoutApi->>CheckoutApi: Settle payment & confirm reservation in DB
    CheckoutApi->>Rabbit: Publish ticket.issued (OrderId, ReservationId, CustomerEmail, Total)
    CheckoutApi-->>Customer: 200 OK { OrderId, Status: "paid", Message: "Tickets sent to email" }

    Note over Rabbit,Worker: Asynchronous Event Processing
    Rabbit->>Worker: Consume ticket.issued event
    Worker->>Worker: Fetch Ticket & Showtime Details from DB
    Worker->>QR: Generate SVG/PNG Barcode & QR Code Base64
    Worker->>Worker: Generate HMAC-SHA256 Signed URL for web pass
    Worker->>Worker: Render Responsive HTML Ticket Template
    Worker->>EmailSvc: SendAsync(EmailMessage)
    EmailSvc->>Mailpit: Dispatches SMTP to port 1025
    Mailpit-->>Mailpit: Appears in Mailpit Web Inbox at http://localhost:8025
```

### 7.2 Integration Event Contract
```csharp
namespace Cinema.Foundation.Messaging;

public sealed record TicketIssuedIntegrationEvent(
    Guid OrderId,
    Guid ReservationId,
    string CustomerEmail,
    string? CustomerPhone,
    string? CustomerName,
    string MovieTitle,
    string BranchName,
    string AuditoriumName,
    DateTimeOffset ShowtimeStart,
    List<TicketSeatItem> Seats,
    decimal TotalAmount,
    DateTimeOffset IssuedAt
);

public sealed record TicketSeatItem(
    Guid TicketId,
    string RowLabel,
    int SeatNumber,
    string SeatType,
    string QrToken
);
```

---

## 8. Docker Infrastructure Updates

### 8.1 Adding Mailpit to `docker-compose.yml`

```yaml
  mailpit:
    image: axllent/mailpit:latest
    container_name: cinema-mailpit
    restart: unless-stopped
    ports:
      - "8025:8025"   # Mailpit Web UI for viewing captured emails
      - "1025:1025"   # Mailpit SMTP port for microservice traffic
    environment:
      MP_MAX_MESSAGES: 500
      MP_DATABASE: ""  # In-memory storage (fast, auto-cleans on restart)
      MP_SMTP_AUTH_ACCEPT_ANY: 1
      MP_TAG: "cinema-pos"
    networks:
      - cinema_net
    deploy:
      resources:
        limits:
          cpus: '0.2'
          memory: 128M
```

### 8.2 Environment Variable Injection
Update the shared `&service-env` block in `docker-compose.yml`:
```yaml
    Email__Provider: ${EMAIL_PROVIDER:-Mailpit}
    Email__SenderEmail: tickets@cinemacity.local
    Email__SenderName: "Cinema City Box Office"
    Email__Mailpit__Host: mailpit
    Email__Mailpit__Port: 1025
    Email__Resend__ApiKey: ${RESEND_API_KEY:-}
```

---

## 9. HTML E-Ticket Email Template Specification

### 9.1 Visual Layout Architecture
The transactional email must be mobile-responsive, adhering to email client rendering constraints (inline styles, table-based layouts for Outlook compatibility, dark cinematic theme).

```
+-------------------------------------------------------------+
|                     CINEMA CITY LOGO                        |
|             Downtown Mall - Auditorium 1 (IMAX)             |
+-------------------------------------------------------------+
|                                                             |
|   MOVIE: DUNE: PART TWO                                     |
|   DATE: Saturday, Oct 24, 2026 at 7:30 PM                   |
|   SEATS: Row F, Seats 12 & 13 (VIP Recliner)                |
|   BOOKING REF: #ORD-98412-GUEST                             |
|                                                             |
+-------------------------------------------------------------+
|                                                             |
|                       [ QR CODE IMAGE ]                     |
|                  Scan this at Gate Turnstile                |
|                                                             |
|   [ VIEW DIGITAL E-TICKET BUTTON (Signed HMAC Pass) ]       |
|                                                             |
+-------------------------------------------------------------+
|   Need to make changes? Call (855) 23 888 999               |
|   Do not share this email. It serves as your official pass. |
+-------------------------------------------------------------+
```

### 9.2 Critical Email Implementation Constraints
1. **QR Code Delivery:** Embed QR code using Base64 Data URI (`<img src="data:image/png;base64,..." />`) with a fallback CID inline MIME attachment for strict email clients like Gmail Web.
2. **Instant Pass Button:** Direct hyperlink to the HMAC-signed mobile viewer:
   `https://cinemacity.com/tickets/pass/{ticketId}?sig={hmac}&exp={unixTimestamp}`.
3. **Calendar Integration:** Attach a standard `.ics` (iCalendar) file allowing customers to add the showtime directly to Google Calendar or Apple Calendar with one tap.

---

## 10. Phase 3 Step-by-Step Implementation Roadmap

```mermaid
gantt
    title Phase 3 Implementation Roadmap
    dateFormat  YYYY-MM-DD
    section Infrastructure & Core
    Add Mailpit to Docker & verify :task1, 2026-09-13, 1d
    Run PostgreSQL Schema Migration 003 :task2, after task1, 1d
    Implement Cinema.Foundation.Email & Factory :task3, after task2, 2d
    section Gateway & Web Checkout
    Update YARP Gateway with Anonymous Routes :task4, after task2, 1d
    Implement Anti-Hoard IP Rate Limiter :task5, after task4, 1d
    Create Guest Hold & Checkout Endpoints :task6, after task5, 2d
    section Notifications & Gate Admission
    Implement RabbitMQ Event Consumer for Tickets :task7, after task3, 2d
    Implement HMAC Signed E-Ticket Endpoints :task8, after task6, 1d
    QR Code & HTML Email Template Engine :task9, after task7, 2d
    section Verification & Delivery
    Swagger & Mailpit End-to-End Verification :task10, after task9, 1d
    Seq Structured Logging & Performance Gates :task11, after task10, 1d
```

### Detailed Task Breakdown:
- **Task 1: Mailpit Docker Integration**
  - Add `mailpit` container to `docker-compose.yml`.
  - Expose ports `8025` (Web UI) and `1025` (SMTP).
- **Task 2: Database Migration**
  - Apply `003-guest-checkout.sql` adding `guest_email`, `guest_phone`, `is_guest` to `reservations.reservations`, `channel` to `pos.orders`, and digital delivery flags to `tickets.tickets`.
- **Task 3: Pluggable Email Foundation**
  - Implement `IEmailService`, `MailpitEmailService`, `ResendEmailService`, and `EmailServiceExtensions`.
  - Add `Email` config block to `appsettings.json` with 1-line provider switch.
- **Task 4: Gateway Routing & Anonymous Policies**
  - Modify `Gateway.Api/Program.cs` to set `AuthorizationPolicy = "anonymous"` for public catalog and guest checkout routes.
  - Implement Partitioned Rate Limiter for seat holds.
- **Task 5: Guest Reservation & Checkout Endpoints**
  - Add `POST /api/v1/reservations/holds/guest` accepting `showtimeId`, `seatIds`, `guestEmail`.
  - Add `POST /api/v1/checkout/guest` processing payments and issuing tickets.
- **Task 6: Asynchronous Event Consumer & Mail Delivery**
  - Implement `TicketNotificationSubscriber` consuming `ticket.issued` from RabbitMQ.
  - Invert HTML rendering & dispatch to `IEmailService`.
- **Task 7: Cryptographic E-Ticket Viewer**
  - Add `GET /api/v1/tickets/e-ticket/{ticketId}` with HMAC validation.
- **Task 8: End-to-End Testing & Verification**
  - Test checkout without auth in Swagger UI.
  - Inspect received HTML tickets in Mailpit at `http://localhost:8025`.
  - Verify structured logs and traces in Seq (`http://localhost:5341`) and Jaeger (`http://localhost:16686`).

---

## 11. Testing & Verification Playbook

### 11.1 Local Mailpit Verification Checklist
1. **Launch Stack:** Run `docker compose up -d`.
2. **Access Mailpit:** Open browser to `http://localhost:8025`. Mailpit inbox should be clean and responsive.
3. **Execute Anonymous Flow in Swagger (`http://localhost:8080/swagger`):**
   - Call `GET /api/v1/catalog/movies` without Bearer token -> Expect `200 OK`.
   - Call `POST /api/v1/reservations/holds` with guest payload -> Expect `200 OK` (Seat locked in Redis).
   - Call `POST /api/v1/checkout/guest` with `guest_email: "moviegoer@gmail.com"` -> Expect `200 OK`.
4. **Inspect Received Email in Mailpit:**
   - Switch to `http://localhost:8025`.
   - The ticket confirmation email should appear immediately with:
     - Correct recipient (`moviegoer@gmail.com`).
     - Movie title, auditorium name, seat numbers.
     - Embedded readable QR code.
     - Clickable HMAC-signed e-ticket link.
5. **Test Signed E-Ticket Link:**
   - Click the link in the email or paste in a private browsing window without login.
   - Verify that the web ticket displays.
   - Modify one character of the `sig` query parameter -> Verify that the gateway immediately returns `403 Forbidden`.
6. **Verify Seq Structured Logs:**
   - Open `http://localhost:5341`.
   - Query: `@Message like '%Email dispatched%' or @Message like '%ticket.issued%'`.
   - Confirm complete end-to-end correlation IDs across Gateway, POS, RabbitMQ, and Notification Worker.

---

## 12. Conclusion & Next Steps
This Phase 3 specification equips the Cinema Microservice platform with true commercial-grade public customer capabilities:
- **Zero barrier to entry** for online moviegoers via frictionless guest checkout.
- **Bulletproof security** for gate admission via HMAC cryptographic signatures without requiring user accounts.
- **Developer-friendly elegance** via a 1-line configuration switch between local Mailpit testing and cloud production providers.

Ready to proceed with execution starting from Task 1 (Mailpit Docker integration & schema migration).
