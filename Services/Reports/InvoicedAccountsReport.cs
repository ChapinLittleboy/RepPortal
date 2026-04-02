using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepPortal.Data;
using RepPortal.Models;
using RepPortal.Services;


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
    private readonly IIdoService _idoService;
    private readonly CsiOptions _csiOptions;

    public InvoicedAccountsReport(
        IDbConnectionFactory dbFactory,
        ILogger<InvoicedAccountsReport> logger,
        IIdoService idoService,
        IOptions<CsiOptions> csiOptions)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _idoService = idoService;
        _csiOptions = csiOptions.Value;
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
        _logger?.LogInformation(
            "InvoicedAccounts: Rep={Rep} Cust={Cust} Range=[{Begin}->{End}) Regions={Regions} UseApi={UseApi}",
            repCode, customerId ?? "(ALL)",
            beginLocal, endLocalExclusive,
            allowedRegions is { Count: > 0 } ? string.Join(",", allowedRegions) : "(none)",
            _csiOptions.UseApi);

        if (_csiOptions.UseApi)
        {
            var parameters = new SalesService.InvoiceRptParameters
            {
                RepCode          = repCode,
                BeginInvoiceDate = beginLocal,
                EndInvoiceDate   = endLocalExclusive,
                CustNum          = customerId,
                CorpNum          = corpNum,
                CustType         = custType,
                EndUserType      = endUserType,
                CoNum            = coNum,
                AllowedRegions   = allowedRegions?.ToList() ?? new List<string>(),
            };
            return await _idoService.GetInvoiceRptDataAsync(parameters, repCode);
        }

        // SQL path
        string? allowedRegionsCsv = allowedRegions is { Count: > 0 }
            ? string.Join(",", allowedRegions)
            : null;

        using var conn = _dbFactory.CreateRepConnection();
        conn.Open();

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
                EndInvoiceDate   = endLocalExclusive,
                RepCode          = repCode,
                CustNum          = customerId,
                CorpNum          = corpNum,
                CustType         = custType,
                EndUserType      = endUserType,
                AllowedRegions   = allowedRegionsCsv
            });

        IEnumerable<InvoiceRptDetail> filtered = results;

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
