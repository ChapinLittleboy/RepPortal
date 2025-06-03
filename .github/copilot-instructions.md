# copilot-instructions.md

## Coding Style and Practices

- **Clarity first:** Code should be straightforward, maintainable, and easy for future developers to follow.
- **Pragmatic solutions:** Prefer tried-and-true, enterprise-appropriate patterns over bleeding-edge or academic solutions.
- **Keep it simple:** Avoid unnecessary abstractions, indirection, or “clever” tricks that make the code harder to maintain.
- **Consistency:** Use the project’s existing conventions for naming, structure, and formatting.

## Stack and Technologies

- **Backend:**  
  - SQL Server (2017 and newer).  
  - T-SQL for queries, stored procedures, and views.  
  - Dapper for data access in .NET.
  - Sometimes use inline SQL in C# services, sometimes stored procedures (when flexibility for DBAs is needed).
- **Application Layer:**  
  - Blazor Server (main UI framework).
  - C# for business logic, view models, services, and integration layers.
  - VB.NET is sometimes used for scripting in legacy ERP integrations.
- **Front-end:**  
  - Razor Components for Blazor.
  - Syncfusion components for data grids, dropdowns, and UI controls.
  - Code should be compatible with .NET 7+ and Syncfusion Blazor.

## Patterns and Preferences

- **SQL**:  
  - All queries should be written in T-SQL unless otherwise specified.
  - Write full working queries, including DECLARE and table setup if needed for clarity/testing.
  - When returning dynamic columns (e.g., for grids), use dynamic SQL only when necessary, and clearly document any assumptions.
  - Table variables preferred for intermediate result sets in scripts.
  - Use CTEs for complex data shaping.
  - Avoid SQL features that require SQL 2019+ unless absolutely necessary.

- **C#**:  
  - Favor explicitness: e.g., use `var` only when the type is obvious.
  - Show the full method/class, not just code fragments.
  - LINQ queries should be easy to follow and not overly chained.
  - Dapper usage: show full command, including parameter usage.
  - If code involves impersonation or user context, clearly indicate how context is passed (e.g., via service injection).
  - Include necessary usings and class wrappers for copy-paste execution.
  - Use summary comments for non-obvious logic or public methods.

- **Blazor/Syncfusion**:  
  - Prefer code-behind or partial class for non-trivial component logic.
  - Show the .razor markup *and* related .cs logic for complex examples.
  - Always include using/imports required at the top of files.
  - UI suggestions should match Syncfusion Blazor component patterns.

- **General**:  
  - Use comments to document assumptions, input expectations, and potential side effects.
  - If a task involves role-based access, admin controls, or impersonation, prefer using existing services/context over inventing new ones.

## What to Avoid

- Overly abstract or “clever” code that would be difficult for a mid-level developer to debug or maintain.
- Use of external packages or services that are not already part of the stack, unless explicitly requested.
- Cutting corners on security, especially around authentication, authorization, and database access.
- Partial code snippets that require guessing at context; always show the whole method/class/component.

## Example Code Request

If a request is for a SQL query:
- Write in T-SQL, using variables and table setup as needed for standalone testing.
- Comment any assumptions (e.g., expected schema).

If a request is for a C# service method:
- Provide the entire method, class, and necessary usings.
- Show Dapper usage with parameters and error handling.

If a request is for a Blazor UI enhancement:
- Show the .razor markup, the backing .cs code, and any supporting classes/models.
- Include comments for any tricky parts.

---

