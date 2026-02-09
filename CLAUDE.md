# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build RepPortal.sln

# Run (default profile: http://localhost:5197)
dotnet run --project RepPortal.csproj

# Run tests
dotnet test RepPortal.Tests/RepPortal.Tests.csproj

# Run a single test
dotnet test RepPortal.Tests/RepPortal.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

Launch profiles: `http` (port 5197), `https` (port 7143), `IIS Express` (port 43100).

## Architecture Overview

**ASP.NET Core 8 Blazor Server** app for Chapin Manufacturing's sales rep portal. Uses Syncfusion Blazor 30 for UI components (grids, charts, dropdowns).

### Multi-Database Pattern

Three SQL Server databases accessed via `IDbConnectionFactory` (singleton):
- **RepPortal** (`RepPortalConnection`) — Identity, audit logs, notifications, Hangfire
- **BAT_App** (`BatAppConnection`) — Customer, order, shipment data
- **CustInfo** (`PcfConnection`) — Price & Cost File (PCF) data

Primary data access is **Dapper** (60s command timeout). Entity Framework Core is used only for ASP.NET Identity tables.

### Service Layer

```
Blazor Pages/Components (Pages/, Shared/)
        ↓
Service Layer (Services/)
        ↓
Data Access (Dapper + DbConnectionFactory, EF Core for Identity)
        ↓
SQL Server databases  +  CSI REST API (CsiRestClient)
```

Key services:
- `ISalesService` / `ISalesDataService` — Sales data aggregation across databases
- `IRepCodeContext` — Scoped service tracking current rep code (multi-tenant filtering)
- `PcfService` — Price & Cost File management
- `ExportService` — PDF/Excel export via Syncfusion
- `CsiRestClient` — HTTP client for external CSI ERP system (with `CsiLoggingHandler`)
- `StateContainer` — Scoped Blazor circuit-level state

### Authentication & Authorization

- Windows Authentication (Negotiate) + ASP.NET Identity with email confirmation
- Roles: `Administrator`, `SalesManager`, `SalesRep`, `User`, `SuperUser`, `HangfireAdmin`
- Custom claims (RepCode, Region, RepID) via `CustomUserClaimsPrincipalFactory`
- Cookie: `.ChapPortal.Auth`, 7-day expiry, no sliding expiration

### Background Jobs

Hangfire with SQL Server storage (schema: `HangFire`, queue: `reports`):
- `ReportRunner` — Executes scheduled report jobs
- `ExpiringPcfNotificationsJob` — Sends PCF expiration email notices
- `SubscriptionService` — Creates/manages recurring jobs

### Logging

Serilog with separate log files:
- `Logs/RepPortal-log-.txt` — Main app log (daily, 14-day retention)
- `Logs/RepPortal-csi-http-.txt` — CSI API calls only (daily)
- `Logs/RepPortal-expiring-pcf-job-.txt` — PCF notification job (monthly)

### Static Files

Price books served from a network share (`\\ciiws01\ChapinRepDocs`) mapped to `/RepDocs` request path.

## Coding Conventions

- **Dapper over EF Core** for all non-Identity data access. Show full commands with parameters.
- **T-SQL** for all SQL. Use CTEs for complex queries, table variables for intermediate results. Avoid SQL 2019+ features.
- **`var` only when type is obvious.** Favor explicitness.
- **Blazor components**: prefer code-behind or partial classes for non-trivial logic.
- **Syncfusion Blazor** patterns for UI (SfGrid, dropdowns, etc.).
- Use existing services/context for role-based access and impersonation rather than creating new ones.
- Do not introduce external packages not already in the stack without explicit approval.
- Show full methods/classes, not fragments. Include necessary `using` statements.
- SQL scripts go in `SqlScripts/` as embedded resources (DbUp migration pattern).

## Key Configuration

- `appsettings.json`: Connection strings, CSI API config (`Csi` section), SMTP (`Smtp`), OpenAI, PriceBooks paths
- `appsettings.Development.json`: Dev overrides
- CSI API options bound to `CsiOptions` from config section `"CSI"`
- Syncfusion license registered in `Program.cs` (v30)

## Project Structure

- `Pages/` — Blazor page components (36 .razor files)
- `Shared/` — Layout and shared components (MainLayout, NavMenu, Appbar, RepCodeSwitcher)
- `Services/` — Business logic and data access (44 service classes)
- `Services/Reports/` — Hangfire report jobs and report implementations
- `Services/ReportExport/` — Excel/PDF export logic
- `Models/` — Data models (39 classes), includes `CsiFieldAttribute` for CSI API field mapping
- `Data/` — EF Core context (`ApplicationDbContext`), `DbConnectionFactory`, Identity models, migrations
- `Controllers/` — Single API controller (`InsuranceRequestController`)
- `Areas/Identity/` — Scaffolded ASP.NET Identity pages
- `SqlScripts/` — DbUp embedded SQL migration scripts
- `Dashboards/` — Dashboard Blazor components
