# SyteLine Cloud Migration Tasks

This document tracks replacing direct SQL access to SyteLine databases with API calls.

Direct SQL against `RepPortal` is still allowed. Only queries that read from or execute against `Bat_App` or `Kent_App` are included below.

## High Priority
Core business operations.

- [ ] Replace `Bat_App` customer master lookups used by `CustomerService` (`GetCustomersByRepCodeAsync`, `GetCustomersDetailsByRepCodeAsync`, `GetCustomerNamesByRepCodeAsync`, `GetCustomerTypesAsync`, `GetCustomerTypesListAsync`, `GetExcludedCustomerListAsync`, `GetAllReasonCodesAsync`).
- [ ] Replace `Bat_App` credit-hold customer lookup in `CreditHoldExclusionService.GetAllExcludedCustNumsAsync`.
- [ ] Replace `Bat_App` order lookup queries in `Pages/CustomerOrderLookup.razor` (`LoadOrderAsync` header and line-item queries).
- [ ] Replace `Bat_App` item master and pricing queries in `ItemService` (`GetItemsAsync`, `GetItemDetailAsync`).
- [ ] Replace `Bat_App` and `Kent_App` sales-report SQL in `SalesService` and `SalesDataService` (`GetSalesReportData`, `GetSalesReportDataUsingInvRep`, `GetItemSalesReportData`, `GetItemSalesReportDataWithQty`, shared pivot/query builders).
- [ ] Remove remaining `Bat_App` SQL dependencies from API report paths in `SalesService` (`GetSalesReportDataApiAsync`, `GetSalesReportDataUsingInvRepApiAsync`) that still read `Chap_RegionNames`.
- [ ] Replace `Bat_App` / `Kent_App` shipment and packing-list access in `PackingListService`, `SalesService.GetShipmentsData`, and `Services/Reports/ShipmentsReport`.
- [ ] Replace `Bat_App` open-order detail SQL branch in `SalesService.GetAllOpenOrderDetailsAsync`.
- [ ] Replace `Bat_App` / `Kent_App` recent-sales and monthly item sales data in `SalesService.GetRecentSalesAsync` and the `MonthlySalesByItemReport` path.
- [ ] Replace `Bat_App` customer/order helper lookup in `SalesService.GetCustNumFromCoNum`.
- [ ] Replace `Bat_App` enrichment queries used by `PcfService` (`GetAllowedCustomerNumbersAsync`, `GetPCFHeaderWithItemsAsync`, `GetPCFHeaderWithItemsNoRepAsync`).

## Medium Priority
Supporting functionality.

- [ ] Replace `Bat_App` rep, agency, and region metadata lookups in `SalesService` (`GetRepAgency`, `GetAllRepCodesAsync`, `GetRegionsForRepCodeAsync`, `GetRegionInfoForRepCodeAsync`, `GetAllRepCodeInfoAsync`, `GetAllRegionsAsync`).
- [ ] Replace `Bat_App` IDO metadata queries in `Pages/IdoPropsBuilder.razor` (`GetSlIdoListAsync`, `GetFinalIdoPropertiesAsync`).
- [ ] Replace `Bat_App` dashboard queries in `Dashboards/TopCustomers.razor` (`GetTopCustomers`, `GetCustomerMonthlyData`).
- [ ] Verify whether `SalesService.RunDynamicQueryAsync` is still used; if it is, replace the `Bat_App` SQL execution path with approved API-backed queries. Needs verification.
- [ ] Verify whether `SalesService.GetInvoiceRptData` and `InvoicedAccountsReport.GetAsync` are safe to keep because they call `RepPortal.dbo.sp_GetInvoices`; if that stored procedure reaches `Bat_App` or `Kent_App`, migrate it to API-backed logic. Needs verification.
- [ ] Verify whether `SalesService.GetShipmentsData` executes `RepPortal_GetShipmentsSp` from `Bat_App`; if so, replace it with the existing API shipment flow or move the logic fully into `RepPortal`. Needs verification.

## Low Priority
Cleanup and refactoring.

- [ ] Remove unused SQL-only paths once the `Csi:UseApi` flow is the default and validated.
- [ ] Remove or lock down generic SQL execution helpers that can bypass the SyteLine API strategy.
- [ ] Consolidate duplicate sales-report query logic currently split between `SalesService` and `SalesDataService`.
- [ ] Centralize SyteLine site selection (`BAT` vs `KENT`) behind a shared API service instead of per-feature connection handling.

## Suggested Refactors

- Build a shared `SyteLineCustomerApiService` for customer, address, region, credit-hold, and rep metadata so `CustomerService`, `CreditHoldExclusionService`, `PcfService`, and `SalesService` stop reimplementing the same customer lookups.
- Build a shared `SyteLineSalesApiService` for invoice, shipment, open-order, and item-sales report retrieval so `SalesService`, `SalesDataService`, `ShipmentsReport`, and dashboard pages use the same API composition rules.
- Replace direct `Chap_RegionNames` SQL lookups with one API-backed metadata provider or a RepPortal-owned cached lookup table if the API does not expose region names directly.
- Move `BAT`/`KENT` packing-list orchestration behind a dedicated API adapter so `PackingListService` no longer manages per-site SQL connections and stored procedures.
- Separate SyteLine access from PCF-specific SQL so `PcfService` can keep `custinfo` access while calling shared API services for item, customer, terms, rep agency, and email metadata.
- Audit pages and reports that still call SQL-only methods even though API alternatives already exist, then switch those call sites to the API path before removing the SQL fallback.
