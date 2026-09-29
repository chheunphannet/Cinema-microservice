# Cinema POS Admin UI — Implementation Plan

This document outlines the step-by-step implementation plan for the Admin UI of the Cinema POS system. The plan is organized by modules, with explicit mapping to backend services, UI components, and RBAC requirements.

## Phase 1: Core Dashboard & Setup
**Objective**: Establish the foundation of the admin panel and basic health metrics.

### 1.1 Layout & Navigation (React/Next.js or Vue)
- **Routes**: `/` (Dashboard), sidebar navigation grouped by module.
- **RBAC Matrix**: Read access to basic metrics for Managers; full for SuperAdmin.
- **Backend API**: `GET /api/v1/admin/executive`, `GET /api/v1/admin/system-health` (Identity.Api).

### 1.2 System Administration & Health Monitoring
- **UI Components**: Service status indicators, audit logs table, feature flag toggles.
- **Backend API**: `GET /api/v1/admin/audit-logs`, `GET /api/v1/admin/feature-flags` (Identity.Api).

## Phase 2: Catalog & Cinema Topology
**Objective**: Build out the structural data (Branches, Screens, Movies) before showtimes can be scheduled.

### 2.1 Branch & Screen Management
- **UI Components**: Branch list, Add/Edit Branch form, Screen list per branch, Seat Map visual editor.
- **Backend API**: `GET/POST /api/v1/admin/branches`, `POST /api/v1/admin/branches/{id}/screens` (Catalog.Api - *Newly Implemented*).

### 2.2 Movie Management
- **UI Components**: Movie list with tabs (Now Showing, Coming Soon, Ended), Add/Edit Movie form (poster upload, metadata), CSV Bulk Import dropzone.
- **Backend API**: `GET/POST/PUT /api/v1/admin/movies`, `POST /api/v1/admin/media/upload` (Catalog.Api).

### 2.3 F&B and Inventory Catalog
- **UI Components**: Menu item list, Category manager, Combo/Bundle builder.
- **Backend API**: `GET/POST/PUT /api/v1/admin/menu`, `POST /api/v1/admin/menu/combos` (Pos.Api - *Newly Implemented*).

## Phase 3: Scheduling & Operations
**Objective**: Enable daily operations, scheduling, and inventory tracking.

### 3.1 Showtime & Scheduling
- **UI Components**: Drag-and-drop calendar view per screen, Conflict detector warnings, Bulk schedule copy.
- **Backend API**: `GET/POST /api/v1/admin/showtimes`, `POST /api/v1/admin/showtimes/bulk-import` (Catalog.Api).

### 3.2 Inventory & Wastage Management
- **UI Components**: Stock levels data grid, Stock adjustment modal, Wastage logging form, Low-stock alerts.
- **Backend API**: `GET /api/v1/admin/branches/{id}/stock`, `POST /api/v1/admin/branches/{id}/wastage` (Pos.Api).

### 3.3 Staff & Role Management
- **UI Components**: Staff list, Role/Permission assignment matrix, Shift clock-in/out viewer.
- **Backend API**: `GET/POST /api/v1/admin/staff`, `GET /api/v1/admin/shifts` (Identity.Api).

## Phase 4: Business Rules & Pricing
**Objective**: Implement dynamic pricing, loyalty, and promotions.

### 4.1 Pricing & Promotions
- **UI Components**: Ticket types list, Pricing rule builder (conditions & modifiers), Promo code generator.
- **Backend API**: `GET/POST /api/v1/admin/pricing/rules`, `POST /api/v1/admin/pricing/ticket-types` (Catalog.Api).

### 4.2 Customer & Loyalty Management
- **UI Components**: Customer search & 360 view, Manual point adjustment modal, Tier override toggle.
- **Backend API**: `GET /api/v1/admin/members`, `POST /api/v1/admin/customers/{id}/adjust-points` (Loyalty.Api).

### 4.3 Marketing & CRM
- **UI Components**: Campaign builder, Audience segment list, Homepage banner manager.
- **Backend API**: `GET /api/v1/admin/segments`, `POST /api/v1/admin/broadcasts` (Loyalty.Api).

## Phase 5: Transactions, Finance & CMS
**Objective**: Close the loop with order management, financial reconciliation, and static content.

### 5.1 Booking & Transaction Management
- **UI Components**: Global reservation search, Booking details page, Refund/Void action buttons, Dispute logging.
- **Backend API**: `GET /api/v1/admin/search`, `POST /api/v1/admin/{id}/refund` (Reservation.Api).

### 5.2 Payment & Finance
- **UI Components**: Settlement reconciliation table, Tax/Fee configuration list, Revenue share reports.
- **Backend API**: `GET /api/v1/admin/finance/settlements`, `GET/POST /api/v1/admin/finance/taxes` (Pos.Api - *Newly Implemented*).

### 5.3 Reports & Analytics
- **UI Components**: Chart.js/Recharts dashboards for Sales, Occupancy, F&B, Export to CSV/PDF.
- **Backend API**: `GET /api/v1/admin/sales`, `GET /api/v1/admin/occupancy` (Identity.Api).

### 5.4 Content / CMS
- **UI Components**: WYSIWYG editor for Terms/FAQ, Localization keys grid.
- **Backend API**: `GET/POST /api/v1/admin/cms/pages`, `GET /api/v1/admin/cms/localizations` (Identity.Api - *Newly Implemented*).
