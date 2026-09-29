# POS Client UI - Implementation Plan

## 1. Project Setup
- **Framework:** Windows Forms .NET C# (.NET 8 recommended)
- **Local Database:** SQL Server (Express or LocalDB) required for local storage, caching, and offline support.
- **API Integration:** `HttpClient` configured with `Polly` for resilience.
- **Recommended NuGet Packages:**
  - `Newtonsoft.Json` or `System.Text.Json`: For API payload serialization.
  - `Dapper`: Micro-ORM for high-performance SQL Server data access.
  - `Microsoft.Data.SqlClient`: SQL Server database provider.
  - `Serilog` & `Serilog.Sinks.File`: For structured local logging (crash reports, audits).
  - `Polly`: For retry policies and network error handling.
  - `ESCPOS_NET`: For thermal receipt printing via ESC/POS protocol.
  - `QRCoder`: Generate Bakong KHQR payment QR codes as `Bitmap` for display in a `PictureBox`.
  - `ReaLTaiizor` or `MaterialSkin.2`: For modern, flat UI controls (optional, if default WinForms controls are too dated).

## 2. Solution Project Structure
Recommended WinForms solution layout:
```text
CinemaPOS.sln
├── CinemaPOS.App              (WinForms startup project — Forms, UserControls)
│   ├── Forms/
│   │   ├── LoginForm.cs
│   │   ├── MainPosForm.cs
│   │   ├── PaymentForm.cs      (embedded as panel in MainPosForm center)
│   │   ├── ReceiptForm.cs
│   │   └── ShiftCloseForm.cs
│   ├── Controls/
│   │   ├── SeatMapControl.cs   (custom GDI+ double-buffered Panel)
│   │   ├── MovieTileControl.cs (UserControl: poster + showtimes)
│   │   ├── FnbItemControl.cs   (UserControl: image + +/- stepper)
│   │   ├── CartControl.cs      (UserControl: line items + totals)
│   │   └── BakongQrControl.cs  (UserControl: KHQR bitmap + polling spinner + success overlay)
│   └── Program.cs
├── CinemaPOS.Core             (Business logic, models, services — no UI deps)
│   ├── Models/                (all DTOs and local data models)
│   ├── Services/              (ApiService, CartService, PrintService, ShiftService)
│   ├── Services/BakongWebhookService.cs  (polls webhook endpoint until paid or timeout)
│   └── Database/              (Dapper repositories)
└── CinemaPOS.Tests            (unit tests for Core layer)
```

## 3. SQL Server Local Storage Schema
This schema enables offline caching, parked transactions, and shift management.
```sql
-- Track terminal shifts
CREATE TABLE LocalShifts (
    ShiftId UNIQUEIDENTIFIER PRIMARY KEY,
    BranchId UNIQUEIDENTIFIER,
    CashierId UNIQUEIDENTIFIER,
    TerminalCode NVARCHAR(50),
    OpeningFloat DECIMAL(18,2),
    Status NVARCHAR(20),
    OpenedAt DATETIME
);

-- Support Hold/Resume transactions
CREATE TABLE ParkedTransactions (
    TransactionId UNIQUEIDENTIFIER PRIMARY KEY,
    HoldId UNIQUEIDENTIFIER,
    ShowtimeId UNIQUEIDENTIFIER,
    SeatIdsJson NVARCHAR(MAX),
    Subtotal DECIMAL(18,2),
    ParkedAt DATETIME
);

-- Cache movies and showtimes locally (refreshed on startup)
CREATE TABLE LocalMovies (
    MovieId UNIQUEIDENTIFIER PRIMARY KEY,
    Title NVARCHAR(255),
    PosterUrl NVARCHAR(500),
    Genre NVARCHAR(100),
    CachedAt DATETIME
);

CREATE TABLE LocalShowtimes (
    ShowtimeId UNIQUEIDENTIFIER PRIMARY KEY,
    MovieId UNIQUEIDENTIFIER,
    MovieTitle NVARCHAR(255),
    AuditoriumName NVARCHAR(100),
    ScreenType NVARCHAR(50),
    StartTime DATETIME,
    BasePrice DECIMAL(18,2),
    Status NVARCHAR(20),
    CachedAt DATETIME
);

-- Cache F&B product catalog locally
CREATE TABLE LocalProducts (
    ProductId UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(255),
    Category NVARCHAR(100),
    Price DECIMAL(18,2),
    ImageUrl NVARCHAR(500),
    StockLevel INT,
    CachedAt DATETIME
);

-- Audit log of completed orders (for shift reporting and void tracking)
CREATE TABLE LocalOrderAudit (
    AuditId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    OrderId UNIQUEIDENTIFIER,
    ShiftId UNIQUEIDENTIFIER,
    TotalAmount DECIMAL(18,2),
    PaymentMethod NVARCHAR(50),
    IsVoided BIT DEFAULT 0,
    CreatedAt DATETIME
);

-- Ticket types cache
CREATE TABLE LocalTicketTypes (
    TicketTypeId UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(50),
    PriceModifier DECIMAL(18,2),
    CachedAt DATETIME
);
```

## 4. DTO Models Complete Reference
Ensure these DTOs are mapped in the `CinemaPOS.Core/Models` layer:

- `BranchDto`: `(BranchId, Name, Location, IsActive)`
- `TicketTypeDto`: `(Id, Name, Label, PriceModifier)`
- `MemberProfileDto`: `(MemberId, Email, FullName, Tier, PointsBalance, DiscountRate)`
- `VoucherValidateRequest`: `(Code)`
- `VoucherValidateResponse`: `(IsValid, DiscountType, DiscountValue, MaxDiscountAmount)`
- `SeatMapResponseDto`: `(ShowtimeId, AuditoriumName, TotalSeats, AvailableSeats, Seats: List<SeatDto>)`
- `OrderLineDto`: `(ProductId, Description, Quantity, UnitPrice, LineType: "ticket"|"fnb")`
- `OrderRequest`: `(ReservationId, Lines, VoucherCode, DiscountAmount)`
- `TicketPrintRequest`: `(IsReprint, SupervisorPin)`
- `ConcessionsBarcodeScanRequest`: `(BarcodeOrToken, AutoFulfill)`
- `LoginRequest`: `(Username, PinOrPassword)`
- `LoginResponse`: `(Token, UserId, BranchId)`
- `ShiftOpenRequest`: `(BranchId, CashierId, TerminalCode, OpeningFloat)`
- `ShiftOpenResponse`: `(ShiftId, Status)`
- `HoldRequest`: `(ShowtimeId, SeatIds, IdempotencyKey)`
- `HoldResponse`: `(HoldId, FencingToken)`
- `PaymentRequest`: `(Method, Amount)`
- `PaymentResponse`: `(ChangeGiven, IsOrderFullyPaid)`
- `TicketIssueRequest` / `TicketIssueResponse`
- `ShiftCloseRequest`: `(ShiftId, ClosingCash)` / `ShiftCloseResponse`: `(Discrepancy)`

## 5. Color System & Visual Guidelines

> **Design Reference:** Boss Revolution POS — clean flat white panels, high-contrast semantic action buttons. NOT a dark-mode "AI slop" palette.

### 5.1 Global Application Colors

| Role | Hex | Usage |
|---|---|---|
| **Window Chrome / BG** | `#F5F5F5` | Main form background — warm light grey |
| **Panel Background** | `#FFFFFF` | Cart, center panel, left panel — pure white |
| **Summary Card BG** | `#F0F0F0` | Subtotal/Total card at bottom of cart |
| **Primary Text** | `#1A1A1A` | All main labels — near-black, readable at distance |
| **Secondary Text** | `#5B7FA6` | Qty × unit price (e.g., "2 @ $6.50") — muted steel blue |
| **Total Amount** | `#1A1A1A` bold 22pt+ | The prominent total figure |
| **Discount / Savings** | `#D32F2F` | Savings/discount line in cart — red |
| **Border / Divider** | `#E0E0E0` | Panel separators, tile borders, keypad cell borders |
| **Base Font** | `Segoe UI 13pt` | Consistent throughout all forms |

### 5.2 Action Button Colors

| Button | Hex | Notes |
|---|---|---|
| **CASH / Confirm** | `#2E7D32` | Deep green — biggest CTA, white text |
| **CARD / Credit** | `#C62828` | Deep red — high-stakes action |
| **QR / Bakong KHQR** | `#1565C0` | Deep blue — visually distinct |
| **SPLIT Payment** | `#6A1B9A` | Purple — unique, not confused with others |
| **VOUCHER / Other** | `#E65100` | Burnt orange |
| **REFUND** | `#B71C1C` | Darker red — same family as CARD, distinct |
| **Quick Tender ($10/$20/$50)** | `#37474F` | Dark blue-grey slate, white text |
| **HOLD / Park** | `#455A64` | Dark slate grey |
| **CANCEL / Void** | `#757575` | Medium grey — de-emphasized |
| **SKIP / Secondary** | `#BDBDBD` | Light grey, dark text |

> **WinForms:** `FlatStyle = Flat`, `FlatAppearance.BorderSize = 0`, `DoubleBuffered = true` on all panels.

### 5.3 Seat Map Colors

| Seat State | Hex | Meaning |
|---|---|---|
| **Available** | `#388E3C` | Green — same family as Cash ("safe to pick") |
| **Selected / In Cart** | `#F57F17` | Amber — warm standout on white canvas |
| **Held by Others** | `#FFA000` | Orange warning |
| **Booked / Sold** | `#C62828` | Red — same as CARD ("stop") |
| **Blocked / Maintenance** | `#9E9E9E` | Neutral grey |
| **VIP / Premium** | `#FFB300` | Gold accent |
| **Screen / Stage bar** | `#212121` | Near-black bar at top of seat map |

### 5.4 Category Tile Colors (Movie Filters & F&B Grid)

| State | Style |
|---|---|
| **Default** | `#FFFFFF` bg + `#E0E0E0` 1px border |
| **Active / Selected** | `#FFFFFF` bg + `#2E7D32` 2px border + green ✓ badge (bottom-right, via `OnPaint`) |
| **Hover** | `#F5F5F5` background tint |

### 5.5 Ticket Type Badge Colors (Cart Panel)

| Type | Color |
|---|---|
| **Adult** | `#1565C0` Deep Blue |
| **Child** | `#4CAF50` Green |
| **Senior** | `#7B1FA2` Purple |
| **Student** | `#E65100` Orange |

### 5.6 Status / Notification Banner

| State | Background | Text |
|---|---|---|
| **Promo** | `#C62828` Red | `#FFFFFF` bold |
| **OFFLINE warning** | `#E65100` Burnt orange | `#FFFFFF` bold |
| **Success** | `#2E7D32` Green | `#FFFFFF` bold |

### 5.7 Numeric Keypad

| Element | Color |
|---|---|
| Digit key bg | `#FFFFFF` + `#E0E0E0` border |
| Digit key text | `#1A1A1A` |
| Key hover/press | `#EEEEEE` |
| Quick-tender buttons | `#37474F` bg, `#FFFFFF` text |

### 5.8 Bottom Navigation Bar

| Element | Color |
|---|---|
| Bar background | `#ECEFF1` very light blue-grey |
| Icon color | `#546E7A` |
| Active icon | `#1A1A1A` |

### 5.9 Manager Override Modal

| Element | Color |
|---|---|
| Overlay | `rgba(0,0,0,0.55)` |
| Modal card | `#FFFFFF` |
| PIN pad buttons | `#37474F` slate, `#FFFFFF` text |
| Confirm button | `#C62828` red |

### 5.10 UI Layout Rules (from Reference Image)

1. **Cart line items** — Item name `#1A1A1A` bold 13pt; qty below in `#5B7FA6` 11pt (e.g., "2 @ $6.50"); price right-aligned `#1A1A1A` bold.
2. **Cart total block** — Sub Total / Discount / Tax / **Total** in a `#F0F0F0` rounded card at bottom of cart. Total in 22pt+ bold.
3. **Category tiles** — `FlowLayoutPanel` of custom `UserControl` tiles. White + grey border default; green 2px border + ✓ badge when active (use `OnPaint` override).
4. **Numeric keypad** — `TableLayoutPanel` grid of white digit buttons. Quick-tender row (`$10`, `$20`, `$50`) in `#37474F` to the right of digits.
5. **Bottom action buttons** (Cancel / Hold) — Full-width flat buttons, **52px min height**. Cancel = `#757575`, Hold = `#455A64`.
6. **Bottom nav bar** — 40px strip: `[Hold F9]` `[Void F11]` `[Discount F3]` `[New Sale F12]` icon+label in `#546E7A` on `#ECEFF1`. Shift total pinned right bold.
7. **Notification banner** — 28px collapsible bar below title bar. `#C62828` for promos; `#E65100` for OFFLINE.
8. **WinForms flat style** — `FlatStyle = Flat`, `BackColor = #F5F5F5` on Form, `#FFFFFF` on panels, `Segoe UI 13pt`. Rounded corners via `GraphicsPath` or `ReaLTaiizor`.

## 6. Keyboard Shortcut Map
- **F1:** Search Movie / Showtimes
- **F2:** Search Customer / Booking ID
- **F3:** Apply Discount
- **F4:** F&B Upsell Menu
- **F9:** Hold / Park Transaction
- **F10:** Resume Parked Transaction
- **F11:** Void / Cancel Transaction
- **F12 / Enter:** Proceed to Payment / New Sale
- **Space:** Quick-select highlighted item

## 7. Cross-Cutting UX Patterns
- **Manager Override Flow:** If a cashier voids a paid transaction, an inline modal pops up requesting a Supervisor PIN (`X-Supervisor-Pin`). Calls `POST /api/v1/identity/verify-supervisor`. Do not close the current transaction context.
- **Hold/Resume Transaction:** Press F9 to serialize the cart to `ParkedTransactions` in SQL Server and clear the screen for the next customer. Press F10 to load it back.
- **Network Error Handling Strategy:** Use `Polly` to wrap `HttpClient` calls. Reads (GET) retry 3 times with exponential backoff. Writes (POST) use `X-Idempotency-Key` (UUID) so that retrying the exact same request doesn't result in double-booking or double-charging. If offline, display a red banner "OFFLINE - Seat availability may be stale".

---

## 8. Screens Implementation

### Screen 1: Login / Shift Start
- **What to build:** A dark-themed login screen using a numeric PIN pad (no typing) and a prompt to confirm the opening cash drawer float.
- **API Endpoints:** 
  - `GET /api/v1/catalog/branches` — to populate branch selector on startup
  - `GET /api/v1/identity/users` — to populate the username dropdown (used by Roles: `branch_manager`, `system_admin`, or pre-populated from local cache)
  - `POST /api/v1/identity/login`
  - `POST /api/v1/pos/shifts/open`
- **UI Layout:**
```text
┌───────────────────────────────────────────┐
│               CINEMA POS                  │
│                                           │
│         [ BRANCH DROPDOWN ]               │
│         [ USERNAME DROPDOWN ]             │
│                                           │
│             [7]  [8]  [9]                 │
│             [4]  [5]  [6]                 │
│             [1]  [2]  [3]                 │
│             [CLEAR (#757575)] [0] [ENTER (#C62828)]           │
│                                           │
│   (Or swipe Staff Card to authenticate)   │
└───────────────────────────────────────────┘
```
- **Key WinForms Controls:** `TableLayoutPanel` (for the keypad), `ComboBox` (for branches and usernames), `Button` (large targets).

### Screen 2: Main POS Home & Seat Map
- **What to build:** The 3-panel hub where cashiers spend their day. Movie selection on the left, interactive seat map in the center, and cart on the right. When a seat is clicked, a lightweight inline popup asks for ticket type (Adult/Child). Add a visible countdown timer (10 minutes) in the Cart panel showing hold expiry time, with a "Refresh Hold" warning when <2 min remain.
- **Seat Map UI Approach:** DO NOT use hundreds of WinForms `Button` controls (causes severe lag). **Recommendation:** Clone an open-source GitHub repo like `https://github.com/Jinjinov/SeatMap` (or a similar GDI+ custom control), OR create a custom `Panel` overriding `OnPaint` to draw seats using `Graphics.FillRectangle` for maximum performance.
- **API Endpoints:** 
  - `GET /api/v1/catalog/showtimes?branchId={id}`
  - `GET /api/v1/catalog/seat-map/{showtimeId}`
  - `GET /api/v1/catalog/ticket-types` — required for the seat-click inline popup (Adult/Child/Senior price modifiers)
  - `POST /api/v1/reservations/holds` (with `X-Idempotency-Key`)
  - `DELETE /api/v1/reservations/holds/{holdId}` — when cashier deselects a seat, release the hold
- **UI Layout:**
```text
┌─────────────────────────────────────────────────────────┐
│ [PROMO BANNER: #C62828 Red / OFFLINE: #E65100 Orange]   │
├─────────────────────────────────────────────────────────┤
│ [Search Bar]              [Staff: John D.] [Till: #3]   │
│                                           Hold: 09:45   │
├───────────────┬───────────────────────────┬─────────────┤
│               │   SEAT MAP (GDI+ DRAWN)   │   Cart /    │
│  Movie List   │   [ ] [ ] [ ] [ ] [ ]     │   Order     │
│  (scrollable) │   [ ] [x] [x] [ ] [ ]     │   Summary   │
│               │                           │             │
│  [Now Showing]│   Popup:                  │  [Total]    │
│  [Coming Soon]│   [Adult][Child][Senior]  │  [Pay F12]  │
├───────────────┴───────────────────────────┴─────────────┤
│ [Hold F9 (#455A64)] [Void F11 (#757575)] [Discount F3]  │
│                                        [Shift: $2,340]  │
└─────────────────────────────────────────────────────────┘
```
- **Key WinForms Controls:** `FlowLayoutPanel` (Movie list), `DataGridView` (Cart), Custom `Control` with DoubleBuffering (Seat Map), Timer (for Hold countdown).

### Screen 3: F&B Upsell Screen & Concessions
- **What to build:** An F&B grid that replaces the center Seat Map panel. Shows item images with quantity steppers. Automatically highlights combo upsells. Includes a Barcode Scanner mode for pre-ordered F&B.
- **API Endpoints:** 
  - `GET /api/v1/pos/products`
  - `POST /api/v1/pos/orders/scan-concessions` — Scan Barcode button/mode for customers who pre-ordered F&B via mobile app
- **UI Layout:**
```text
┌─────────────────────────────────────────────────────────┐
│ Center Panel (Replaces Seat Map)                        │
│                                                         │
│  [ Popcorn (L) ]  [ Coke 32oz  ]  [ Nachos     ]        │
│  [   $3.50     ]  [   $2.00    ]  [  $4.50     ]        │
│  [ (-) 1 (+)   ]  [ (-) 0 (+)  ]  [ (-) 0 (+)  ]        │
│                                                         │
│  [ SCAN BARCODE ]                                       │
│  ** Recommended: Add Popcorn Combo +$3.00 **            │
│  [SKIP F&B]                                             │
└─────────────────────────────────────────────────────────┘
```
- **Key WinForms Controls:** `FlowLayoutPanel` filled with custom `UserControl` elements.

### Screen 4: Payment Screen
- **What to build:** Center panel swaps to payment methods. Supports Split Payments. 
- **Payment Methods Supported:**
  1. **Cash:** numeric keypad → change calculation
  2. **Card:** waiting spinner state + cancel
  3. **QR / Bakong KHQR:** show QR code for customer to scan (`method: "qr"`)
  4. **Loyalty Points:** requires member lookup first (`GET /api/v1/loyalty/members/{email}`), then apply as discount
  5. **Voucher/Gift Card:** validate via loyalty API (`POST /api/v1/loyalty/vouchers/validate`), apply discount
- **Split Payment Pattern:** Call `/payments` endpoint twice with partial amounts until `remainingBalance == 0`.
- **API Endpoints:** 
  - `GET /api/v1/loyalty/members/{email}` — Customer lookup for loyalty points redemption
  - `POST /api/v1/loyalty/vouchers/validate` — Voucher validate logic
  - `POST /api/v1/pos/orders` (creates the order - MUST map `voucherCode` and `discountAmount` from UI)
  - `POST /api/v1/pos/orders/{orderId}/payments`
- **UI Layout:**
```text
┌─────────────────────────────────────────────────────────┐
│ Center Panel (Payment)                                  │
│                                                         │
│  TOTAL DUE: $19.00                                      │
│                                                         │
│  [ CASH (#2E7D32) ] [ CARD (#C62828) ] [ QR (#1565C0) ]                      │
│                                                         │
│  [ Lookup Customer (Loyalty) ]  [ Validate Voucher ]    │
│                                                         │
│  Amount Tendered: $____  [Keypad 1-9 (#FFFFFF)]                   │
│                          [Quick: $20, $50 (#37474F)]              │
└─────────────────────────────────────────────────────────┘
```

### Screen 5: Receipt & Confirmation
- **What to build:** Success state indicating payment is complete and tickets are printing. Big checkmark. Includes Reprint workflow.
- **API Endpoints:** 
  - `POST /api/v1/tickets/issue`
  - `POST /api/v1/tickets/{ticketId}/print` — standard print
  - `POST /api/v1/tickets/{ticketId}/print` (with `isReprint: true` + supervisor PIN flow) — if cashier presses "Reprint", it requires a manager override PIN inline modal before calling.
- **UI Layout:**
```text
┌─────────────────────────────────────────────────────────┐
│ Center Panel                                            │
│                                                         │
│            [✓] TRANSACTION COMPLETE                     │
│                                                         │
│            CHANGE DUE: $1.00                            │
│                                                         │
│      [Print Receipt] [Email Receipt] [Reprint]          │
│                                                         │
│            [ NEW SALE (F12) ]                           │
└─────────────────────────────────────────────────────────┘
```

### Screen 6: End of Shift / Cash-out
- **What to build:** End of day reconciliation. Displays a **shift summary** queried from the `LocalOrderAudit` table before closing. Cashier enters actual cash in drawer.
- **Shift Summary Data:** Total Tickets Sold, Total F&B Sales, Sales by Payment Method (Cash / Card / QR / Voucher), Total Voids.
- **API Endpoints:** `POST /api/v1/pos/shifts/close`
- **UI Layout:**
```text
┌─────────────────────────────────────────────────────────┐
│ SHIFT CLOSE SUMMARY                                     │
│                                                         │
│ Expected Cash: $450.00                                  │
│ Tickets Sold: 120 | F&B Sales: $300.00                  │
│ Voids: 2 | Sales By Method: Cash $400 / Card $50        │
│                                                         │
│ Actual Cash in Drawer: [ $________ ]                    │
│                                                         │
│ [ CLOSE SHIFT ]  [ CANCEL ]                             │
└─────────────────────────────────────────────────────────┘
```

---

## 9. Implementation Phases
- **Phase 1: Setup & Data Layer** - Create solution, set up SQL Server local schema via Dapper, implement `HttpClient` + Polly wrapper. Build Screen 1 (Login).
- **Phase 2: Core POS (Screen 2)** - Build the 3-panel UI layout. Implement catalog fetching, custom GDI+ Seat Map, and local Cart state.
- **Phase 3: F&B & Cart Operations (Screen 3)** - Implement Screen 3, ticket type assignments, F&B grid, and Hold/Resume F9/F10 logic.
- **Phase 4: Checkout & Hardware (Screens 4, 5, 6)** - Build Payment flows, Order API integration, ESC/POS printing logic, and End-of-Shift functionality, manager overrides.

---

## 10. Bakong KHQR Payment — QR Display & Webhook Status

> **Backend status:** Payment verification not yet implemented. Webhook receiver endpoint already exists. UI must be **fully ready** and webhook-aware so it works the moment backend is wired.

### 10.1 Flow Overview

```
Cashier selects [ QR / Bakong ] on Payment Screen
        ↓
POST /api/v1/pos/orders/{orderId}/payments
  { method: "qr", amount: totalDue }
        ↓
Backend returns a KHQR string (deeplink/EMV QR payload)
e.g. "00020101021229370016A000000677010111..."
        ↓
UI generates QR bitmap using QRCoder and displays in BakongQrControl
        ↓
UI starts polling loop (BakongWebhookService)
  every 3 seconds → GET /api/v1/pos/orders/{orderId}/payment-status
        ↓
  ┌── Status == "pending"  → keep showing QR + spinner
  ├── Status == "paid"     → overlay green SUCCESS state
  └── Timeout (2 min)      → show EXPIRED state + retry/cancel
```

### 10.2 BakongQrControl — 3 Visual States

```text
┌──────────────────────────────────────────────────────┐
│  STATE 1: WAITING FOR PAYMENT                        │
│                                                      │
│         ┌─────────────────────┐                      │
│         │  ░░░░░░░░░░░░░░░░   │  ← KHQR QR Code      │
│         │  ░░  ██████  ░░░░   │    (QRCoder bitmap)  │
│         │  ░░  ██████  ░░░░   │                      │
│         │  ░░░░░░░░░░░░░░░░   │                      │
│         └─────────────────────┘                      │
│                                                      │
│    TOTAL DUE: $19.00                                 │
│    Scan with Bakong app or any KHQR app              │
│                                                      │
│    ⏱ Waiting for payment...  [● ● ●] animated dots  │
│    ⏳ Expires in: 01:47                              │
│                                                      │
│    [ CANCEL ]                                        │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│  STATE 2: PAYMENT CONFIRMED  (webhook fires)         │
│                                                      │
│              ✅  PAYMENT RECEIVED                    │
│                                                      │
│         Amount Paid: $19.00                          │
│         Method: Bakong KHQR                          │
│         Ref: TXN-20260926-8821                       │
│                                                      │
│    (auto-advances to Receipt screen in 2 sec)        │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│  STATE 3: EXPIRED / TIMEOUT                          │
│                                                      │
│              ⚠️  QR CODE EXPIRED                    │
│                                                      │
│    Customer did not pay within 2 minutes.            │
│                                                      │
│    [ GENERATE NEW QR ]    [ CHOOSE OTHER METHOD ]   │
└──────────────────────────────────────────────────────┘
```

### 10.3 BakongWebhookService Implementation Pattern

Backend has a webhook but payment verification API is not ready yet. UI polls a status endpoint as a bridge until the webhook is fully wired on backend.

```csharp
// CinemaPOS.Core/Services/BakongWebhookService.cs

public class BakongWebhookService
{
    // PHASE A (now): Poll order status every 3 sec until paid or timeout
    public async Task PollUntilPaidAsync(
        string orderId,
        Action onPaid,          // → fires when status == "paid"
        Action onTimeout,       // → fires after 2 minutes
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var status = await _api.GetOrderPaymentStatusAsync(orderId);
            if (status == "paid") { onPaid(); return; }
            await Task.Delay(3000, ct);
        }
        onTimeout();
    }

    // PHASE B (future): Replace polling with SignalR / WebSocket push
    // when backend wires the webhook to push payment events to POS terminals.
    // UI code stays identical — just swap PollUntilPaidAsync for a hub listener.
}
```

**Endpoint to poll (must be added to API when ready):**
```
GET /api/v1/pos/orders/{orderId}/payment-status
Response: { "status": "pending" | "paid" | "failed", "paidAt": "...", "transactionRef": "..." }
```

> ⚠️ **Note:** This endpoint does not exist yet in the backend. The UI code must handle `404` gracefully (treat as "pending") so it does not crash during development.

### 10.4 DTOs to Add

```csharp
// In CinemaPOS.Core/Models/

public record BakongQrResponse(
    string OrderId,
    string KhqrPayload,   // EMV QR string to encode as bitmap
    string DeepLinkUrl,   // Optional: "bakong://..." for mobile redirect
    decimal Amount,
    DateTime ExpiresAt
);

public record OrderPaymentStatusResponse(
    string OrderId,
    string Status,          // "pending" | "paid" | "failed"
    DateTime? PaidAt,
    string? TransactionRef
);
```

### 10.5 Color Tokens for Bakong QR States

| State | Background | Icon/Text Color |
|---|---|---|
| **Waiting** | `#FFFFFF` | Spinner `#1565C0` blue, countdown `#757575` grey |
| **Paid / Success** | `#E8F5E9` light green | ✅ `#2E7D32`, text `#1A1A1A` |
| **Expired / Failed** | `#FFF3E0` light orange | ⚠️ `#E65100`, text `#1A1A1A` |

### 10.6 WinForms Controls for BakongQrControl

| Control | Purpose |
|---|---|
| `PictureBox` | Displays QRCoder-generated `Bitmap` of KHQR payload |
| `Label` (large) | "TOTAL DUE: $X.XX" |
| `Label` (animated) | Polling dots `● ● ●` — use `System.Windows.Forms.Timer` to cycle |
| `Label` (countdown) | "Expires in: MM:SS" — countdown `Timer` tick every second |
| `Panel` (overlay) | State 2 & 3 overlays — set `Visible = true` over QR on state change |
| `Button` Cancel | Cancels polling `CancellationTokenSource`, returns to payment method screen |
| `Button` Retry | Regenerates QR (calls payment endpoint again with new idempotency key) |

---

## 11. Demo Mode — Hardware Simulator Configuration

For development and demos without physical POS hardware. Toggle in `appsettings.json`.

### 11.1 Configuration

```json
// CinemaPOS.App/appsettings.json
{
  "DemoMode": {
    "Enabled": true,
    "PrinterName": "Microsoft Print to PDF",
    "SkipDrawerKick": true,
    "AutoApproveCard": true,
    "ScannerInputMode": "Keyboard"
  }
}
```

### 11.2 Hardware Replacement Table

| Hardware | Real Implementation | Demo / Software Replacement |
|---|---|---|
| **Thermal Printer** | `ESCPOS_NET` → physical 80mm printer | Print to **"Microsoft Print to PDF"** — receipt saves as PDF |
| **Cash Drawer (RJ11)** | Kick pulse via printer port | Log message: *"[DEMO] Drawer kick sent"* — install **com0com** to avoid COM port errors |
| **Barcode / QR Scanner** | USB HID keyboard-wedge device | Type token string in scan `TextBox` + press `Enter` — identical code path |
| **Card Terminal** | Real payment terminal API | `AutoApproveCard: true` → returns instant mock `PaymentResponse { IsOrderFullyPaid: true }` |
| **Bakong Terminal** | KHQR + webhook confirmation | QR displays correctly — polling returns mock `"paid"` after 5 sec delay in demo mode |

### 11.3 Demo Mode Service Pattern

```csharp
// CinemaPOS.Core/Services/PrintService.cs
public async Task PrintTicketAsync(byte[] escPosBytes)
{
    if (_config.DemoMode.Enabled)
    {
        // Send to PDF printer instead of thermal
        PrintDocument doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = _config.DemoMode.PrinterName;
        // ... render bytes as PDF
        _logger.LogInformation("[DEMO] Printed to PDF instead of thermal printer.");
        return;
    }
    // Real ESC/POS path
    var printer = new ImmediateNetworkPrinter(...);
    await printer.WriteAsync(escPosBytes);
}

// CinemaPOS.Core/Services/BakongWebhookService.cs
public async Task PollUntilPaidAsync(string orderId, Action onPaid, Action onTimeout, CancellationToken ct)
{
    if (_config.DemoMode.Enabled)
    {
        // Simulate payment confirmed after 5 seconds in demo
        await Task.Delay(5000, ct);
        onPaid();
        return;
    }
    // Real polling path (see Section 10.3)
    ...
}
```

### 11.4 Scanner Simulation — AutoHotkey Script (optional)

For demos where you want to simulate a real scanner beep + paste:

```autohotkey
; Save as SimulateScanner.ahk
; Press F6 to inject a test QR token into focused input field
F6::
  token := "CINEMA-TICKET:aa110000-0000-0000-0000-000000000001:f1000000:e1000000"
  SendInput %token%{Enter}
return
```

### 11.5 Demo Mode Visual Indicator

When `DemoMode.Enabled = true`, show a persistent **amber badge** in the top-right corner of the main form:

```text
┌─────────────────────────────────────────────────────────┐
│ [Search Bar]              [Staff: John D.]  ⚠ DEMO MODE │
```

- Badge color: `#F57F17` amber background, `#FFFFFF` text
- Ensures cashier / manager knows they are NOT on a live terminal

