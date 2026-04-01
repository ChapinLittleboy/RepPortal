
# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

'''bash
# Build
dotnet build RepPortal.sln

# Run (default profile: http://localhost:5197)
dotnet run --project RepPortal.csproj

# Run tests
dotnet test RepPortal.Tests/RepPortal.Tests.csproj

# Run a single test
dotnet test RepPortal.Tests/RepPortal.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
'''

Launch profiles: 'http' (port 5197), 'https' (port 7143), 'IIS Express' (port 43100).

---

## Project Overview

**ASP.NET Core 8 Blazor Server** app for Chapin Manufacturing's sales rep portal. Uses Syncfusion Blazor 30 for UI components (grids, charts, dropdowns).

The application is in active migration from **direct SQL Server access** to **Infor Syteline
IDO/ION API calls** as part of the company's move from on-premise CloudSuite Industrial (CSI v10)
to Infor's multi-tenant cloud ERP. **Migration target: September 1, 2026.**

---

## Architecture Overview

### Multi-Database Pattern

Three SQL Server databases accessed via 'IDbConnectionFactory' (singleton):
- **RepPortal** ('RepPortalConnection') — Identity, audit logs, notifications, Hangfire
- **BAT_App** ('BatAppConnection') — Customer, order, shipment data
- **CustInfo** ('PcfConnection') — Price & Cost File (PCF) data

Primary data access is **Dapper** (60s command timeout). Entity Framework Core is used only for ASP.NET Identity tables.

### Service Layer

'''
Blazor Pages/Components (Pages/, Shared/)
        ↓
Service Layer (Services/)
        ↓
IdoService  ←  all IDO/CSI API calls centralized here
        ↓
CsiRestClient (HTTP transport layer)
        ↓
Infor CSI REST API
'''

Key services:
- 'IdoService' — **Centralized IDO API service.** All ERP data access goes through here.
  Other services inject 'IdoService' rather than 'CsiRestClient' directly.
  ⚠️ **Refactor in progress:** IDO calls are being consolidated from individual services into
  'IdoService'. When touching any service for a report migration, move any remaining direct
  'CsiRestClient' calls into 'IdoService' as part of that same change.
- 'ISalesService' / 'ISalesDataService' — Sales data aggregation across databases
- 'IRepCodeContext' — Scoped service tracking current rep code (multi-tenant filtering)
- 'PcfService' — Price & Cost File management
- 'ExportService' — PDF/Excel export via Syncfusion
- 'CsiRestClient' — Low-level HTTP client for the CSI REST API. Used internally by 'IdoService'
  only — **do not inject 'CsiRestClient' directly** into new or refactored code.
- 'StateContainer' — Scoped Blazor circuit-level state

### Authentication & Authorization

- Windows Authentication (Negotiate) + ASP.NET Identity with email confirmation
- Roles: 'Administrator', 'SalesManager', 'SalesRep', 'User', 'SuperUser', 'HangfireAdmin'
- Custom claims (RepCode, Region, RepID) via 'CustomUserClaimsPrincipalFactory'
- Cookie: '.ChapPortal.Auth', 7-day expiry, no sliding expiration

### Background Jobs

Hangfire with SQL Server storage (schema: 'HangFire', queue: 'reports'):
- 'ReportRunner' — Executes scheduled report jobs
- 'ExpiringPcfNotificationsJob' — Sends PCF expiration email notices
- 'SubscriptionService' — Creates/manages recurring jobs

### Logging

Serilog with separate log files:
- 'Logs/RepPortal-log-.txt' — Main app log (daily, 14-day retention)
- 'Logs/RepPortal-csi-http-.txt' — CSI API calls only (daily)
- 'Logs/RepPortal-expiring-pcf-job-.txt' — PCF notification job (monthly)

### Static Files

Price books served from a network share ('\\ciiws01\ChapinRepDocs') mapped to '/RepDocs' request path.

---

## CSI / IDO API Integration

### Overview

'IdoService' is the single point of access for all Infor CSI ERP data. It wraps 'CsiRestClient'
and provides a consistent interface for IDO calls across the application.

- CSI API options are bound to 'CsiOptions' from config section '"CSI"' in 'appsettings.json'
- HTTP calls are logged separately via 'CsiLoggingHandler' to 'Logs/RepPortal-csi-http-.txt'
- Models use 'CsiFieldAttribute' (in 'Models/') to map C# properties to IDO field names

### IDO Concepts

- **IDO** = Intelligent Data Object — Syteline's abstraction layer over the database
- IDO requests use a filter string similar to a SQL WHERE clause (no 'WHERE' keyword)
- 'Chap_InvoiceLines' is the primary custom IDO for invoice/sales line data and serves as the
  **canonical reference implementation** — review it before implementing any new IDO call

### IDO Call Pattern

All IDO calls go through 'IdoService' (injected via DI). Do not call 'CsiRestClient' directly:

'''csharp
// In a report service — inject IdoService, not CsiRestClient
private readonly IdoService _idoService;

var lines = await _idoService.LoadCollectionAsync<InvoiceLineModel>(
    "Chap_InvoiceLines",
    filter: $"SalesRep = '{repCode}'",
    properties: "SalesRep, InvoiceNo, QtyOrdered, ExtAmt",
    orderBy: "InvoiceDate DESC"
);
'''

> **Refactor note:** Some existing services may still inject 'CsiRestClient' directly — this is
> the old pattern. When touching one of these services as part of a report migration, update it
> to use 'IdoService' instead. Do not copy the old direct 'CsiRestClient' pattern into new code.

### Implementation Checklist for Each Report Migration

When migrating a report from direct SQL to IDO:

1. **Identify the current data source** — direct SQL, stored procedure, or stubbed data
2. **Determine the IDO(s) needed** — check existing IDO definitions or the Syteline IDE
3. **Add/extend a service method** in the appropriate '*Service.cs' using 'IdoService'
4. **If the service still injects 'CsiRestClient' directly**, refactor it to use 'IdoService'
   as part of this same change
5. **Update the Blazor component** to call the new service method (typically in 'OnInitializedAsync'
   or on filter-change events)
6. **Preserve all existing filter parameters** (rep code, date range, region, etc.) — wire them
   to the IDO 'Filter' string
7. **Do not remove Syncfusion component bindings** — only replace the data source behind them
8. **Add a 'CsiFieldAttribute'** to any new model properties that map to IDO field names
9. **Verify the build** ('dotnet build') before considering the work complete

---

## Reports — IDO Migration Status

> Update this table as reports are migrated. Each parallel worktree session owns one report.

| Report | Page Component | Service Method | IDO / Status |
|---|---|---|---|
| Invoice Lines | 'Pages/InvoiceLines.razor' | ISalesService.GetInvoiceRptData | 'Chap_InvoiceLines' ✅ Complete — use as reference |
| Customer List| Pages/CustomerList.razor | CustomerService.GetCustomersDetailsByRepCodeAsync | ⬜ Needs IDO implementation |
| Monthly Sales by Item | Pages/MonthlySalesByItemReport.razor | ISalesService.GetItemSalesReportData | ⬜ Needs IDO implementation |
| Order Lookup| Pages/CustomerOrderLookup.razor | Inline SQL via IDbConnectionFactory | ⬜ Needs IDO implementation |
| Item Pricing Lookup| Pages/GetItemPricing.razor | IItemService.GetItemDetailAsync | ⬜ Needs IDO implementation |
| Packing List | Pages/PackingListPage.razor | PackingListService Rep_Rpt_PackingSlipByBOLSp detail in docs  | ⬜ Needs IDO implementation | 
##Also some Hangfire Reports
| Invoiced Accounts (scheduled| Services/Reports/InvoicedAccountsReport | sp_GetInvoices detail in docs  | ⬜ Needs IDO implementation |
| Shipments (scheduled)| Services/Reports/ShipmentsReport.cs| Raw sql | ⬜ Needs IDO implementation |



---

## Coding Conventions

- **Dapper over EF Core** for all non-Identity, non-ERP data access. Show full commands with parameters.
- **T-SQL** for all SQL. Use CTEs for complex queries, table variables for intermediate results. Avoid SQL 2019+ features.
- **'var' only when type is obvious.** Favor explicitness.
- **Blazor components**: prefer code-behind or partial classes for non-trivial logic.
- **Syncfusion Blazor** patterns for UI (SfGrid, SfPivotView, dropdowns, etc.).
- Use existing services/context for role-based access and impersonation — do not create new auth mechanisms.
- Do not introduce external packages not already in the stack without explicit approval.
- Show full methods/classes, not fragments. Include necessary 'using' statements.
- SQL scripts go in 'SqlScripts/' as embedded resources (DbUp migration pattern).
- Service methods are 'async Task<T>' and follow the naming pattern 'Get[ReportName]Async()'.

---

## What NOT to Do

- ❌ Do not add new direct SQL queries ('SqlConnection', 'SqlCommand', 'Dapper') against ERP source databases for report data — use 'IdoService' instead
- ❌ Do not inject 'CsiRestClient' directly into pages, components, or report services — use 'IdoService' instead
- ❌ Do not modify 'Chap_InvoiceLines' IDO — it is deployed and in use
- ❌ Do not change Syncfusion component markup unless data binding requires it
- ❌ Do not commit connection strings or credentials
- ❌ Do not introduce new NuGet packages without approval

---

## Key Configuration

- 'appsettings.json': Connection strings, CSI API config ('Csi' section), SMTP ('Smtp'), OpenAI, PriceBooks paths
- 'appsettings.Development.json': Dev overrides
- CSI API options bound to 'CsiOptions' from config section '"CSI"'
- Syncfusion license registered in 'Program.cs' (v30)

---

## Project Structure

- 'Pages/' — Blazor page components (36 .razor files)
- 'Shared/' — Layout and shared components (MainLayout, NavMenu, Appbar, RepCodeSwitcher)
- 'Services/' — Business logic and data access (44 service classes)
- 'Services/Reports/' — Hangfire report jobs and report implementations
- 'Services/ReportExport/' — Excel/PDF export logic
- 'Models/' — Data models (39 classes), includes 'CsiFieldAttribute' for CSI API field mapping
- 'Data/' — EF Core context ('ApplicationDbContext'), 'DbConnectionFactory', Identity models, migrations
- 'Controllers/' — Single API controller ('InsuranceRequestController')
- 'Areas/Identity/' — Scaffolded ASP.NET Identity pages
- 'SqlScripts/' — DbUp embedded SQL migration scripts
- 'Dashboards/' — Dashboard Blazor components

---

## Parallel Worktree Notes

When running parallel Claude Code sessions for report migrations:

- Each session works on **one report** in its own Git worktree
- Worktree naming: '../RepPortal-report-[name]/' → branch 'feature/ido-[name]'
- Merge target: 'api-dev'
- Before merging: confirm 'dotnet build' passes, no direct SQL against ERP databases remains
  in touched files, and no remaining direct 'CsiRestClient' injections in touched services
