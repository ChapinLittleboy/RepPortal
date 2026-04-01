namespace RepPortal.Services;

/// <summary>
/// API path for IDO-backed report queries. Implementations call the CSI REST API
/// instead of the BAT/KENT SQL Server databases.
/// </summary>
public interface IIdoService
{
    /// <summary>
    /// Fetches item-level invoice data from the Chap_InvoiceLines IDO and pivots it into
    /// the same revenue-only, two-fiscal-year + monthly column format produced by the SQL path.
    /// Matches the column layout of <c>BuildItemsMonthlyQuery</c>:
    /// fixed dimension columns, FY{prior}, FY{current}, and one column per current-FY month.
    /// </summary>
    Task<List<Dictionary<string, object>>> GetItemSalesReportDataAsync(
        string repCode,
        List<string>? allowedRegions);
}
