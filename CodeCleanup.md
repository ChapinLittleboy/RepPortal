# RepPortal — Code Cleanup Checklist

Generated: 2026-04-10  
This file documents code quality issues found across the RepPortal codebase. No code has been changed yet.

---

## Priority 1 — Correctness / Bugs

### 1.1 Duplicate `_idoService` Assignment (`Services/SalesService.cs`)
`_idoService = idoService;` appears on **both line 51 and line 54** of the primary DI constructor. The second assignment is redundant and masks the fact that the `_csiRestClient` assignment between them was the developer's stopping point.

**Fix:** Remove the duplicate assignment on line 54.

---

### 1.2 `NullReferenceException` Risk in `Customer` Computed Properties (`Models/Customer.cs`)
```csharp
public string? DisplayCustName => Cust_Name.Replace("&", "(and)");  // line 116
public string? CustNameWithNum => $"{Cust_Name} ({Cust_Num.Trim()})";  // line 118
```
Both properties dereference nullable strings without a null check. The return type is annotated `string?` but the body throws if the fields are null.

**Fix:** Use null-conditional operators: `Cust_Name?.Replace(...)` and guard `Cust_Num` similarly.

---

### 1.3 Two Properties Mapped to the Same CSI Field (`Models/Customer.cs`)
- `Cust_Name` and `BillToName` both carry `[CsiField("Name")]` (lines 12 and 27).
- `PricingMethod` and `PricingCode` both carry `[CsiField("cusUf_PROG_BASIS")]` (lines 63 and 100).

Duplicate attribute mappings mean only one property will be populated by `MapRow<T>`, with which one winning being undefined. This is a latent data bug.

**Fix:** Determine the correct mapping for each pair and remove or remap the duplicate.

---

### 1.4 Unimplemented Methods Still Registered on `IIdoService` (`Services/IdoService.cs`)
```csharp
public Task<List<Dictionary<string, object>>> GetItemSalesReportDataAsync(...)
    => throw new NotImplementedException("GetItemSalesReportDataAsync IDO implementation pending.");  // line 1478

public Task<(OrderLookupHeader? Header, List<OrderLookupLine> Lines)> GetOrderLookupAsync(...)
    => throw new NotImplementedException("GetOrderLookupAsync IDO implementation pending.");  // line 1481
```
The CLAUDE.md migration table shows these reports as ✅ Complete. If the real implementations live in `SalesService`, these stubs in `IdoService` conflict with the interface contract and will throw at runtime if called.

**Fix:** Either implement these methods in `IdoService` or remove them from the `IIdoService` interface if they are intentionally not IDO-backed.

---

### 1.5 Unimplemented Private Helpers in `ReportRunner.cs` (lines 150, 153)
```csharp
private static System.Data.DataTable ToDataTable(List<Dictionary<string, object>> data)
    => throw new NotImplementedException();

private static byte[] BuildExcel(System.Data.DataTable dt, string sheetName)
    => throw new NotImplementedException();
```
These are dead stubs left from incomplete work. They will crash at runtime if reached.

**Fix:** Implement them or delete them if they are no longer needed.

---

## Priority 2 — Architecture / Design Violations

### 2.1 `SalesService` Still Injects `ICsiRestClient` Directly (`Services/SalesService.cs`)
`_csiRestClient` is declared (line 28) and injected via the primary constructor (line 41). Per `CLAUDE.md`, `CsiRestClient` must not be injected into services other than `IdoService`.

**Fix:** Remove `ICsiRestClient` from `SalesService`'s constructor and field list. Route any remaining direct API calls through `_idoService`.

---

### 2.2 Nullable Dependency Anti-Pattern in `SalesService.cs`
All injected dependencies are declared as nullable (`IRepCodeContext?`, `IDbConnectionFactory?`, etc.) to support a "convenience constructor" for tests. This means every method must null-check its own dependencies at runtime, or risk a `NullReferenceException`.

**Fix:** Remove the convenience `SalesService(string connectionString)` constructor. Use a proper test double or `WebApplicationFactory` setup in tests instead. Make all primary dependencies non-nullable.

---

### 2.3 `SalesService.cs` Is 1,812 Lines — God Class
`IdoService.cs` is 1,736 lines. These files contain too many responsibilities. They are difficult to navigate, test, and maintain.

**Fix (longer-term):** Decompose `SalesService` into focused sub-services per report domain (e.g., `OrderLookupService`, `ShipmentsService`, `CustomerProgramsService`). Similarly, consider splitting `IdoService` by domain area.

---

### 2.4 Blazor Pages Without Code-Behind
Only `CustomerList.razor` has a code-behind (`.razor.cs`) file. All other 33 pages with `@code` blocks embed logic directly in the `.razor` file. For pages with non-trivial logic, the mixing of markup and C# makes unit testing and maintenance harder.

**Fix (longer-term):** Migrate non-trivial pages to partial-class code-behind files (`.razor.cs`), following the pattern established in `CustomerList.razor.cs`.

---

### 2.5 Services Missing `ILogger` (`Services/CustomerService.cs`, `Services/PackingListService.cs`)
`CustomerService` and `PackingListService` have no `ILogger` injection, so errors and diagnostics cannot be traced via Serilog.

**Fix:** Add `ILogger<T>` injection and replace any silent failures with `_logger.LogError(...)` calls.

---

## Priority 3 — Debug / Development Artifacts to Remove

### 3.1 Debug `Console.WriteLine` Calls in Production Services
| File | Line | Issue |
|---|---|---|
| `Services/CustomUserClaimsPrincipalFactory.cs` | 25 | `Console.WriteLine($"Adding role claim: {role}"); // 🔍 For debugging` |
| `Services/ExportService.cs` | 446 | `Console.WriteLine("Error sending email: " + ex.Message);` — also swallows the exception |

**Fix:** Replace with `_logger.LogDebug(...)` / `_logger.LogError(...)` and rethrow or handle the exception properly in `ExportService`.

---

### 3.2 Commented-Out Usage Example in `Models/Customer.cs` (lines 181–194)
A block comment at the bottom of the file shows how to call `ToDescription()` and `GetDescription()`, including `Console.WriteLine` examples:
```csharp
// Usage
/*
Customer.CustomerStatus status = Customer.CustomerStatus.A;
...
Console.WriteLine(description); // Outputs: Active
*/
```
This is documentation scaffolding, not production code.

**Fix:** Delete the `// Usage` comment block.

---

### 3.3 Debug / Development Utility Pages That Should Be Removed or Restricted
| File | Route | Issue |
|---|---|---|
| `Pages/DebugUserIdentity.razor` | `/admin/debug-identity` | Exposes claim and identity info; useful during dev but should not be in production |
| `Pages/Counter.razor` | `/counter` | Blazor default template demo page; has no application purpose |
| `Pages/IdoPropsBuilder.razor` | (admin route) | Developer utility; verify whether it should remain in production |

**Fix:** Delete `Counter.razor`. Evaluate `DebugUserIdentity.razor` and `IdoPropsBuilder.razor`; if kept, restrict to `SuperUser` or `Administrator` role only and remove nav links to them in production.

---

### 3.4 Leftover Duplicate / Backup Pages
| File | Issue |
|---|---|
| `Pages/OpenOrdersOriginal.razor` | Named "Original," suggesting it is a pre-migration backup that was never deleted |
| `Pages/MonthlyItemSalesWithQuantity - Copy.razor` | File name includes " - Copy" — clearly a copy made for reference |

**Fix:** Confirm these pages are not linked from the nav menu or used anywhere, then delete them. Git history preserves the originals if they are ever needed again.

---

### 3.5 "Counter" Still in `NavMenu.razor` (line 23)
The default Blazor template `Counter` page is exposed in the navigation menu:
```html
<NavLink class="nav-link" href="counter">
    Counter
</NavLink>
```

**Fix:** Remove this nav entry (and delete `Counter.razor`, per item 3.3).

---

## Priority 4 — Code Hygiene

### 4.1 Extraneous / Debug `using` Directives in `Services/SalesService.cs`
| Import | Reason to Remove |
|---|---|
| `using Dumpify;` (line 16) | Third-party debug-dump library; has no place in production |
| `using Org.BouncyCastle.Tls.Crypto;` (line 11) | Cryptographic library unrelated to sales data |
| `using System.Reflection.Emit;` (line 4) | Low-level IL emit; not needed in this service |

**Fix:** Remove all three. Run `dotnet build` to confirm they are truly unused.

---

### 4.2 `using Dumpify;` in `Services/PcfService.cs` (line 4)
Same debug library as above.

**Fix:** Remove the `using Dumpify;` statement.

---

### 4.3 Inconsistent Property Naming in `Models/Customer.cs`
Three properties use underscored names (`Cust_Num`, `Cust_Name`, `Corp_Cust`) while all others use PascalCase (`BillToCity`, `CreditHold`, etc.). The underscored names appear to be carried over from legacy SQL column names.

**Fix:** Rename to `CustNum`, `CustName`, `CorpCust` and update all references. The IDO field name mapping is handled via `[CsiField]` so renaming C# properties is safe.

---

### 4.4 Excessive Blank Lines and Inconsistent Spacing in `Models/Customer.cs`
Multiple properties are separated by two or three blank lines (e.g., lines 100–106, 107–110, 113–118). The C# convention is a single blank line between members.

**Fix:** Normalize to single blank lines between property declarations.

---

### 4.5 `HoldStatusYN` Should Use an Expression Body (`Models/Customer.cs`)
```csharp
// Current (lines 143–156)
public string HoldStatusYN
{
    get
    {
        if (CreditHold == 1) { return "Yes"; }
        else { return "No"; }
    }
}
```
**Fix:**
```csharp
public string HoldStatusYN => CreditHold == 1 ? "Yes" : "No";
```

---

### 4.6 Redundant `GetDescription` Reflection Method in `CustomerStatusExtensions` (`Models/Customer.cs`)
`ToDescription()` (lines 164–172) uses a switch expression and is the method actually called in `StatusDescription`. `GetDescription()` (lines 173–178) does the same thing via `DescriptionAttribute` reflection but is never called anywhere in the solution.

**Fix:** Remove `GetDescription()` unless a specific caller is found.

---

### 4.7 Commented-Out Code Blocks in `ReportRunner.cs` (lines 155–157)
```csharp
// private static readonly DateTime SqlMin = SqlDateTime.MinValue.Value;
// private static readonly DateTime SqlMax = SqlDateTime.MaxValue.Value;
```
These are immediately followed by un-commented declarations of the same fields (lines 160–161). The commented-out lines serve no purpose.

**Fix:** Delete the commented-out lines.

---

### 4.8 Convenience Test Constructor Creates Brittle Null Contract (`Services/SalesService.cs`)
The second constructor `SalesService(string connectionString)` (lines 59–64) was added for "Tests/console" use and sets all injected services to `null`. Any method that reaches a null dependency at runtime will throw `NullReferenceException` without a clear error message.

**Fix:** Remove this constructor and configure test infrastructure properly using `WebApplicationFactory` or a mock-based `SalesService` constructor that accepts all required non-nullable dependencies.

---

## Priority 5 — Minor / Polish

### 5.1 `@using` Directives Repeated Across Many Razor Pages
Many pages declare `@using Syncfusion.Blazor.Grids`, `@using Microsoft.AspNetCore.Authorization`, etc. at the top. These are already in `_Imports.razor`.

**Fix:** Audit `_Imports.razor` to ensure common namespaces are imported globally and remove per-page `@using` statements that are already covered.

---

### 5.2 Missing XML Doc Comments on Public Service Interfaces
`IIdoService`, `ISalesService`, `ISalesDataService`, and other public interfaces have few or no XML doc comments, making IntelliSense less useful for maintainers.

**Fix:** Add `/// <summary>` doc comments to at minimum the public interface members.

---

### 5.3 `throw new Exception(...)` in `AIService.cs` (line 142)
```csharp
throw new Exception($"OpenAI API error ({response.StatusCode}): {errorText}");
```
Throwing `System.Exception` directly is discouraged; a more specific type should be used.

**Fix:** Throw `HttpRequestException` or a custom `AiServiceException` to allow callers to distinguish error types.

---

## Summary by File

| File | Issues |
|---|---|
| `Services/SalesService.cs` | Direct `ICsiRestClient` injection, nullable dependency anti-pattern, duplicate `_idoService` assignment, debug `using` statements, god class (1812 lines) |
| `Services/IdoService.cs` | Two unimplemented interface stubs, large class (1736 lines) |
| `Services/ReportRunner.cs` | Two unimplemented private stubs, commented-out code |
| `Services/CustomUserClaimsPrincipalFactory.cs` | `Console.WriteLine` debug statement |
| `Services/ExportService.cs` | `Console.WriteLine` swallowing exception |
| `Services/CustomerService.cs` | No `ILogger` injection |
| `Services/PackingListService.cs` | No `ILogger` injection |
| `Services/PcfService.cs` | `using Dumpify;` debug import |
| `Models/Customer.cs` | Duplicate `[CsiField]` mappings, NRE-prone computed properties, inconsistent naming, leftover comment block, redundant extension method |
| `Pages/Counter.razor` | Template leftover, should be deleted |
| `Pages/DebugUserIdentity.razor` | Debug page, restrict or delete |
| `Pages/OpenOrdersOriginal.razor` | Legacy backup, should be deleted |
| `Pages/MonthlyItemSalesWithQuantity - Copy.razor` | Backup copy, should be deleted |
| `Shared/NavMenu.razor` | Counter nav link should be removed |
