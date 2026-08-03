# RepPortal ION API Migration Plan

Date: 2026-08-03  
Target environment: SyteLine MT Cloud TRN  
Source branch reviewed: `api-dev`

## Purpose

RepPortal currently has an API path that reads SyteLine IDOs through the on-premises `MGRestService.svc` endpoint. The goal is to route that existing API functionality through Infor ION API Gateway to the MT Cloud TRN tenant, using the working authentication and endpoint conventions demonstrated by `IonApiTester`.

This document describes the required work before application code is changed. It separates the transport/authentication migration from the larger effort to remove every remaining direct SQL dependency on the on-premises SyteLine databases.

## Recommended scope

The first implementation should migrate the existing `IIdoService` HTTP traffic from on-premises MGRest to ION Gateway while preserving its current request URLs, query parameters, response models, and business mapping.

The current calls use the legacy route:

```text
json/{IDO}/adv
```

IonApiTester proves that this route can be reached through ION at:

```text
{IonApiUrl}/{TenantId}/CSI/IDORequestService/MGRestService.svc/json/{IDO}/adv
```

Keeping the legacy route for this phase avoids rewriting the large `IdoService` response-mapping layer. Migration to the REST v2 `/ido/load/{IDO}` contract can be considered later as a separate refactor.

## Current and target request flow

| Concern | Current RepPortal behavior | Target behavior |
| --- | --- | --- |
| SyteLine host | On-premises `ciiutil10` | ION API Gateway TRN URL |
| API base path | `/IDORequestService/MGRestService.svc` | `/{tenant}/CSI/IDORequestService/MGRestService.svc` |
| Authentication | Static Authorization values | OAuth bearer token obtained from the ION token endpoint |
| Token lifetime | Static value | Cache until shortly before expiration; refresh automatically |
| SyteLine configuration | Not populated in `MongooseConfig`; BAT/KENT use authorization overrides | Send `X-Infor-MongooseConfig` on every SyteLine request |
| BAT/KENT routing | Different Authorization values | Named BAT and KENT Mongoose configuration values |
| Response contract | Legacy MGRest `MessageCode`, `Message`, `Items` | Preserve this contract in the first phase |
| Feature switch | `Csi:UseApi` | Retain until TRN parity testing is complete |

## Existing functionality affected

`Services/IdoService.cs` currently calls these IDOs:

| IDO | Primary use |
| --- | --- |
| `SLCoitems` | Open-order details and order-related lookups |
| `SLCustomers` | Customer, rep, region, credit-hold, and corporate-customer data |
| `SLCoShips` | Shipment and packing-list data |
| `ait_ss_bols` | Custom BOL and packing-list data |
| `SLInvHdrs` | Invoice headers and sales reporting |
| `SLInvItemAlls` | Invoice-line and item-sales reporting |
| `Chap_InvoiceLines` | Custom combined invoice-line reporting |
| `SLCustAddrs` | Bill-to and ship-to address enrichment |
| `SLItems` | Item details and item status |
| `SLItemprices` | Current item pricing |

Before enabling the cloud path, each IDO and every requested custom property must be verified in the BAT and KENT TRN configurations. The two custom IDOs, `ait_ss_bols` and `Chap_InvoiceLines`, are the highest deployment risk because they must exist in MT Cloud with compatible property names and permissions.

## Required implementation

### 1. Add ION configuration models

Replace the transport-related fields in `Models/CsiOptions.cs` with options that represent the ION profile and named SyteLine configurations. Suggested shape:

```json
{
  "Csi": {
    "UseApi": false,
    "IonApiUrl": "configured outside source control",
    "TenantId": "configured outside source control",
    "OAuthBaseUrl": "configured outside source control",
    "OAuthTokenPath": "token.oauth2",
    "Configurations": {
      "BAT": "TRN BAT Mongoose configuration",
      "KENT": "TRN KENT Mongoose configuration"
    }
  }
}
```

The credentials represented by the `.ionapi` keys `ci`, `cs`, `saak`, and `sask` should map to strongly named options such as `ClientId`, `ClientSecret`, `ServiceAccountKey`, and `ServiceAccountSecret`.

RepPortal is a server application, so it should not depend on IonApiTester's `%LOCALAPPDATA%` profile store. Values can be copied from the approved TRN `.ionapi` profile into deployment configuration, but secrets must come from environment variables, .NET user-secrets for local development, or the production secret provider.

Add startup validation for all required URLs, identifiers, credentials, and the BAT configuration. Validate KENT when KENT reporting is enabled.

### 2. Add a thread-safe ION token provider

Extract the working token logic from IonApiTester into a RepPortal service, for example `IIonTokenProvider` and `IonTokenProvider`.

The provider must:

- POST to `{OAuthBaseUrl}/{OAuthTokenPath}`.
- Use HTTP Basic authentication with `ClientId:ClientSecret`.
- Send form fields `grant_type=password`, `username=ServiceAccountKey`, and `password=ServiceAccountSecret`.
- Read `access_token` and `expires_in`.
- Cache the token in memory and refresh it at least 60 seconds before expiration.
- Use a lock or `SemaphoreSlim` so simultaneous Blazor requests and Hangfire jobs do not request duplicate tokens.
- Allow invalidation and one retry when ION returns `401 Unauthorized`.
- Never log credentials, token payloads, or bearer tokens.

The token provider should be registered as a singleton because the service-account token is application-scoped. Its `HttpClient` should still come from `IHttpClientFactory`.

### 3. Convert `CsiRestClient` to ION transport

Update `Services/CsiRestClient.cs` so every request:

1. Builds the ION legacy base URL.
2. Obtains a valid bearer token.
3. sets `Authorization: Bearer {token}`.
4. Sets `X-Infor-MongooseConfig` to the selected named configuration.
5. Sends the existing `json/{IDO}/adv` route and query parameters.

Replace the current overload that accepts `authorizationOverride` with explicit configuration routing, for example:

```csharp
Task<string> GetAsync(
    string relativeUrl,
    IDictionary<string, string>? query = null,
    string site = "BAT",
    CancellationToken cancellationToken = default);
```

The client should translate `BAT` or `KENT` through `CsiOptions.Configurations`. Business services must not handle raw authorization values.

The existing `X-Infor-MongooseSessionType: CustomUser` header is not used by IonApiTester. Remove it unless TRN testing demonstrates that it is required through ION.

### 4. Replace BAT/KENT authorization switching

`IdoService` currently builds BAT/KENT site lists using `KentAuthorization` and passes raw authorization overrides into `CsiRestClient`. Replace those tuples with site/configuration identifiers:

```text
(BAT, configured BAT Mongoose configuration)
(KENT, configured KENT Mongoose configuration)
```

This affects the combined sales and invoice flows, including the KENT fallback that uses `SLInvHdrs` and `SLInvItemAlls` when `Chap_InvoiceLines` is unavailable.

### 5. Preserve and strengthen error handling

Continue checking both HTTP status and legacy MGRest `MessageCode`. Add explicit handling for:

- `401`: invalidate the cached token and retry once.
- `403`: report missing ION suite/API or Mongoose authorization without retrying.
- `404`: distinguish an incorrect gateway route from a missing custom IDO.
- `429`: honor `Retry-After` for a bounded retry or return a clear throttling error.
- `5xx`: return an actionable gateway/SyteLine service error.
- HTML responses: retain the current guard, but identify the request as an ION Gateway response rather than MGRest alone.

Update `CsiLoggingHandler` to retain method, sanitized URL, status, elapsed time, and selected site/configuration. It must continue excluding Authorization and should not log token responses or secret-bearing form bodies.

### 6. Clean up dependency injection

`Program.cs` registers `IIdoService` four times. Consolidate this to one registration while changing the HTTP/token services. Add validated options and the token provider, then retain `CsiLoggingHandler` on the SyteLine client.

### 7. Remove exposed legacy credentials

The reviewed `appsettings.json` contains credential-like values under `Authorization`, `AuthorizationFT05`, and `KentAuthorization`. These values must be removed from source control and rotated before deployment. Git history should be treated as exposed unless the credentials are already expired or revoked.

Do not replace them with `.ionapi` secrets in `appsettings.json`. Use deployment secrets or environment variables, and add safe placeholders/documentation only.

## Direct SQL boundary

This transport change does not, by itself, make RepPortal independent of on-premises SyteLine databases.

The existing `Csi:UseApi` branches already cover several customer, item, shipment, invoice, sales, and packing-list workflows. However:

- `IIdoService.GetItemSalesReportDataAsync` is still unimplemented.
- `IIdoService.GetOrderLookupAsync` is still unimplemented.
- Several services retain direct `Bat_App` or `Kent_App` queries for rep, region, customer type, dashboard, PCF, and helper data.
- Some API methods still enrich results from local BAT tables.
- RepPortal's own application database access remains valid and is not part of the SyteLine migration.

After the ION transport is stable, use `docs/TODO.md` as the backlog for eliminating the remaining SyteLine SQL dependencies. Do not remove SQL fallbacks as part of the initial transport change.

## Verification plan

### Automated tests

Add unit tests for:

- Token acquisition request URL, Basic client authentication, and password-grant form values.
- Token caching and refresh before expiration.
- Concurrent callers sharing one token acquisition.
- `401` invalidation and exactly one retry.
- ION legacy base URL construction.
- BAT and KENT `X-Infor-MongooseConfig` selection.
- Authorization header redaction.
- Proper propagation of `403`, `404`, `429`, gateway `5xx`, HTML responses, and MGRest `MessageCode` failures.
- Existing query escaping and MGRest row mapping.

Update integration tests to verify the new services resolve and that missing required ION configuration fails with a clear startup-validation message. Use fake HTTP handlers; tests must never contact TRN or contain real credentials.

### TRN smoke tests

Using the same TRN service account and configurations proven in IonApiTester:

1. Authenticate and query one row from `SLCustomers` in BAT.
2. Repeat against the KENT configuration.
3. Verify each of the ten IDOs in the inventory above.
4. Verify all custom properties used by `IdoService`.
5. Run representative portal workflows for customers, item detail, open orders, shipments, packing lists, invoiced accounts, and sales reports.
6. Compare row counts and selected totals with the current on-premises API/SQL result for the same inputs and dates.
7. Exercise Hangfire reports to confirm token sharing works outside an interactive Blazor circuit.
8. Review logs to confirm no credentials or bearer tokens are emitted.

### Rollout

1. Deploy with `Csi:UseApi=false` and the ION configuration present.
2. Run an authenticated health/smoke check against TRN.
3. Enable `UseApi` in TRN only.
4. Complete parity testing and address missing cloud IDOs/properties.
5. Promote the same code with environment-specific ION and Mongoose configuration values.
6. Retain the SQL fallback until production parity and operational monitoring are accepted.

## Expected code changes

| File/area | Planned change |
| --- | --- |
| `Models/CsiOptions.cs` | Replace static authorization fields with ION profile and named-configuration options |
| New token-provider files under `Services` | Acquire, cache, refresh, and invalidate ION bearer tokens |
| `Services/CsiRestClient.cs` | Route legacy MGRest requests through ION and attach bearer/configuration headers |
| `Services/IdoService.cs` | Replace authorization overrides with BAT/KENT configuration selection |
| `Services/CsiLoggingHandler.cs` | Add ION-aware diagnostics while maintaining secret redaction |
| `Program.cs` | Register validated options, token provider, and a single `IIdoService` registration |
| `appsettings*.json` and deployment configuration | Remove legacy Authorization values; add non-secret structure and external secret mappings |
| `RepPortal.Tests` | Add token, routing, retry, error, and DI tests |

## Implementation decision

Proceed first with the transport/authentication migration while preserving the legacy MGRest response contract. Treat complete removal of on-premises SyteLine SQL as a follow-on project governed by `docs/TODO.md`. This keeps the first change reviewable, allows side-by-side TRN validation, and limits business-logic churn while the cloud connection is proven.

## Implementation status

Implementation began on 2026-08-03. The code now includes ION OAuth token acquisition and caching, legacy MGRest routing through the TRN gateway, BAT/KENT Mongoose configuration selection, one token-refresh retry on `401`, bounded `429` handling, startup validation when `Csi:UseApi` is enabled, and removal of the legacy authorization values from source-controlled settings. See `docs/ION-TRN-Configuration.md` for local and deployed secret configuration.

TRN parity testing and confirmation of the KENT Mongoose configuration remain open. The saved IonApiTester state confirms the BAT configuration but does not contain a confirmed KENT configuration.
