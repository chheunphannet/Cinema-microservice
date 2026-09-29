# Cinema POS — Microservices API Documentation

> **Target Audience:** POS Client UI Developers (Web, Desktop, Touchscreen Terminals, Mobile Handheld Scanners)  
> **API Gateway Base URL:** `http://localhost:8080` (YARP Reverse Proxy)  
> **Environment:** Development / Staging / Production  
> **Version:** 1.0 (API `/api/v1`)

---

## Table of Contents

1. [Architecture & Gateway Topology](#1-architecture--gateway-topology)
2. [Global Standards & Conventions](#2-global-standards--conventions)
   - [Authentication & JWT Headers](#authentication--jwt-headers)
   - [Idempotency (`X-Idempotency-Key`)](#idempotency-x-idempotency-key)
   - [Supervisor Override PIN (`X-Supervisor-Pin`)](#supervisor-override-pin-x-supervisor-pin)
   - [Standard Error Format (RFC 7807)](#standard-error-format-rfc-7807)
3. [End-to-End POS Workflows](#3-end-to-end-pos-workflows)
   - [Workflow A: Cashier Till Shift Lifecycle](#workflow-a-cashier-till-shift-lifecycle)
   - [Workflow B: Box Office Ticket Sales & Booking](#workflow-b-box-office-ticket-sales--booking)
   - [Workflow C: F&B Concessions & Counter Fulfillment](#workflow-c-fb-concessions--counter-fulfillment)
   - [Workflow D: Gate Admission & Turnstile Ticket Scanning](#workflow-d-gate-admission--turnstile-ticket-scanning)
   - [Workflow E: Customer Loyalty & Promotional Vouchers](#workflow-e-customer-loyalty--promotional-vouchers)
4. [Microservices Endpoint Reference](#4-microservices-endpoint-reference)
   - [1. Identity & Staff Management API (`Identity.Api`)](#1-identity--staff-management-api)
   - [2. Catalog & Showtimes API (`Catalog.Api`)](#2-catalog--showtimes-api)
   - [3. Reservations & Seat Locking API (`Reservation.Api`)](#3-reservations--seat-locking-api)
   - [4. Point of Sale & Checkout API (`Pos.Api`)](#4-point-of-sale--checkout-api)
   - [5. Ticket Issuance & Gate Scanning API (`Ticket.Api`)](#5-ticket-issuance--gate-scanning-api)
   - [6. Loyalty, CRM & Vouchers API (`Loyalty.Api`)](#6-loyalty-crm--vouchers-api)
5. [TypeScript Integration Helper](#5-typescript-integration-helper)

---

## 1. Architecture & Gateway Topology

All client requests from POS touchscreens, kiosks, and scanners connect directly to the **API Gateway** on port `8080`. The gateway terminates incoming connections and reverse-proxies requests to the downstream microservices using high-performance YARP routing.

```
+-------------------------------------------------------------------------------+
|                           POS CLIENT UI APPS                                  |
|   (Cashier Touchscreen Terminal / Self-Service Kiosk / Gate Barcode Scanner)  |
+-------------------------------------------------------------------------------+
                                      |
                           HTTP/1.1 REST (/api/v1)
                                      v
+-------------------------------------------------------------------------------+
|                       API GATEWAY (Port 8080 - YARP)                          |
+-------------------------------------------------------------------------------+
     |              |              |              |              |              |
     v              v              v              v              v              v
+----------+  +-----------+  +-----------+  +----------+  +----------+  +----------+
| Identity |  |  Catalog  |  |Reservation|  | POS / F&B|  |  Ticket  |  | Loyalty  |
| Service  |  |  Service  |  |  Service  |  | Service  |  | Service  |  | Service  |
|  (Auth,  |  |  (Movies, |  |   (Redis  |  | (Orders, |  |(Issuance,|  | (Points, |
|  Staff,  |  | Showtimes,|  |    Holds, |  | Payments,|  | Printing,|  | Member   |
|  Roles)  |  | Seat Map) |  |   Locks)  |  | Shifts)  |  | Scans)   |  | Accounts)|
+----------+  +-----------+  +-----------+  +----------+  +----------+  +----------+
```

---

## 2. Global Standards & Conventions

### Authentication & JWT Headers

Endpoints requiring authorization require the `Authorization` header with a signed HMAC-SHA256 Bearer JWT:

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

#### JWT Claims Schema
| Claim | Type | Description |
|---|---|---|
| `sub` / `nameid` | UUID | User ID |
| `unique_name` | string | Staff username (e.g. `cashier1`, `admin`) |
| `display_name`| string | Human-readable staff name |
| `role` | string | Security role: `cashier`, `supervisor`, `branch_manager`, `system_admin`, `super_admin`, `customer` |
| `branch_id` / `branchId` | UUID | Assigned cinema branch ID (used for multi-tenant tenant isolation) |

### Idempotency (`X-Idempotency-Key`)

All state-changing, financial, or hold-allocating operations (`/orders`, `/payments`, `/holds`) support or require an **Idempotency Key** to protect against network drops, retries, or double-clicks:

```http
X-Idempotency-Key: 3fa85f64-5717-4562-b3fc-2c963f66afa6
```
- Must be a unique **UUIDv4**.
- If a client retries with the same key within 24 hours, the server returns the cached initial response without charging or allocating twice.

### Supervisor Override PIN (`X-Supervisor-Pin`)

Privileged operations (such as re-printing already printed thermal tickets or voiding transactions) require a supervisor PIN. It can be provided in the JSON body or in the header:

```http
X-Supervisor-Pin: 9999
```

### Standard Error Format (RFC 7807)

All validation, business invariant, and network errors return standard `application/problem+json`:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "SeatIds count must be between 1 and 10.",
  "traceId": "00-84fa734b46c64188b024479e0a0d9e80-60b64d0bc7b0559e-00"
}
```

---

## 3. End-to-End POS Workflows

### Workflow A: Cashier Till Shift Lifecycle

```mermaid
sequenceDiagram
    autonumber
    actor Cashier
    participant UI as POS Client UI
    participant Identity as Identity.Api
    participant POS as Pos.Api

    Cashier->>UI: Enter Username & PIN
    UI->>Identity: POST /api/v1/identity/login
    Identity-->>UI: 200 OK (JWT Token, User Details, BranchId)

    Cashier->>UI: Enter Opening Cash Float ($100.00)
    UI->>POS: POST /api/v1/pos/shifts/open
    POS-->>UI: 201 Created (ShiftId, Status="open")

    Note over UI,POS: Cashier handles orders & payments throughout shift

    Cashier->>UI: End of Day: Count Drawer Cash ($450.00)
    UI->>POS: POST /api/v1/pos/shifts/close
    POS-->>UI: 200 OK (Discrepancy calculation, Status="closed")
```

---

### Workflow B: Box Office Ticket Sales & Booking

```mermaid
sequenceDiagram
    autonumber
    actor Customer
    actor Cashier
    participant UI as POS Client UI
    participant Catalog as Catalog.Api
    participant Res as Reservation.Api
    participant POS as Pos.Api
    participant Ticket as Ticket.Api

    Cashier->>UI: Select Showtime & Movie
    UI->>Catalog: GET /api/v1/catalog/seat-map/{showtimeId}
    Catalog-->>UI: 200 OK (Auditorium Seat Grid, Status: available/held/booked)

    Customer->>Cashier: Chooses Seats A1, A2
    UI->>Res: POST /api/v1/reservations/holds (SeatIds, ShowtimeId, X-Idempotency-Key)
    Res-->>UI: 200 OK (HoldId, HoldExpiresAt = 10 min, FencingToken)

    Cashier->>UI: Add F&B Snacks (e.g., Popcorn Combo)
    UI->>POS: POST /api/v1/pos/orders (ReservationId, Lines, BranchId)
    POS-->>UI: 200 OK (OrderId, TotalAmount = $22.50)

    Customer->>Cashier: Pays with Cash ($30.00)
    UI->>POS: POST /api/v1/pos/orders/{orderId}/payments (Method="cash", Amount=30.00)
    POS-->>UI: 200 OK (TotalPaid=30.00, ChangeGiven=7.50, Status="paid")

    UI->>Ticket: POST /api/v1/tickets/issue (ReservationId, ShowtimeId, SeatIds)
    Ticket-->>UI: 201 Created (List of TicketIds with Cryptographic QR Hash)

    UI->>Ticket: POST /api/v1/tickets/{ticketId}/print
    Ticket-->>UI: 200 OK (Raw ESC/POS Thermal byte stream to printer)
```

---

### Workflow C: F&B Concessions & Counter Fulfillment

```mermaid
sequenceDiagram
    autonumber
    actor Staff as Concession Staff
    participant UI as Handheld / Counter Scanner
    participant POS as Pos.Api

    Staff->>UI: Scan Customer Email / App QR (e.g. CINEMA-FNB:..., CINEMA-ORDER:...)
    UI->>POS: POST /api/v1/pos/orders/scan-concessions { barcodeOrToken, autoFulfill: true }
    POS-->>UI: 200 OK (Items: 1x Large Caramel Popcorn, 2x Coke, Status="collected")
    Staff->>Staff: Hand food & drinks to customer
```

---

### Workflow D: Gate Admission & Turnstile Ticket Scanning

```mermaid
sequenceDiagram
    autonumber
    actor Attendant as Gate Attendant
    participant UI as Turnstile / Scanner App
    participant Ticket as Ticket.Api

    Attendant->>UI: Scan Ticket QR Code
    UI->>Ticket: POST /api/v1/tickets/redeem { qrToken }
    alt Valid & Active Ticket
        Ticket-->>UI: 200 OK (Status="used", MovieTitle, Seat, Hall)
        UI->>Attendant: Green Light (Admit Customer)
    else Already Used
        Ticket-->>UI: 409 Conflict ("Ticket has already been redeemed at 19:15")
        UI->>Attendant: Red Light (Reject Admission)
    else Voided / Cancelled
        Ticket-->>UI: 400 Bad Request ("Ticket has been voided")
        UI->>Attendant: Red Light (Reject Admission)
    end
```

---

## 4. Microservices Endpoint Reference

### 1. Identity & Staff Management API

Service responsible for staff credentials, cashier terminal logins, supervisor PIN overrides, and RBAC token generation.

#### 1.1 Staff / Cashier PIN Login
- **Endpoint:** `POST /api/v1/identity/login`
- **Auth:** Anonymous
- **Description:** Authenticates cashier or supervisor via terminal touchscreen numeric PIN or alphanumeric password.

**Request Body:**
```json
{
  "username": "cashier1",
  "pinOrPassword": "1234"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "11111111-2222-3333-4444-555555555555",
  "username": "cashier1",
  "displayName": "John Doe (Cashier)",
  "roles": ["cashier"],
  "branchId": "b1000000-0000-0000-0000-000000000001",
  "expiresAt": "2026-09-26T23:59:59Z"
}
```

---

#### 1.2 Verify Supervisor Override PIN
- **Endpoint:** `POST /api/v1/identity/verify-supervisor`
- **Auth:** Required (`Bearer <token>`)
- **Description:** Securely validates a supervisor PIN for administrative overrides, ticket voiding, or reprints.

**Request Body:**
```json
{
  "supervisorPin": "9999",
  "branchId": "b1000000-0000-0000-0000-000000000001"
}
```

**Response (200 OK):**
```json
{
  "isValid": true,
  "supervisorId": "88888888-9999-aaaa-bbbb-cccccccccccc",
  "displayName": "Jane Smith (Supervisor)"
}
```

---

#### 1.3 List Staff Users
- **Endpoint:** `GET /api/v1/identity/users`
- **Auth:** Required (Roles: `branch_manager`, `system_admin`, `super_admin`)
- **Description:** Retrieves all staff members assigned to the current branch.

---

### 2. Catalog & Showtimes API

High-performance read-through cached service providing branches, movies, showtime schedules, and interactive auditorium seat maps.

#### 2.1 Get Active Cinema Branches
- **Endpoint:** `GET /api/v1/catalog/branches`
- **Auth:** Anonymous / Cached

**Response (200 OK):**
```json
[
  {
    "branchId": "b1000000-0000-0000-0000-000000000001",
    "name": "Legend 271 Mega Mall",
    "location": "Phnom Penh, Cambodia",
    "isActive": true
  },
  {
    "branchId": "b1000000-0000-0000-0000-000000000002",
    "name": "Legend Eden Garden",
    "location": "Phnom Penh, Cambodia",
    "isActive": true
  }
]
```

---

#### 2.2 List Showtimes Schedules
- **Endpoint:** `GET /api/v1/catalog/showtimes?branchId={branchId}&movieId={movieId}`
- **Auth:** Anonymous / Cached
- **Query Parameters:**
  - `branchId` (UUID, optional): Filter by cinema branch.
  - `movieId` (UUID, optional): Filter by specific movie.

**Response (200 OK):**
```json
[
  {
    "showtimeId": "c1000000-0000-0000-0000-000000000001",
    "movieId": "d1000000-0000-0000-0000-000000000001",
    "movieTitle": "Avengers: Endgame - Encore",
    "branchId": "b1000000-0000-0000-0000-000000000001",
    "branchName": "Legend 271 Mega Mall",
    "auditoriumId": "a1000000-0000-0000-0000-000000000001",
    "auditoriumName": "Hall 1 (ScreenX + Dolby Atmos)",
    "screenType": "SCREENX",
    "startTime": "2026-09-26T18:30:00Z",
    "endTime": "2026-09-26T21:33:00Z",
    "basePrice": 6.50,
    "status": "Active"
  }
]
```

---

#### 2.3 Get Interactive Seat Map
- **Endpoint:** `GET /api/v1/catalog/seat-map/{showtimeId}`
- **Auth:** Anonymous
- **Description:** Generates the complete 2D auditorium seat grid with live seat status (`available`, `held`, `booked`, `blocked`).

**Response (200 OK):**
```json
{
  "showtimeId": "c1000000-0000-0000-0000-000000000001",
  "auditoriumId": "a1000000-0000-0000-0000-000000000001",
  "auditoriumName": "Hall 1",
  "totalSeats": 120,
  "availableSeats": 98,
  "seats": [
    {
      "seatId": "e1000000-0000-0000-0000-000000000001",
      "row": "A",
      "seatNumber": 1,
      "seatType": "Standard",
      "price": 6.50,
      "status": "available"
    },
    {
      "seatId": "e1000000-0000-0000-0000-000000000002",
      "row": "A",
      "seatNumber": 2,
      "seatType": "Standard",
      "price": 6.50,
      "status": "held"
    },
    {
      "seatId": "e1000000-0000-0000-0000-000000000003",
      "row": "A",
      "seatNumber": 3,
      "seatType": "VIP",
      "price": 9.00,
      "status": "booked"
    }
  ]
}
```

---

#### 2.4 Get Ticket Demographic Types
- **Endpoint:** `GET /api/v1/catalog/ticket-types`
- **Auth:** Anonymous
- **Description:** Returns price modifiers and ticket demographics (Adult, Student, Child, Senior).

---

### 3. Reservations & Seat Locking API

Manages atomic seat holding via Redis Lua distributed locks and transactional double-booking prevention in PostgreSQL.

#### 3.1 Acquire Atomic Seat Hold
- **Endpoint:** `POST /api/v1/reservations/holds`
- **Header:** `X-Idempotency-Key` (UUIDv4 recommended)
- **Auth:** Anonymous / Cashier
- **Rules:**
  - Holds seats for **10 minutes**.
  - Limits: 1 to 10 seats per transaction.
  - Returns a monotonically increasing `fencingToken` for concurrency verification.

**Request Body:**
```json
{
  "showtimeId": "c1000000-0000-0000-0000-000000000001",
  "seatIds": [
    "e1000000-0000-0000-0000-000000000001",
    "e1000000-0000-0000-0000-000000000002"
  ],
  "customerId": null,
  "guestEmail": "customer@example.com",
  "idempotencyKey": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response (200 OK):**
```json
{
  "holdId": "f1000000-0000-0000-0000-000000000001",
  "showtimeId": "c1000000-0000-0000-0000-000000000001",
  "seatIds": [
    "e1000000-0000-0000-0000-000000000001",
    "e1000000-0000-0000-0000-000000000002"
  ],
  "idempotencyKey": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fencingToken": 1042,
  "holdExpiresAt": "2026-09-26T16:10:00Z",
  "status": "held",
  "message": "Seats successfully locked for 10 minutes."
}
```

**Error Responses:**
- `409 Conflict`: One or more selected seats are already held or booked by another transaction.

---

#### 3.2 Release Seat Hold (Cancel Selection)
- **Endpoint:** `DELETE /api/v1/reservations/holds/{holdId}`
- **Auth:** Anonymous / Cashier
- **Description:** Instantly releases Redis seat locks if the customer changes their mind before the 10-minute timeout.

---

### 4. Point of Sale & Checkout API

Handles till shifts, pending order creation, combo discounts, cash and electronic payments, and F&B concessions.

#### 4.1 Open Cashier Till Shift
- **Endpoint:** `POST /api/v1/pos/shifts/open`
- **Auth:** Required (`Bearer <token>`, Role: `cashier`, `supervisor`)

**Request Body:**
```json
{
  "branchId": "b1000000-0000-0000-0000-000000000001",
  "cashierId": "11111111-2222-3333-4444-555555555555",
  "terminalCode": "POS-01",
  "openingFloat": 100.00
}
```

**Response (201 Created):**
```json
{
  "shiftId": "77777777-8888-9999-0000-111111111111",
  "branchId": "b1000000-0000-0000-0000-000000000001",
  "cashierId": "11111111-2222-3333-4444-555555555555",
  "terminalCode": "POS-01",
  "openingFloat": 100.00,
  "status": "open",
  "openedAt": "2026-09-26T08:00:00Z"
}
```

---

#### 4.2 Close Cashier Till Shift
- **Endpoint:** `POST /api/v1/pos/shifts/close`
- **Auth:** Required (`Bearer <token>`)

**Request Body:**
```json
{
  "shiftId": "77777777-8888-9999-0000-111111111111",
  "closingCash": 450.00
}
```

**Response (200 OK):**
```json
{
  "shiftId": "77777777-8888-9999-0000-111111111111",
  "openingFloat": 100.00,
  "closingCash": 450.00,
  "discrepancy": 350.00,
  "status": "closed",
  "openedAt": "2026-09-26T08:00:00Z",
  "closedAt": "2026-09-26T17:00:00Z"
}
```

---

#### 4.3 Get F&B Product Catalog
- **Endpoint:** `GET /api/v1/pos/products?branchId={branchId}`
- **Auth:** Anonymous

**Response (200 OK):**
```json
[
  {
    "productId": "99000000-0000-0000-0000-000000000001",
    "name": "Caramel Popcorn (L)",
    "category": "Popcorn",
    "price": 3.50,
    "imageUrl": "http://localhost:9000/cinema-assets/fnb/caramel-popcorn.png",
    "stockLevel": 45,
    "isActive": true
  },
  {
    "productId": "99000000-0000-0000-0000-000000000002",
    "name": "Coca Cola 32oz",
    "category": "Beverages",
    "price": 2.00,
    "imageUrl": "http://localhost:9000/cinema-assets/fnb/coke.png",
    "stockLevel": 120,
    "isActive": true
  }
]
```

---

#### 4.4 Create POS Order
- **Endpoint:** `POST /api/v1/pos/orders`
- **Header:** `X-Idempotency-Key` (UUIDv4)
- **Auth:** Required (`Bearer <token>`)
- **Description:** Creates an order transactionally bundling movie tickets (from seat hold) and/or concession products. Automatically executes the **Combo Discount Engine**.

**Request Body:**
```json
{
  "branchId": "b1000000-0000-0000-0000-000000000001",
  "cashierId": "11111111-2222-3333-4444-555555555555",
  "reservationId": "f1000000-0000-0000-0000-000000000001",
  "customerId": null,
  "discountAmount": 0.00,
  "voucherCode": null,
  "lines": [
    {
      "productId": null,
      "description": "Avengers: Endgame - Seat A1",
      "quantity": 1,
      "unitPrice": 6.50,
      "lineType": "ticket"
    },
    {
      "productId": null,
      "description": "Avengers: Endgame - Seat A2",
      "quantity": 1,
      "unitPrice": 6.50,
      "lineType": "ticket"
    },
    {
      "productId": "99000000-0000-0000-0000-000000000001",
      "description": "Caramel Popcorn (L)",
      "quantity": 1,
      "unitPrice": 3.50,
      "lineType": "fnb"
    },
    {
      "productId": "99000000-0000-0000-0000-000000000002",
      "description": "Coca Cola 32oz",
      "quantity": 2,
      "unitPrice": 2.00,
      "lineType": "fnb"
    }
  ]
}
```

**Response (200 OK):**
```json
{
  "orderId": "88880000-0000-0000-0000-000000000001",
  "branchId": "b1000000-0000-0000-0000-000000000001",
  "cashierId": "11111111-2222-3333-4444-555555555555",
  "reservationId": "f1000000-0000-0000-0000-000000000001",
  "status": "pending",
  "subtotal": 20.50,
  "discountAmount": 1.50,
  "totalAmount": 19.00,
  "idempotencyKey": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "createdAt": "2026-09-26T16:05:00Z"
}
```

---

#### 4.5 Process Order Payment
- **Endpoint:** `POST /api/v1/pos/orders/{orderId}/payments`
- **Header:** `X-Idempotency-Key` (string)
- **Auth:** Required (`Bearer <token>`)
- **Supported Methods:** `cash`, `card`, `qr` (Bakong KHQR), `loyalty_points`.

**Request Body (Cash Example):**
```json
{
  "method": "cash",
  "amount": 20.00,
  "providerReference": null
}
```

**Response (200 OK):**
```json
{
  "paymentId": "99990000-0000-0000-0000-000000000001",
  "orderId": "88880000-0000-0000-0000-000000000001",
  "method": "cash",
  "amountCharged": 20.00,
  "orderTotal": 19.00,
  "totalPaid": 20.00,
  "changeGiven": 1.00,
  "remainingBalance": 0.00,
  "isOrderFullyPaid": true,
  "status": "paid",
  "paidAt": "2026-09-26T16:06:00Z"
}
```

---

#### 4.6 Scan / Fulfill Concessions Voucher
- **Endpoint:** `POST /api/v1/pos/orders/scan-concessions`
- **Auth:** Required (Role: `cashier`)
- **Description:** Decodes 2D barcode scanner inputs (`CINEMA-FNB:...` or `CINEMA-ORDER:...`) at the snack counter and fulfills items.

**Request Body:**
```json
{
  "barcodeOrToken": "CINEMA-FNB:88880000-0000-0000-0000-000000000001:FNB-88880000",
  "autoFulfill": true
}
```

---

### 5. Ticket Issuance & Gate Scanning API

Handles cryptographic ticket issuance, ESC/POS thermal printing, and barcode scanner gate redemption.

#### 5.1 Issue Digital Tickets
- **Endpoint:** `POST /api/v1/tickets/issue`
- **Auth:** Required (`Bearer <token>`)
- **Description:** Converts paid reservations into entry tickets with SHA-256 digital signature hashes.

**Request Body:**
```json
{
  "reservationId": "f1000000-0000-0000-0000-000000000001",
  "showtimeId": "c1000000-0000-0000-0000-000000000001",
  "seatIds": [
    "e1000000-0000-0000-0000-000000000001",
    "e1000000-0000-0000-0000-000000000002"
  ]
}
```

**Response (201 Created):**
```json
[
  {
    "ticketId": "aa110000-0000-0000-0000-000000000001",
    "reservationId": "f1000000-0000-0000-0000-000000000001",
    "showtimeId": "c1000000-0000-0000-0000-000000000001",
    "seatId": "e1000000-0000-0000-0000-000000000001",
    "status": "active",
    "qrHash": "3A7F8B2C4E9D0F1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0B1C2D3E4F5A"
  }
]
```

---

#### 5.2 Print Physical ESC/POS Ticket Stub
- **Endpoint:** `POST /api/v1/tickets/{ticketId}/print`
- **Auth:** Required (`Bearer <token>`)
- **Invariant:** Enforces single first-print guarantee. If ticket was already printed, requires supervisor PIN.

**Request Body (Optional for First Print, Required for Reprint):**
```json
{
  "isReprint": false,
  "supervisorPin": null
}
```

**Response (200 OK):**
```json
{
  "ticketId": "aa110000-0000-0000-0000-000000000001",
  "isPrinted": true,
  "printedAt": "2026-09-26T16:07:00Z",
  "printFormat": "ESC/POS 80mm Direct Thermal Raw Bytes",
  "drawerKick": false
}
```

---

#### 5.3 Redeem Ticket at Gate / Turnstile
- **Endpoint:** `POST /api/v1/tickets/redeem`
- **Auth:** Required (`Bearer <token>`)
- **Target Latency:** `< 500ms` for seamless admission flow.

**Request Body:**
```json
{
  "qrToken": "CINEMA-TICKET:aa110000-0000-0000-0000-000000000001:f1000000-0000-0000-0000-000000000001:e1000000-0000-0000-0000-000000000001"
}
```

**Response (200 OK):**
```json
{
  "ticketId": "aa110000-0000-0000-0000-000000000001",
  "status": "used",
  "redeemedAt": "2026-09-26T18:25:12Z",
  "message": "Ticket successfully validated and redeemed."
}
```

**Conflict Response (409 Conflict):**
```json
{
  "error": "Ticket has already been redeemed.",
  "ticketId": "aa110000-0000-0000-0000-000000000001",
  "status": "used",
  "redeemedAt": "2026-09-26T18:15:00Z"
}
```

---

### 6. Loyalty, CRM & Vouchers API

Handles membership profiles, tier perks, point discounts, and promotional discount vouchers.

#### 6.1 Get Member Profile & Points
- **Endpoint:** `GET /api/v1/loyalty/members/{email}`
- **Auth:** Required (`Bearer <token>`)

**Response (200 OK):**
```json
{
  "memberId": "bb220000-0000-0000-0000-000000000001",
  "email": "customer@example.com",
  "fullName": "Sokha Mean",
  "tier": "Gold",
  "pointsBalance": 350,
  "discountRate": 0.10
}
```

---

#### 6.2 Validate Voucher Code
- **Endpoint:** `POST /api/v1/loyalty/vouchers/validate`
- **Auth:** Anonymous / Cashier

**Request Body:**
```json
{
  "code": "CINEMA2026"
}
```

**Response (200 OK):**
```json
{
  "code": "CINEMA2026",
  "isValid": true,
  "discountType": "Percentage",
  "discountValue": 15.00,
  "maxDiscountAmount": 5.00,
  "description": "15% Grand Opening Discount"
}
```

---

## 5. TypeScript Integration Helper

The following clean wrapper module can be copied directly into the POS Client UI project:

```typescript
// src/lib/posApiClient.ts

const API_BASE_URL = import.meta.env.VITE_API_GATEWAY_URL || "http://localhost:8080";

export async function posApiRequest<T>(
  endpoint: string,
  options: RequestInit & { idempotencyKey?: string; supervisorPin?: string } = {}
): Promise<T> {
  const token = localStorage.getItem("cinema_pos_token");
  
  const headers = new Headers(options.headers || {});
  headers.set("Content-Type", "application/json");

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (options.idempotencyKey) {
    headers.set("X-Idempotency-Key", options.idempotencyKey);
  }

  if (options.supervisorPin) {
    headers.set("X-Supervisor-Pin", options.supervisorPin);
  }

  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let errorDetail = `Request failed with status ${response.status}`;
    try {
      const errorJson = await response.json();
      errorDetail = errorJson.detail || errorJson.error || errorJson.message || errorDetail;
    } catch {
      // Fallback to text status
    }
    throw new Error(errorDetail);
  }

  return response.json() as Promise<T>;
}

// Quick Helper: Generate UUID v4 for Idempotency
export function generateIdempotencyKey(): string {
  return crypto.randomUUID();
}
```
