using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using RepPortal.Data; // your factory namespace
using RepPortal.Models; // InvoiceRptDetail


public interface IInvoicedAccountsReport
{
    Task<List<InvoiceRptDetail>> GetAsync(
        string repCode,
        IReadOnlyList<string>? allowedRegions,
        string? customerId,
        DateTime beginLocal,          // inclusive, in your business TZ
        DateTime endLocalExclusive,   // exclusive, in your business TZ
        string? corpNum = null,
        string? custType = null,
        string? endUserType = null,
        string? coNum = null);
}
public sealed class InvoicedAccountsReport : IInvoicedAccountsReport

{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<InvoicedAccountsReport> _logger;

    public InvoicedAccountsReport(IDbConnectionFactory dbFactory, ILogger<InvoicedAccountsReport> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<List<InvoiceRptDetail>> GetAsync(
            string repCode,
            IReadOnlyList<string>? allowedRegions,
            string? customerId,
            DateTime beginLocal,
            DateTime endLocalExclusive,
            string? corpNum = null,
            string? custType = null,
            string? endUserType = null,
            string? coNum = null)
    {
        // Build the regions CSV exactly like your Blazor method did
        string? allowedRegionsCsv = (allowedRegions is { Count: > 0 })
            ? string.Join(",", allowedRegions)
            : null;

        using var conn = _dbFactory.CreateRepConnection(); // returns IDbConnection
        conn.Open(); // sync open is fine in a Hangfire job

        // If you prefer async open and you know it's SqlConnection:
        // if (conn is SqlConnection sc) await sc.OpenAsync();

        _logger?.LogInformation(
            "InvoicedAccounts: Rep={Rep} Cust={Cust} Range=[{Begin}->{End}) Regions={Regions}",
            repCode, customerId ?? "(ALL)",
            beginLocal, endLocalExclusive, allowedRegionsCsv ?? "(none)");

        var results = await conn.QueryAsync<InvoiceRptDetail>(@"
                EXEC RepPortal.dbo.sp_GetInvoices 
                     @BeginInvoiceDate, 
                     @EndInvoiceDate, 
                     @RepCode, 
                     @CustNum, 
                     @CorpNum, 
                     @CustType, 
                     @EndUserType,
                     @AllowedRegions;",
            new
            {
                BeginInvoiceDate = beginLocal,
                EndInvoiceDate = endLocalExclusive,
                RepCode = repCode,           // security
                CustNum = customerId,        // nullable
                CorpNum = corpNum,           // nullable
                CustType = custType,          // nullable
                EndUserType = endUserType,       // nullable
                AllowedRegions = allowedRegionsCsv  // nullable CSV
            });

        IEnumerable<InvoiceRptDetail> filtered = results;

        // Preserve your optional CoNum post-filter
        if (!string.IsNullOrWhiteSpace(coNum))
        {
            var match = coNum.Trim().ToUpperInvariant();
            filtered = filtered.Where(r =>
                !string.IsNullOrWhiteSpace(r.CoNum) &&
                r.CoNum.Trim().ToUpperInvariant() == match);
        }

        return filtered.ToList();
    }



    private static string BuildQuery(IEnumerable<string>? allowedRegions)
    {
        var regionFilter = allowedRegions is null ? "" : "AND ia.Region IN @AllowedRegions";
        return $@"
SELECT ia.InvoiceNo, ia.InvoiceDate, ia.Cust_Num, ia.Cust_Name, ia.Amount
FROM dbo.Invoices ia
WHERE ia.RepCode = @RepCode
  AND (@CustomerId IS NULL OR ia.Cust_Num = @CustomerId)
  AND ia.InvoiceDate >= @StartUtc AND ia.InvoiceDate < @EndUtc
  {regionFilter}
ORDER BY ia.InvoiceDate DESC";
    }

    public static List<Dictionary<string, object>> Materialize(IEnumerable<dynamic> rows)
    {
        var list = new List<Dictionary<string, object>>();
        foreach (var r in rows)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in (IDictionary<string, object>)r)
                dict[kv.Key] = kv.Value ?? DBNull.Value;
            list.Add(dict);
        }
        return list;
    }
}
