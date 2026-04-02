# RepPortal Test Plan

## Current State

- `RepPortal.Tests` exists, but coverage is still very light.
- The current test file (`RepPortal.Tests/UnitTest1.cs`) contains a few starter tests for `SalesService`, but it does not yet cover the highest-risk workflows in the application.
- The project has several service-heavy areas with business rules, dynamic SQL/query generation, API mapping, Hangfire scheduling, Identity behavior, and controller endpoints. Those should be the first targets.

## Recommended Test Strategy

Use three layers of tests:

1. Unit tests
   - Fast tests for business rules, normalization, query/filter generation, mapping, and guard clauses.
   - These should make up most of the suite.

2. Integration tests
   - Focus on ASP.NET Core wiring, controller behavior, Identity-related flows, DI registration, and selected data-access paths.
   - Use these for places where mocking everything would hide the real risk.

3. Component/UI tests
   - Add focused Blazor component tests only for pages/components with meaningful branching or role/rep-code behavior.
   - Do not try to cover every `.razor` file immediately.

## Test Infrastructure To Add

Recommended NuGet additions to `RepPortal.Tests`:

- `FluentAssertions`
- `Microsoft.AspNetCore.Mvc.Testing`
- `Microsoft.EntityFrameworkCore.InMemory` or `Microsoft.EntityFrameworkCore.Sqlite`
- `bunit`
- `Respawn` if database-backed integration tests grow

Recommended test folders:

- `RepPortal.Tests/Unit/Services`
- `RepPortal.Tests/Unit/Controllers`
- `RepPortal.Tests/Unit/Support`
- `RepPortal.Tests/Integration`
- `RepPortal.Tests/Components`

## Priority 1: Tests That Should Be Created First

These are the most valuable tests to add first because they cover core business logic and failure-prone behavior.

### 1. `SubscriptionService` tests

File under test: `Services/SubscriptionService.cs`

Create tests for:

- `RegisterOrUpdateJob` builds a stable recurring job ID for the same logical subscription.
- Non-scoped report types ignore `customerId` and `dateRangeCode`.
- Scoped report types (`InvoicedAccounts`, `Shipments`) preserve trimmed `customerId`.
- Invalid `dateRangeCode` values are normalized to `null`.
- Valid `dateRangeCode` values are preserved.
- Invalid time zone IDs fall back to `Eastern Standard Time`.
- If Eastern cannot be resolved, fallback is `UTC`.
- `RemoveJob` computes the same job ID as `RegisterOrUpdateJob`.

Why first:

- This is pure business logic, easy to test, and directly affects scheduled report correctness.

### 2. `IdentityEmailDomainMigration` tests

File under test: `Services/IdentityEmailDomainMigration.cs`

Create tests for:

- Users on `@chapinmfg.com` are updated to `@chapinusa.com`.
- Users already on the new domain are skipped.
- Users with blank email are skipped.
- Missing users from the snapshot are skipped.
- Email collisions cause skip, not update.
- Username collisions cause skip, not update.
- `UpdateAsync` failures are counted and logged as failures.
- A thrown exception during one user update does not stop the entire migration.

Why first:

- This is a one-off migration type of workflow where a bad bug can silently damage account data.

### 3. `InsuranceRequestController` tests

File under test: `Controllers/InsuranceRequestController.cs`

Create tests for:

- Missing `data` form field returns `400 BadRequest`.
- Invalid JSON throws or is translated into a failure result, depending on desired behavior.
- Valid multipart form calls `SaveRequestAsync` exactly once.
- Returned `id` is surfaced in `200 OK`.
- Uploaded files are passed through to the service.

Why first:

- This is a public HTTP entry point with file upload handling and JSON deserialization.

### 4. `PackingListService` tests

File under test: `Services/PackingListService.cs`

Create tests for:

- Constructor throws when `UseApi = false` and no `Sites` configuration is provided.
- `GetPackingListByShipmentAsync(packNum)` routes to `IIdoService` when `UseApi = true`.
- Blank `packNum` returns an empty `PackingList`.
- Unknown site throws a clear `ArgumentException`.
- `GetShipmentKeysByOrderAsync` returns empty list for blank `coNum`.
- `GetShipmentsByOrderAsync` returns empty list when no shipment keys are found.
- `GetShipmentsByOrderAsync` de-duplicates `(site, pack_num)` pairs.
- `GetShipmentsByOrderAsync` fans out shipment loading and returns only hydrated packing lists.
- Data-table mapping populates header and items correctly when expected columns exist.
- Missing optional columns in the returned table do not break mapping.

Why first:

- This service has branching between SQL and API paths plus fragile tabular mapping logic.

### 5. `SalesService` unit tests for logic-only seams

File under test: `Services/SalesService.cs`

Expand coverage for:

- `GetRepCodeByRegistrationCodeAsync` returns `null` for blank input.
- `GetCustNumFromCoNum` returns `null` for blank input.
- `GetRepIDAsync` returns `null` when claim is absent.
- `GetCurrentRepCode` returns the current rep code from context.
- Guard methods throw when required dependencies are missing.
- `GetDynamicQueryForItemsMonthlyWithQty` includes region filter only when allowed regions are provided.
- `BuildSalesPivotQuery` generates fiscal-year columns for the current fiscal year layout.
- `BuildItemsMonthlyQuery` includes current/prior FY rollups.
- `MaterializeToDictionaries` preserves row values and keys.
- `GetSalesReportDataApiAsync` delegates to `IIdoService` and logs usage.
- `GetSalesReportDataUsingInvRepApiAsync` delegates to `IIdoService` and logs usage.
- `GetItemSalesReportDataWithQtyApiAsync` delegates to `IIdoService` and logs usage.
- `GetInvoiceRptData` copies rep/region security values into the request parameters before executing.
- `GetInvoiceRptData` filters final results by `CoNum` when provided.

Why first:

- `SalesService` is one of the central business services and already has a test foothold.

## Priority 2: High-Value Service Tests

### 6. `IdoService` tests

File under test: `Services/IdoService.cs`

Create tests for:

- `ConvertTo` correctly handles:
  - `string`
  - nullable and non-nullable `int`
  - nullable and non-nullable `decimal`
  - supported `DateTime` formats
  - empty input for nullable targets
  - invalid input throwing `FormatException`
- `MapRow<T>` maps `[CsiField]` properties case-insensitively.
- `GetAllOpenOrderDetailsAsync` builds filters correctly with and without sales regions.
- `GetAllOpenOrderDetailsAsync` includes cutoff date only when configured.
- Non-zero CSI `MessageCode` raises `InvalidOperationException`.
- `GetShipmentsDataAsync` filters shipment rows to allowed customers when region-restricted.
- `GetShipmentsDataAsync` enriches returned shipments with BOL fields when available.
- `GetShipmentsDataAsync` returns an empty list on mapping exception after logging.
- `GetInvoiceRptDataAsync` returns empty list when header query returns no rows.
- `GetInvoiceRptDataAsync` joins header, invoice item, and order item data correctly.
- `GetPackingListByShipmentAsync` builds header/items from SLCoShips and `ait_ss_bols`.
- `GetPackingListsByOrderAsync` groups shipments by BOL number correctly.
- Not-implemented methods continue throwing `NotImplementedException` until implemented.

Why this matters:

- `IdoService` is a large mapping/orchestration layer against external CSI APIs, so regressions here can be subtle and expensive.

### 7. `ReportRunner` tests

File under test: `Services/ReportRunner.cs`

Create tests for:

- `RunAsync` throws for unsupported `ReportType`.
- `Normalize` strips invalid scope data for unsupported report types.
- `ToDateRange` returns the correct start/end for:
  - `CurrentMonth`
  - `PriorMonth`
  - `PriorAndCurrentMonth`
  - `AllDates`
  - invalid code default path
- `ClampToSqlDateTime` enforces SQL min/max and business minimum date.
- `RunAsync` throws when no user context exists for the email.
- `RunAsync` routes `InvoicedAccounts` to the invoiced report path.
- `RunAsync` routes `ExpiringPCFNotications` to the notification job.
- `RunInvoicedAccountsAsync` sends an email with an Excel attachment and expected filename pattern.

Important note:

- `ToDataTable` and `BuildExcel` currently throw `NotImplementedException`. Either implement them or keep tests focused on the paths already using `_excel.Export`.

### 8. `RepCodeContext` tests

File under test: `Services/RepCodeContext.cs`

Create tests for:

- Default values come from the current user context/claims.
- `OverrideRepCode(string)` changes current rep code.
- `OverrideRepCode(string, List<string>)` changes rep code and region set together.
- `ResetRepCode()` restores original state.
- Administrator detection behaves correctly.
- Current/assigned region behavior is correct before and after override.

Why this matters:

- Many pages and services depend on rep/region scoping, so this context object is foundational.

### 9. `PcfPdfAssetResolver` tests

File under test: `Services/PcfPdfAssetResolver.cs`

Create tests for:

- Valid asset names resolve to expected absolute paths.
- Invalid names are rejected or handled safely.
- Path traversal attempts such as `..\..\secret.txt` are not allowed.

Why this matters:

- This is a low-effort, high-safety test area.

## Priority 3: Data/Repository Style Services

Add focused tests around input validation, SQL intent, and edge cases for:

- `Services/CreditHoldExclusionService.cs`
- `Services/FolderAdminService.cs`
- `Services/HelpContentService.cs`
- `Services/MarketingFileService.cs`
- `Services/PivotLayoutService.cs`
- `Services/PageDefinitionService.cs`
- `Services/PriceBookService.cs`
- `Services/ItemService.cs`
- `Services/UsageAnalyticsService.cs`
- `Services/UserContextResolver.cs`
- `Services/Reports/PcfNotificationLogRepository.cs`

Suggested behaviors to cover:

- Empty-result handling.
- Null and invalid input handling.
- Duplicate-name or rename collision rules where applicable.
- Correct persistence/update/delete behavior.
- Filtering and sorting expectations.
- Logging on failure paths.

These services are good candidates for integration tests with a disposable SQL Server/SQLite substitute where practical.

## Priority 4: Startup and Integration Tests

File under test: `Program.cs`

Create integration tests for:

- The application starts successfully with test configuration.
- Required services are registered in DI:
  - `ISalesService`
  - `IIdoService`
  - `PackingListService`
  - `SubscriptionService`
  - `IInsuranceRequestService`
  - `IExcelReportExporter`
- `/api/InsuranceRequest` requires authorization.
- Rate limiting is applied to `Identity/Account/ForgotPassword`.
- The `/chapinrep*` redirect middleware redirects to `/`.
- Hangfire dashboard authorization policy is present.
- JSON serialization preserves property names as configured.

Why this matters:

- `Program.cs` contains a lot of critical composition logic that can drift silently over time.

## Priority 5: Blazor Component Tests

Start with components/pages that contain real branching, not static markup.

Recommended first candidates:

- `Shared/RepCodeSwitcher.razor`
- `Shared/NavMenu.razor`
- `Pages/Subscriptions.razor`
- `Pages/PackingListPage.razor`
- `Pages/CustomerList.razor`
- `Pages/UsageDashboard.razor`

Create tests for:

- Role-based visibility.
- Rep-code/region-dependent rendering.
- Validation message display.
- Empty/loading/error state rendering.
- Event callbacks firing expected service calls.

Use `bUnit` here instead of end-to-end browser tests unless you need full JS/browser behavior.

## Suggested Initial Test Backlog

If we want the most progress for the least effort, build in this order:

1. `SubscriptionServiceTests`
2. `IdentityEmailDomainMigrationTests`
3. `InsuranceRequestControllerTests`
4. `PackingListServiceTests`
5. `SalesServiceTests` expansion
6. `IdoServiceMappingTests`
7. `ReportRunnerTests`
8. `ProgramIntegrationTests`
9. First `bUnit` tests for `Subscriptions` and `RepCodeSwitcher`

## Naming Recommendations

Use clear test names in the format:

- `MethodName_ShouldExpectedBehavior_WhenCondition`

Examples:

- `RegisterOrUpdateJob_ShouldIgnoreCustomerId_WhenReportTypeIsNotScoped`
- `RunAsync_ShouldThrow_WhenUserContextCannotBeResolved`
- `Post_ShouldReturnBadRequest_WhenDataFieldIsMissing`
- `GetShipmentKeysByOrderAsync_ShouldDeduplicateBySiteAndPackNumber`

## Coverage Goal Recommendation

Short-term target:

- 60%+ coverage on service classes with business logic.

Medium-term target:

- 75%+ coverage on `SubscriptionService`, `IdentityEmailDomainMigration`, `PackingListService`, `InsuranceRequestController`, and the logic-heavy portions of `SalesService`/`IdoService`.

Do not optimize for overall percentage first. Optimize for risk-covered behavior first.

## Immediate Next Step

The best first implementation pass would be:

1. Clean up `RepPortal.Tests/UnitTest1.cs` into a real `SalesServiceTests.cs`.
2. Add new test classes for:
   - `SubscriptionServiceTests`
   - `IdentityEmailDomainMigrationTests`
   - `InsuranceRequestControllerTests`
   - `PackingListServiceTests`
3. Add test helpers/builders for:
   - fake `AuthenticationStateProvider`
   - fake `HttpContext` with multipart form data
   - test configuration factory for `IConfiguration` and `IOptions<CsiOptions>`

That would give RepPortal a strong base to grow from without overbuilding the test project up front.
