using System.Data;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Tls.Crypto;
using RepPortal.Data;
using RepPortal.Models;
using static RepPortal.Pages.MonthlyItemSalesPivot;
using RegionInfo = RepPortal.Models.RegionInfo;
using Dumpify;

namespace RepPortal.Services;

public class SalesService : ISalesService
{
    private readonly string _connectionString;
    private readonly AuthenticationStateProvider? _authenticationStateProvider;
    private readonly IRepCodeContext? _repCodeContext;
    private readonly IDbConnectionFactory? _dbConnectionFactory;
    private readonly ILogger<SalesService>? _logger;
    private readonly ISalesDataService? _core;
    private readonly ICsiRestClient? _csiRestClient;
    private readonly CsiOptions _csiOptions;

    // Primary DI ctor
    public SalesService(
        IConfiguration configuration,
        AuthenticationStateProvider authenticationStateProvider,
        IRepCodeContext repCodeContext,
        IDbConnectionFactory dbConnectionFactory,
        ILogger<SalesService> logger,
        ISalesDataService core,
        ICsiRestClient csiRestClient,
        IOptions<CsiOptions> csiOptions)
    {
        _connectionString = configuration.GetConnectionString("BatAppConnection")
                            ?? throw new InvalidOperationException("Missing BatAppConnection connection string.");
        _authenticationStateProvider = authenticationStateProvider;
        _repCodeContext = repCodeContext;
        _dbConnectionFactory = dbConnectionFactory;
        _logger = logger;
        _core = core;
        _csiRestClient = csiRestClient;
        _csiOptions = csiOptions.Value;
    }

    // Convenience ctor (tests/console). Only methods that use the raw connection string will work.

    public SalesService(string connectionString)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? throw new ArgumentException("connectionString is required.", nameof(connectionString))
            : connectionString;
    }

    public async Task<string?> GetRepCodeByRegistrationCodeAsync(string registrationCode)
    {
        if (string.IsNullOrWhiteSpace(registrationCode))
            return null;

        const string sql = @"
            SELECT TOP 1 RepCode
            FROM AgencyRegistrationCodes
            WHERE RegistrationCode = @RegistrationCode
              AND IsActive = 1
              AND (ExpirationDate IS NULL OR ExpirationDate > GETUTCDATE())
            ORDER BY ExpirationDate DESC;";

        if (_dbConnectionFactory == null)
            throw new InvalidOperationException("IDbConnectionFactory is required for this operation.");

        using var connection = _dbConnectionFactory.CreateRepConnection();
        return await connection.QueryFirstOrDefaultAsync<string>(
            sql, new { RegistrationCode = registrationCode.Trim() });
    }

    public async Task<string?> GetCustNumFromCoNum(string coNum)
    {
        if (string.IsNullOrWhiteSpace(coNum))
            return null;

        const string sql = @"
            SELECT TOP 1 Cust_Num
            FROM CO_mst
            WHERE co_num = @CoNum;";

        if (_dbConnectionFactory == null)
            throw new InvalidOperationException("IDbConnectionFactory is required for this operation.");

        using var connection = _dbConnectionFactory.CreateBatConnection();
        return await connection.QueryFirstOrDefaultAsync<string>(sql, new { CoNum = coNum.Trim() });
    }

    public async Task<string?> GetRepIDAsync()
    {
        EnsureAuth();
        var authState = await _authenticationStateProvider!.GetAuthenticationStateAsync();
        var user = authState.User;
        return user?.FindFirst("RepID")?.Value;
    }

    public string? GetCurrentRepCode()
    {
        EnsureRepContext();
        return _repCodeContext!.CurrentRepCode;
    }

    public async Task<List<Dictionary<string, object>>> GetSalesReportDataUsingInvRep()
    {
        EnsureRepContext();

        if (_csiOptions.UseApi)
            return await GetSalesReportDataUsingInvRepApiAsync();

        EnsureAuth();

        var authState = await _authenticationStateProvider!.GetAuthenticationStateAsync();
        var user = authState.User;
        var repCode = _repCodeContext!.CurrentRepCode;

        IEnumerable<string>? allowedRegions = null;
        if (repCode == "LAWxxx")
        {
            allowedRegions = user.Claims
                .Where(c => c.Type == "Region")
                .Select(c => c.Value)
                .Distinct()
                .ToList();
        }
        else if (repCode == "LAW")
        {
            allowedRegions = _repCodeContext.CurrentRegions;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var (query, fy) = BuildSalesPivotQuery(allowedRegions);

        // Use the rep on the invoice, not on the customer record
        query = query.Replace("cu.slsman", "ih.slsman");

        var parameters = new { RepCode = repCode, AllowedRegions = allowedRegions };
        var rows = await connection.QueryAsync(query, parameters, commandType: CommandType.Text);

        _logger?.LogInformation("GetSalesReportDataUsingInvRep FY used: {FY}", fy);

        return MaterializeToDictionaries(rows);
    }

    public async Task<List<Dictionary<string, object>>> GetSalesReportData()
    {
        EnsureRepContext();

        if (_csiOptions.UseApi)
            return await GetSalesReportDataApiAsync();

        EnsureAuth();

        var authState = await _authenticationStateProvider!.GetAuthenticationStateAsync();
        var user = authState.User;
        var repCode = _repCodeContext!.CurrentRepCode;

        IEnumerable<string>? allowedRegions = null;
        if (repCode == "LAWxxx")
        {
            allowedRegions = user.Claims
                .Where(c => c.Type == "Region")
                .Select(c => c.Value)
                .Distinct()
                .ToList();
        }
        else if (repCode == "LAW")
        {
            allowedRegions = _repCodeContext.CurrentRegions;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var (query, fy) = BuildSalesPivotQuery(allowedRegions);
        var parameters = new { RepCode = repCode, AllowedRegions = allowedRegions };
        var rows = await connection.QueryAsync(query, parameters, commandType: CommandType.Text);

        _logger?.LogInformation("GetSalesReportData FY used: {FY}", fy);

        return MaterializeToDictionaries(rows);
    }

    public async Task<List<Dictionary<string, object>>> GetItemSalesReportData()
    {
        EnsureAuth();
        EnsureRepContext();

        var authState = await _authenticationStateProvider!.GetAuthenticationStateAsync();
        var user = authState.User;
        var repCode = _repCodeContext!.CurrentRepCode;

        IEnumerable<string>? allowedRegions = null;
        if (repCode == "LAWxxx")
        {
            allowedRegions = user.Claims
                .Where(c => c.Type == "Region")
                .Select(c => c.Value)
                .Distinct()
                .ToList();
        }
        else if (repCode == "LAW")
        {
            allowedRegions = _repCodeContext.CurrentRegions;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var query = BuildItemsMonthlyQuery(allowedRegions);
        _logger?.LogInformation("GetItemSalesReportData SQL:\n{Sql}", query);

        var rows = await connection.QueryAsync(query, new { RepCode = repCode }, commandType: CommandType.Text);
        return MaterializeToDictionaries(rows);
    }

    public async Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQty()
    {
        EnsureRepContext();

        if (_csiOptions.UseApi)
            return await GetItemSalesReportDataWithQtyApiAsync();

        var repCode = _repCodeContext!.CurrentRepCode;
        var allowedRegions = _repCodeContext.CurrentRegions;

        var today = DateTime.Today;
        int fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;

        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyCurrentEnd = new DateTime(fiscalYear, 8, 31);

        var fyMinus1Start = fyCurrentStart.AddYears(-1);
        var fyMinus1End = fyCurrentEnd.AddYears(-1);
        var fyMinus2Start = fyCurrentStart.AddYears(-2);
        var fyMinus2End = fyCurrentEnd.AddYears(-2);
        var fyMinus3Start = fyCurrentStart.AddYears(-3);
        var fyMinus3End = fyCurrentEnd.AddYears(-3);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var currentFYMonths = Enumerable.Range(0, currentFiscalMonth)
            .Select(i => fyCurrentStart.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        var monthColumns = new StringBuilder();
        foreach (var monthName in currentFYMonths)
        {
            var safe = monthName.Replace("'", "''");
            monthColumns.AppendLine($@"
        SUM(CASE WHEN Period = '{safe}' THEN RevAmount   ELSE 0 END) AS [{safe}_Rev],
        SUM(CASE WHEN Period = '{safe}' THEN QtyInvoiced ELSE 0 END) AS [{safe}_Qty],");
        }
        if (monthColumns.Length > 0)
        {
            var i = monthColumns.ToString().LastIndexOf(',');
            if (i >= 0) monthColumns.Remove(i, 1);
        }

        var regionFilter = (allowedRegions != null && allowedRegions.Any())
            ? " AND cu.Uf_SalesRegion IN @AllowedRegions"
            : string.Empty;

        string BaseSelect(string db) => $@"
        SELECT
            ih.cust_num AS Customer,
            ca0.Name     AS [Customer Name],
            ih.cust_seq  AS [Ship To Num],
            ca.City      AS [Ship To City],
            ca.State     AS [Ship To State],
            cu.slsman,
            ca0.name     AS SalespersonName,
            ca0.state    AS [Bill To State],
            cu.Uf_SalesRegion,
            rn.RegionName,
            ii.item      AS Item,
            im.Description AS ItemDescription,
            FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR(4)) AS Period,
            CASE
                WHEN ih.inv_date BETWEEN '{fyMinus3Start:yyyy-MM-dd}' AND '{fyMinus3End:yyyy-MM-dd}' THEN 'FY{fiscalYear - 3}'
                WHEN ih.inv_date BETWEEN '{fyMinus2Start:yyyy-MM-dd}' AND '{fyMinus2End:yyyy-MM-dd}' THEN 'FY{fiscalYear - 2}'
                WHEN ih.inv_date BETWEEN '{fyMinus1Start:yyyy-MM-dd}' AND '{fyMinus1End:yyyy-MM-dd}' THEN 'FY{fiscalYear - 1}'
                WHEN ih.inv_date BETWEEN '{fyCurrentStart:yyyy-MM-dd}' AND '{fyCurrentEnd:yyyy-MM-dd}' THEN 'FY{fiscalYear}'
            END AS FiscalYear,
            (ii.qty_invoiced * (ii.price * ((100 - ISNULL(ih.disc, 0.0)) / 100))) AS RevAmount,
            ii.qty_invoiced AS QtyInvoiced
        FROM {db}.dbo.inv_item_mst ii WITH (NOLOCK)
        JOIN {db}.dbo.inv_hdr_mst  ih WITH (NOLOCK) ON ii.inv_num = ih.inv_num AND ii.inv_seq = ih.inv_seq
        JOIN Bat_App.dbo.customer_mst   cu WITH (NOLOCK) ON ih.cust_num = cu.cust_num AND ih.cust_seq = cu.cust_seq
        JOIN Bat_App.dbo.custaddr_mst   ca0 WITH (NOLOCK) ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
        JOIN Bat_App.dbo.custaddr_mst   ca  WITH (NOLOCK) ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
        LEFT JOIN Bat_App.dbo.Chap_RegionNames rn WITH (NOLOCK) ON rn.Region = cu.Uf_SalesRegion
        LEFT JOIN Bat_App.dbo.Item_mst im WITH (NOLOCK) ON ii.item = im.item
        WHERE ih.inv_date BETWEEN '{fyMinus3Start:yyyy-MM-dd}' AND '{fyCurrentEnd:yyyy-MM-dd}'
          AND cu.slsman = @RepCode{regionFilter}";

        var sql = $@"
    WITH InvoiceData AS (
        {BaseSelect("Bat_App")}
        UNION ALL
        {BaseSelect("Kent_App")}
    ),
    AggregatedData AS (
        SELECT
            Customer, [Customer Name], [Ship To Num], [Ship To City], [Ship To State],
            slsman, SalespersonName, [Bill To State], Uf_SalesRegion, RegionName,
            Item, ItemDescription, Period, FiscalYear,
            SUM(RevAmount)   AS RevAmount,
            SUM(QtyInvoiced) AS QtyInvoiced
        FROM InvoiceData
        GROUP BY
            Customer, [Customer Name], [Ship To Num], [Ship To City], [Ship To State],
            slsman, SalespersonName, [Bill To State], Uf_SalesRegion, RegionName,
            Item, ItemDescription, Period, FiscalYear
    )
    SELECT
        Customer, [Customer Name], [Ship To Num], [Ship To City], [Ship To State],
        slsman, SalespersonName, [Bill To State], Uf_SalesRegion, RegionName,
        Item, ItemDescription,
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear - 3}' THEN RevAmount   ELSE 0 END) AS [FY{fiscalYear - 3}_Rev],
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear - 3}' THEN QtyInvoiced ELSE 0 END) AS [FY{fiscalYear - 3}_Qty],
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear - 2}' THEN RevAmount   ELSE 0 END) AS [FY{fiscalYear - 2}_Rev],
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear - 2}' THEN QtyInvoiced ELSE 0 END) AS [FY{fiscalYear - 2}_Qty],
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear - 1}' THEN RevAmount   ELSE 0 END) AS [FY{fiscalYear - 1}_Rev],
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear - 1}' THEN QtyInvoiced ELSE 0 END) AS [FY{fiscalYear - 1}_Qty],
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear}'     THEN RevAmount   ELSE 0 END) AS [FY{fiscalYear}_Rev],
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear}'     THEN QtyInvoiced ELSE 0 END) AS [FY{fiscalYear}_Qty],
        {monthColumns}
    FROM AggregatedData
    GROUP BY
        Customer, [Customer Name], [Ship To Num], [Ship To City], [Ship To State],
        slsman, SalespersonName, [Bill To State], Uf_SalesRegion, RegionName,
        Item, ItemDescription
    OPTION (RECOMPILE);";

        using var connection = new SqlConnection(_connectionString);
        var param = new DynamicParameters();
        param.Add("@RepCode", repCode);
        if (allowedRegions != null && allowedRegions.Any())
            param.Add("@AllowedRegions", allowedRegions);


        _logger?.LogInformation("GetItemSalesReportDataWithQty SQL:\n{Sql}", sql);

        _logger?.LogInformation("GetItemSalesReportDataWithQty SQL:\n{Sql}", sql);

        // Build a readable dictionary of parameters
        var paramDict = new Dictionary<string, object?>();

        foreach (var name in param.ParameterNames)
        {
            paramDict[name] = param.Get<dynamic>(name);
        }

        _logger?.LogInformation(
            "GetItemSalesReportDataWithQty PARAMETERS:\n{@Parameters}",
            paramDict);


        var rows = await connection.QueryAsync(sql, param, commandType: CommandType.Text);
        return MaterializeToDictionaries(rows);
    }

    /// <summary>
    /// Fetches invoice line data from the Chap_InvoiceLines IDO and pivots it into the
    /// same fiscal-year + monthly column format that the SQL version produces.
    /// Field names verified against Chap_InvoiceLines_Properties.csv.
    /// Customer names come from a secondary SLCustomers call (CustSeq=0);
    /// item descriptions from a secondary SLItems call.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQtyApiAsync()
    {
        EnsureRepContext();

        if (_csiRestClient == null)
            throw new InvalidOperationException("CSI REST client is not available.");

        var repCode = _repCodeContext!.CurrentRepCode;
        var allowedRegions = _repCodeContext.CurrentRegions;

        var today = DateTime.Today;
        int fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;

        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyCurrentEnd   = new DateTime(fiscalYear,     8, 31);
        var fyMinus3Start  = new DateTime(fiscalYear - 4, 9, 1);
        var fyMinus3End    = new DateTime(fiscalYear - 3, 8, 31);
        var fyMinus2Start  = new DateTime(fiscalYear - 3, 9, 1);
        var fyMinus2End    = new DateTime(fiscalYear - 2, 8, 31);
        var fyMinus1Start  = new DateTime(fiscalYear - 2, 9, 1);
        var fyMinus1End    = new DateTime(fiscalYear - 1, 8, 31);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var currentFYMonths = Enumerable.Range(0, currentFiscalMonth)
            .Select(i => fyCurrentStart.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        // ── 1. Chap_InvoiceLines IDO call ──
        // ExtPrice on this IDO = qty_invoiced * price (no discount).
        // We request price + disc separately to compute net revenue correctly.
        // Period is pre-computed by the IDO as "Sep2024" etc.
        var filters = new List<string>
        {
            Eq("Slsman", repCode),
            $"InvDate >= '{fyMinus3Start:yyyyMMdd}'",
            $"InvDate <= '{fyCurrentEnd:yyyyMMdd}'"
        };

        if (allowedRegions is { Count: > 0 })
            filters.Add(In("SalesRegion", allowedRegions));

        var props = string.Join(",", new[]
        {
            "InvDate", "CustNum", "CustSeq",
            "ShipToCity", "ShipToState", "BillToState",
            "Slsman", "SalesRegion", "RegionName",
            "item", "qty_invoiced", "price", "disc", "Period"
        });

        var query = new Dictionary<string, string>
        {
            ["props"]    = props,
            ["filter"]   = string.Join(" AND ", filters),
            ["rowcap"]   = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        // Query both BAT and KENT sites — mirrors the SQL UNION ALL across Bat_App and Kent_App.
        var siteAuths = new List<(string Site, string? AuthOverride)> { ("BAT", null) };
        if (!string.IsNullOrWhiteSpace(_csiOptions.KentAuthorization))
            siteAuths.Add(("KENT", _csiOptions.KentAuthorization));

        var lines = new List<InvLineRawRow>();

        foreach (var (site, authOverride) in siteAuths)
        {
            string siteJson = authOverride != null
                ? await _csiRestClient.GetAsync("json/Chap_InvoiceLines/adv", query, authOverride)
                : await _csiRestClient.GetAsync("json/Chap_InvoiceLines/adv", query);

            var siteResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(siteJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            if (siteResponse.MessageCode == 0)
            {
                lines.AddRange(siteResponse.Items
                    .Select(row => MapRow<InvLineRawRow>(row))
                    .Where(l => l.InvDate.HasValue));
            }
            else if (authOverride != null)
            {
                // Chap_InvoiceLines is a custom IDO that may not be deployed on KENT.
                // Fall back to standard SLInvHdrs + SLInvItemAlls which exist on all instances.
                _logger?.LogInformation(
                    "Chap_InvoiceLines not available on {Site} ({Msg}); falling back to SLInvHdrs + SLInvItemAlls",
                    site, siteResponse.Message);
                lines.AddRange(await FetchKentLinesViaStandardIdosAsync(
                    repCode, fyMinus3Start, fyCurrentEnd, authOverride));
            }
            else
            {
                _logger?.LogWarning("Chap_InvoiceLines failed for {Site}: {Msg}", site, siteResponse.Message);
            }
        }

        _logger?.LogInformation(
            "GetItemSalesReportDataWithQtyApiAsync: {Count} raw lines fetched for rep {RepCode} (BAT + KENT)",
            lines.Count, repCode);

        // ── 2. Customer names — SLCustomers, CustSeq=0 (billing/corporate address) ──
        var uniqueCustNums = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.CustNum))
            .Select(l => l.CustNum!)
            .Distinct()
            .ToList();

        var custNameLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Populated alongside custNameLookup; used to backfill SalesRegion on KENT fallback lines.
        var regionFromCustLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (uniqueCustNums.Count > 0)
        {
            var custQuery = new Dictionary<string, string>
            {
                ["props"]    = "CustNum,CustSeq,Name,Uf_SalesRegion",
                ["filter"]   = $"CustSeq = 0 AND {In("CustNum", uniqueCustNums)}",
                ["rowcap"]   = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var custJson = await _csiRestClient.GetAsync("json/SLCustomers/adv", custQuery);
            var custResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(custJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            if (custResponse.MessageCode == 0)
            {
                foreach (var custRow in custResponse.Items.Select(r => MapRow<CustNameInfo>(r)))
                {
                    if (!string.IsNullOrWhiteSpace(custRow.CustNum))
                    {
                        custNameLookup[custRow.CustNum] = custRow.Name ?? "";
                        if (!string.IsNullOrWhiteSpace(custRow.UfSalesRegion))
                            regionFromCustLookup[custRow.CustNum] = custRow.UfSalesRegion;
                    }
                }
            }
            else
            {
                _logger?.LogWarning("SLCustomers lookup failed: {Msg}", custResponse.Message);
            }
        }

        // ── 3. Item descriptions — SLItems ──
        var uniqueItems = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Item))
            .Select(l => l.Item!)
            .Distinct()
            .ToList();

        var itemDescLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (uniqueItems.Count > 0)
        {
            var itemQuery = new Dictionary<string, string>
            {
                ["props"]    = "Item,Description",
                ["filter"]   = In("Item", uniqueItems),
                ["rowcap"]   = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var itemJson = await _csiRestClient.GetAsync("json/SLItems/adv", itemQuery);
            var itemResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(itemJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            if (itemResponse.MessageCode == 0)
            {
                foreach (var itemRow in itemResponse.Items.Select(r => MapRow<ItemDescInfo>(r)))
                {
                    if (!string.IsNullOrWhiteSpace(itemRow.Item))
                        itemDescLookup[itemRow.Item] = itemRow.Description ?? "";
                }
            }
            else
            {
                _logger?.LogWarning("SLItems lookup failed: {Msg}", itemResponse.Message);
            }
        }

        // ── 3b. Backfill SalesRegion on KENT fallback lines and apply allowedRegions filter ──
        // KENT lines fetched via SLInvHdrs have SalesRegion = "" because that field is not on SLInvHdrs.
        // Use the Uf_SalesRegion values retrieved in step 2 to fill it in.
        foreach (var line in lines.Where(l => string.IsNullOrEmpty(l.SalesRegion)
                                              && !string.IsNullOrWhiteSpace(l.CustNum)))
        {
            if (regionFromCustLookup.TryGetValue(line.CustNum!, out string? region))
                line.SalesRegion = region;
        }

        // Re-apply the allowedRegions filter now that KENT lines have SalesRegion populated.
        // BAT lines were already filtered at the IDO level; this catches any KENT lines that slipped through.
        if (allowedRegions is { Count: > 0 })
        {
            lines = lines
                .Where(l => !string.IsNullOrEmpty(l.SalesRegion) && allowedRegions.Contains(l.SalesRegion))
                .ToList();
        }

        // ── Helper: determine fiscal year label from invoice date ──
        string GetFyLabel(DateTime d)
        {
            if (d >= fyMinus3Start && d <= fyMinus3End) return $"FY{fiscalYear - 3}";
            if (d >= fyMinus2Start && d <= fyMinus2End) return $"FY{fiscalYear - 2}";
            if (d >= fyMinus1Start && d <= fyMinus1End) return $"FY{fiscalYear - 1}";
            if (d >= fyCurrentStart && d <= fyCurrentEnd) return $"FY{fiscalYear}";
            return string.Empty;
        }

        // ── 4. Group by customer + ship-to + item, then pivot ──
        var grouped = lines
            .GroupBy(l => (
                CustNum: l.CustNum ?? "",
                CustSeq: l.CustSeq,
                Item: l.Item ?? ""
            ))
            .ToList();

        var result = new List<Dictionary<string, object>>(grouped.Count);

        foreach (var group in grouped)
        {
            var first = group.First();
            custNameLookup.TryGetValue(first.CustNum ?? "", out string? custName);
            itemDescLookup.TryGetValue(first.Item ?? "", out string? itemDesc);

            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customer"]        = first.CustNum ?? "",
                ["Customer Name"]   = custName ?? "",
                ["Ship To Num"]     = first.CustSeq,
                ["Ship To City"]    = first.ShipToCity ?? "",
                ["Ship To State"]   = first.ShipToState ?? "",
                ["slsman"]          = first.Slsman ?? "",
                ["name"]            = custName ?? "",
                ["Bill To State"]   = first.BillToState ?? "",
                ["Uf_SalesRegion"]  = first.SalesRegion ?? "",
                ["RegionName"]      = first.RegionName ?? "",
                ["Item"]            = first.Item ?? "",
                ["ItemDescription"] = itemDesc ?? ""
            };

            // Fiscal year total columns (4 years)
            foreach (int offset in new[] { 3, 2, 1, 0 })
            {
                string fyLabel = $"FY{fiscalYear - offset}";
                var fyLines = group.Where(l => GetFyLabel(l.InvDate!.Value) == fyLabel).ToList();
                row[$"{fyLabel}_Rev"] = fyLines.Sum(l => l.NetRevenue);
                row[$"{fyLabel}_Qty"] = fyLines.Sum(l => l.QtyInvoiced);
            }

            // Monthly columns for current FY — use the Period field pre-computed by the IDO
            foreach (var monthKey in currentFYMonths)
            {
                var monthLines = group
                    .Where(l => string.Equals(l.Period, monthKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                row[$"{monthKey}_Rev"] = monthLines.Sum(l => l.NetRevenue);
                row[$"{monthKey}_Qty"] = monthLines.Sum(l => l.QtyInvoiced);
            }

            result.Add(row);
        }
 //       result.Dump();

        await LogReportUsageAsync(repCode, "GetItemSalesReportDataWithQtyApi");
        return result;
    }

    // Maps to Chap_InvoiceLines IDO — field names verified against Chap_InvoiceLines_Properties.csv.
    // ExtPrice on that IDO is qty*price with NO discount, so we compute NetRevenue manually.
    private class InvLineRawRow
    {
        [CsiField("InvDate")]       public DateTime? InvDate { get; set; }
        [CsiField("CustNum")]       public string? CustNum { get; set; }
        [CsiField("CustSeq")]       public int CustSeq { get; set; }
        [CsiField("ShipToCity")]    public string? ShipToCity { get; set; }
        [CsiField("ShipToState")]   public string? ShipToState { get; set; }
        [CsiField("BillToState")]   public string? BillToState { get; set; }
        [CsiField("Slsman")]        public string? Slsman { get; set; }
        [CsiField("SalesRegion")]   public string? SalesRegion { get; set; }  // maps to Uf_SalesRegion
        [CsiField("RegionName")]    public string? RegionName { get; set; }
        [CsiField("item")]          public string? Item { get; set; }
        [CsiField("qty_invoiced")]  public decimal QtyInvoiced { get; set; }
        [CsiField("price")]         public decimal Price { get; set; }
        [CsiField("disc")]          public decimal Disc { get; set; }         // header-level discount %
        [CsiField("Period")]        public string? Period { get; set; }        // pre-computed: e.g. "Sep2024"

        // Net revenue on invoice lines ignoring any header level discount.
        public decimal NetRevenueNoDiscount => QtyInvoiced * Price ;



        // Net revenue after discount: matches SQL formula qty * (price * (100-disc%) / 100)  We apply the header-level discount to the each line.

        public decimal NetRevenue => QtyInvoiced * Price * (100m - Disc) / 100m;
    }

    // Used for the secondary SLCustomers call to look up billing/corporate name (CustSeq = 0)
    // and Uf_SalesRegion for KENT fallback lines that need SalesRegion backfilled.
    private class CustNameInfo
    {
        [CsiField("CustNum")]        public string? CustNum { get; set; }
        [CsiField("CustSeq")]        public int CustSeq { get; set; }
        [CsiField("Name")]           public string? Name { get; set; }
        [CsiField("Uf_SalesRegion")] public string? UfSalesRegion { get; set; }
    }

    // Used for the secondary SLItems call to look up item descriptions.
    private class ItemDescInfo
    {
        [CsiField("Item")]        public string? Item { get; set; }
        [CsiField("Description")] public string? Description { get; set; }
    }

    // Used by FetchKentLinesViaStandardIdosAsync — standard SLInvHdrs is on all Syteline instances.
    private class KentInvHdrRaw
    {
        [CsiField("InvNum")]  public string? InvNum { get; set; }
        [CsiField("InvSeq")]  public int InvSeq { get; set; }
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int CustSeq { get; set; }
        [CsiField("InvDate")] public DateTime? InvDate { get; set; }
        [CsiField("State")]   public string? State { get; set; }   // ship-to state
        [CsiField("Disc")]    public decimal Disc { get; set; }    // header-level discount %
        [CsiField("Slsman")]  public string? Slsman { get; set; }
    }

    // Used by FetchKentLinesViaStandardIdosAsync — standard SLInvItemAlls is on all Syteline instances.
    private class KentInvItemRaw
    {
        [CsiField("InvNum")]      public string? InvNum { get; set; }
        [CsiField("InvSeq")]      public int InvSeq { get; set; }
        [CsiField("Item")]        public string? Item { get; set; }
        [CsiField("QtyInvoiced")] public decimal QtyInvoiced { get; set; }
        [CsiField("Price")]       public decimal Price { get; set; }
    }

    /// <summary>
    /// KENT fallback: Chap_InvoiceLines is a custom IDO that may not be deployed on KENT.
    /// This method replicates the same data shape using standard SLInvHdrs + SLInvItemAlls.
    /// ShipToCity, BillToState, SalesRegion, and RegionName are backfilled by the caller.
    /// </summary>
    private async Task<List<InvLineRawRow>> FetchKentLinesViaStandardIdosAsync(
        string repCode,
        DateTime dateFrom,
        DateTime dateTo,
        string kentAuth)
    {
        // ── SLInvHdrs (KENT) ──
        var hdrQuery = new Dictionary<string, string>
        {
            ["props"]    = "InvNum,InvSeq,CustNum,CustSeq,InvDate,State,Disc,Slsman",
            ["filter"]   = $"{Eq("Slsman", repCode)} AND InvDate >= '{dateFrom:yyyyMMdd}' AND InvDate <= '{dateTo:yyyyMMdd}'",
            ["rowcap"]   = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var hdrJson = await _csiRestClient!.GetAsync("json/SLInvHdrs/adv", hdrQuery, kentAuth);
        var hdrResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(hdrJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        if (hdrResponse.MessageCode != 0)
        {
            _logger?.LogWarning("SLInvHdrs (KENT fallback) failed: {Msg}", hdrResponse.Message);
            return new List<InvLineRawRow>();
        }

        var headers = hdrResponse.Items
            .Select(row => MapRow<KentInvHdrRaw>(row))
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum) && h.InvDate.HasValue)
            .ToList();

        if (headers.Count == 0)
            return new List<InvLineRawRow>();

        var hdrLookup = headers
            .GroupBy(h => (h.InvNum!, h.InvSeq))
            .ToDictionary(g => g.Key, g => g.First());

        var invNums = headers.Select(h => h.InvNum!).Distinct().ToList();

        // ── SLInvItemAlls (KENT) — batched ──
        const int batchSize = 30;
        var rawItems = new List<KentInvItemRaw>();

        foreach (var batch in invNums.Chunk(batchSize))
        {
            var itemQuery = new Dictionary<string, string>
            {
                ["props"]    = "InvNum,InvSeq,Item,QtyInvoiced,Price",
                ["filter"]   = In("InvNum", batch),
                ["rowcap"]   = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var itemJson = await _csiRestClient!.GetAsync("json/SLInvItemAlls/adv", itemQuery, kentAuth);
            var itemResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(itemJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            if (itemResponse.MessageCode == 0)
                rawItems.AddRange(itemResponse.Items.Select(r => MapRow<KentInvItemRaw>(r)));
            else
                _logger?.LogWarning("SLInvItemAlls (KENT fallback) batch failed: {Msg}", itemResponse.Message);
        }

        // ── Join headers + items into InvLineRawRow ──
        var result = new List<InvLineRawRow>();

        foreach (var item in rawItems)
        {
            if (string.IsNullOrWhiteSpace(item.InvNum) || string.IsNullOrWhiteSpace(item.Item))
                continue;

            var key = (item.InvNum!, item.InvSeq);
            if (!hdrLookup.TryGetValue(key, out KentInvHdrRaw? hdr) || !hdr.InvDate.HasValue)
                continue;

            result.Add(new InvLineRawRow
            {
                InvDate     = hdr.InvDate,
                CustNum     = hdr.CustNum,
                CustSeq     = hdr.CustSeq,
                ShipToCity  = "",               // not on SLInvHdrs
                ShipToState = hdr.State ?? "",
                BillToState = "",               // not on SLInvHdrs
                Slsman      = hdr.Slsman ?? repCode,
                SalesRegion = "",               // backfilled by caller via SLCustomers lookup
                RegionName  = "",               // backfilled by caller via SLCustomers lookup
                Item        = item.Item,
                QtyInvoiced = item.QtyInvoiced,
                Price       = item.Price,
                Disc        = hdr.Disc,
                Period      = hdr.InvDate.Value.ToString("MMM") + hdr.InvDate.Value.Year
            });
        }

        _logger?.LogInformation(
            "KENT fallback (SLInvHdrs + SLInvItemAlls): {Count} lines for rep {RepCode}",
            result.Count, repCode);

        return result;
    }

    public async Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQtyOLD()
    {
        EnsureAuth();
        EnsureRepContext();

        var authState = await _authenticationStateProvider!.GetAuthenticationStateAsync();
        var user = authState.User;
        var repCode = _repCodeContext!.CurrentRepCode;

        IEnumerable<string>? allowedRegions = null;
        if (repCode == "LAWxxx")
        {
            allowedRegions = user.Claims
                .Where(c => c.Type == "Region")
                .Select(c => c.Value)
                .Distinct()
                .ToList();
        }
        else if (repCode == "LAW")
        {
            allowedRegions = _repCodeContext.CurrentRegions;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var query = GetDynamicQueryForItemsMonthlyWithQty(allowedRegions);
        _logger?.LogInformation("GetItemSalesReportDataWithQtyOLD SQL:\n{Sql}", query);

        var rows = await connection.QueryAsync(query,
            new { RepCode = repCode, AllowedRegions = allowedRegions }, commandType: CommandType.Text);

        return MaterializeToDictionaries(rows);
    }

    public string GetDynamicQueryForItemsMonthlyWithQty(IEnumerable<string>? allowedRegions = null)
    {
        var today = DateTime.Today;
        int fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;

        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyCurrentEnd = new DateTime(fiscalYear, 8, 31);
        var fyPriorStart = fyCurrentStart.AddYears(-1);
        var fyPriorEnd = fyCurrentEnd.AddYears(-1);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var allMonths = Enumerable.Range(0, 24)
            .Select(i => fyPriorStart.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        var currentFYMonths = allMonths.Skip(12).Take(currentFiscalMonth).ToList();

        var monthColumns = new StringBuilder();
        foreach (var m in currentFYMonths)
        {
            var safe = m.Replace("'", "''");
            monthColumns.AppendLine($@"
        SUM(CASE WHEN Period = '{safe}' THEN RevAmount ELSE 0 END) AS [{safe}_Rev],
        SUM(CASE WHEN Period = '{safe}' THEN QtyInvoiced ELSE 0 END) AS [{safe}_Qty],");
        }
        if (monthColumns.Length > 0)
        {
            var i = monthColumns.ToString().LastIndexOf(',');
            if (i >= 0) monthColumns.Remove(i, 1);
        }

        var regionFilter = (allowedRegions != null && allowedRegions.Any())
            ? " AND cu.Uf_SalesRegion IN @AllowedRegions"
            : string.Empty;

        string BaseSelect(string db) => $@"
        SELECT
            ih.cust_num AS Customer,
            ca0.Name AS [Customer Name],
            ih.cust_seq AS [Ship To Num],
            ca.City AS [Ship To City],
            ca.State AS [Ship To State],
            cu.slsman,
            ca0.name AS SalespersonName,
            ca0.state AS [Bill To State],
            cu.Uf_SalesRegion,
            rn.RegionName,
            ii.item AS Item,
            im.Description AS ItemDescription,
            FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR) AS Period,
            CASE
                WHEN ih.inv_date BETWEEN '{fyPriorStart:yyyy-MM-dd}' AND '{fyPriorEnd:yyyy-MM-dd}' THEN 'FY{fiscalYear - 1}'
                WHEN ih.inv_date BETWEEN '{fyCurrentStart:yyyy-MM-dd}' AND '{fyCurrentEnd:yyyy-MM-dd}' THEN 'FY{fiscalYear}'
            END AS FiscalYear,
            ii.qty_invoiced * (ii.price * ((100 - ISNULL(ih.disc, 0.0)) / 100)) AS RevAmount,
            ii.qty_invoiced AS QtyInvoiced
        FROM {db}.dbo.inv_item_mst ii 
        JOIN {db}.dbo.inv_hdr_mst ih WITH (NOLOCK) ON ii.inv_num = ih.inv_num AND ii.inv_seq = ih.inv_seq
        JOIN Bat_App.dbo.customer_mst cu WITH (NOLOCK) ON ih.cust_num = cu.cust_num AND ih.cust_seq = cu.cust_seq
        JOIN Bat_App.dbo.custaddr_mst ca0 WITH (NOLOCK) ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
        JOIN Bat_App.dbo.custaddr_mst ca WITH (NOLOCK) ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
        LEFT JOIN Bat_App.dbo.Chap_RegionNames rn WITH (NOLOCK) ON rn.Region = cu.Uf_SalesRegion
        LEFT JOIN Bat_App.dbo.Item_mst im WITH (NOLOCK) ON ii.item = im.item
        WHERE ih.inv_date BETWEEN '{fyPriorStart:yyyy-MM-dd}' AND '{fyCurrentEnd:yyyy-MM-dd}'
          AND cu.slsman = @RepCode{regionFilter}";

        return $@"
    WITH InvoiceData AS (
        {BaseSelect("Bat_App")}
        UNION ALL
        {BaseSelect("Kent_App")}
    ),
    AggregatedData AS (
        SELECT
            Customer, [Customer Name], [Ship To Num], [Ship To City], [Ship To State],
            slsman, SalespersonName, [Bill To State], Uf_SalesRegion, RegionName,
            Item, ItemDescription, Period, FiscalYear,
            SUM(RevAmount) AS RevAmount,
            SUM(QtyInvoiced) AS QtyInvoiced
        FROM InvoiceData
        GROUP BY
            Customer, [Customer Name], [Ship To Num], [Ship To City], [Ship To State],
            slsman, SalespersonName, [Bill To State], Uf_SalesRegion, RegionName,
            Item, ItemDescription, Period, FiscalYear
    )
    SELECT
        Customer, [Customer Name], [Ship To Num], [Ship To City], [Ship To State],
        slsman, SalespersonName, [Bill To State], Uf_SalesRegion, RegionName,
        Item, ItemDescription,
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear - 1}' THEN RevAmount ELSE 0 END) AS [FY{fiscalYear - 1}_Rev],
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear - 1}' THEN QtyInvoiced ELSE 0 END) AS [FY{fiscalYear - 1}_Qty],
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear}' THEN RevAmount ELSE 0 END) AS [FY{fiscalYear}_Rev],
        SUM(CASE WHEN FiscalYear = 'FY{fiscalYear}' THEN QtyInvoiced ELSE 0 END) AS [FY{fiscalYear}_Qty],
        {monthColumns}
    FROM AggregatedData
    GROUP BY
        Customer, [Customer Name], [Ship To Num], [Ship To City], [Ship To State],
        slsman, SalespersonName, [Bill To State], Uf_SalesRegion, RegionName,
        Item, ItemDescription
    OPTION (RECOMPILE, OPTIMIZE FOR UNKNOWN);";
    }

    public async Task<List<CustomerShipment>> GetShipmentsData(ShipmentsParameters parameters)
    {
        EnsureAuth();
        EnsureRepContext();

        if (!_csiOptions.UseApi)
        {

            using var connection = new SqlConnection(_connectionString);

            var authState = await _authenticationStateProvider!.GetAuthenticationStateAsync();
            var user = authState.User;
            var repCode = _repCodeContext!.CurrentRepCode;

            IEnumerable<string>? allowedRegions = null;
            if (repCode == "LAWxxx")
            {
                allowedRegions = user.Claims
                    .Where(c => c.Type == "Region")
                    .Select(c => c.Value)
                    .Distinct()
                    .ToList();
            }
            else if (repCode == "LAW")
            {
                allowedRegions = _repCodeContext.CurrentRegions;
            }

            parameters.ShipToRegion = (allowedRegions != null && allowedRegions.Any())
                ? string.Join(",", allowedRegions)
                : null;

            await connection.OpenAsync();

            var results = await connection.QueryAsync<CustomerShipment>(@"
            EXEC RepPortal_GetShipmentsSp 
                @BeginShipDate, 
                @EndShipDate, 
                @RepCode, 
                @CustNum, 
                @CorpNum, 
                @CustType, 
                @EndUserType,
                @AllowedRegions;",
                new
                {
                    parameters.BeginShipDate,
                    parameters.EndShipDate,
                    RepCode = repCode,
                    parameters.CustNum,
                    parameters.CorpNum,
                    parameters.CustType,
                    parameters.EndUserType,
                    AllowedRegions = parameters.ShipToRegion
                });

            return results.ToList();
        }
        else 
        {
            return await GetShipmentsDataApiAsync(parameters);
        }
    }

    /// <summary>
    /// Fetches shipments from Syteline APIs (SLCoShips + AitSsBOLs) and merges BOL details
    /// onto the shipment records. Link: SLCoShips.BolNumber = AitSsBOLs.ShipmentId.
    /// </summary>
    public async Task<List<CustomerShipment>> GetShipmentsDataApiAsync(ShipmentsParameters parameters)
    {
        EnsureAuth();
        EnsureRepContext();

        if (_csiRestClient == null)
            throw new InvalidOperationException("CSI REST client is not available.");

        var authState = await _authenticationStateProvider!.GetAuthenticationStateAsync();
        var user = authState.User;
        var repCode = _repCodeContext!.CurrentRepCode;

        IEnumerable<string>? allowedRegions = null;
        if (repCode == "LAW")
        {
            allowedRegions = _repCodeContext.CurrentRegions;
        }

        // ── Get allowed CustNum+CustSeq pairs from SLCustomers when region filtering is active ──
        HashSet<string>? allowedCustKeys = null;
        Dictionary<string, string>? custRegionLookup = null;
        if (allowedRegions != null && allowedRegions.Any())
        {
            var custQuery = new Dictionary<string, string>
            {
                ["props"] = "CustNum,CustSeq,Uf_SalesRegion",
                ["filter"] = In("Uf_SalesRegion", allowedRegions),
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var custJson = await _csiRestClient.GetAsync("json/SLCustomers/adv", custQuery);
            var custResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(custJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            if (custResponse.MessageCode == 0)
            {
                allowedCustKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                custRegionLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in custResponse.Items)
                {
                    var cn = row.FirstOrDefault(c =>
                        string.Equals(c.Name, "CustNum", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                    var cs = row.FirstOrDefault(c =>
                        string.Equals(c.Name, "CustSeq", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? "0";
                    var region = row.FirstOrDefault(c =>
                        string.Equals(c.Name, "Uf_SalesRegion", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(cn))
                    {
                        var key = $"{cn}|{cs}";
                        allowedCustKeys.Add(key);
                        if (!string.IsNullOrWhiteSpace(region))
                            custRegionLookup[key] = region;
                    }
                }
            }
        }

        // ── SLCoShips call ──
        var slFilters = new List<string> { Eq("CoSlsman", repCode) };
        if (parameters.BeginShipDate != default)
            slFilters.Add($"ShipDate >= '{parameters.BeginShipDate:yyyyMMdd}'");
        if (parameters.EndShipDate != default)
            slFilters.Add($"ShipDate <= '{parameters.EndShipDate:yyyyMMdd}'");

        var slProps = string.Join(",", new[]
        {
            "CoCustNum", "CoCustSeq", "CadrName", "CoCustPo", "CoNum", "CoLine",
            "CoiItem", "CoiDescription", "CoiDueDate", "ShipDate",
            "QtyShipped", "DerNetPrice", "BolNumber"
        });

        var slQuery = new Dictionary<string, string>
        {
            ["props"] = slProps,
            ["filter"] = string.Join(" AND ", slFilters),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var slJson = await _csiRestClient.GetAsync("json/SLCoShips/adv", slQuery);
        var slResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(slJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        if (slResponse.MessageCode != 0)
            throw new InvalidOperationException(slResponse.Message);

        // ── Filter by allowed CustNum+CustSeq when region filtering is active ──
        var filteredItems = slResponse.Items;
        if (allowedCustKeys != null && allowedCustKeys.Count > 0)
        {
            filteredItems = slResponse.Items.Where(row =>
            {
                var cn = row.FirstOrDefault(c =>
                    string.Equals(c.Name, "CoCustNum", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                var cs = row.FirstOrDefault(c =>
                    string.Equals(c.Name, "CoCustSeq", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? "0";
                return !string.IsNullOrWhiteSpace(cn) && allowedCustKeys.Contains($"{cn}|{cs}");
            }).ToList();
        }

        var shipments = filteredItems
            .Select(row => MapRow<CustomerShipment>(row))
            .ToList();

        // ── Populate ShipToRegion from SLCustomers lookup ──
        if (custRegionLookup != null && custRegionLookup.Count > 0)
        {
            for (int i = 0; i < filteredItems.Count && i < shipments.Count; i++)
            {
                var row = filteredItems[i];
                var cn = row.FirstOrDefault(c =>
                    string.Equals(c.Name, "CoCustNum", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                var cs = row.FirstOrDefault(c =>
                    string.Equals(c.Name, "CoCustSeq", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? "0";
                if (!string.IsNullOrWhiteSpace(cn)
                    && custRegionLookup.TryGetValue($"{cn}|{cs}", out string? region))
                {
                    shipments[i].ShipToRegion = region;
                }
            }
        }

        // Collect BolNumbers from SLCoShips to filter the AitSsBOLs call
        var bolNumbers = shipments
            .Where(s => s.BolNumber.HasValue && s.BolNumber.Value != 0)
            .Select(s => s.BolNumber!.Value)
            .Distinct()
            .ToList();

        if (bolNumbers.Count == 0)
        {
            await LogReportUsageAsync(repCode, "GetShipmentsDataApi");
            return shipments;
        }

        // ── AitSsBOLs call ──
        var bolFilters = new List<string>
        {
            In("ShipmentId", bolNumbers.Select(n => n.ToString()))
        };

        var bolProps = string.Join(",", new[]
        {
            "ShipmentId", "InvoiceeState", "ConsigneeState", "Whse",
            "CarrierCode", "ShipCode", "ShipCodeDesc", "ShipDate",
            "BillTransportationTo", "TrackingNumber",
            "InvoiceeName", "CustNum", "CustSeq"
        });

        var bolQuery = new Dictionary<string, string>
        {
            ["props"] = bolProps,
            ["filter"] = string.Join(" AND ", bolFilters),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var bolJson = await _csiRestClient.GetAsync("json/ait_ss_bols/adv", bolQuery);
        var bolResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(bolJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        if (bolResponse.MessageCode != 0)
            throw new InvalidOperationException(bolResponse.Message);

        var bolsByShipmentId = bolResponse.Items
            .Select(row => MapRow<BolInfo>(row))
            .Where(b => b.ShipmentId.HasValue)
            .GroupBy(b => b.ShipmentId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        try
        {
            // ── Merge BOL info onto shipment rows ──
            foreach (var shipment in shipments)
            {
                if (!shipment.BolNumber.HasValue
                    || !bolsByShipmentId.TryGetValue(shipment.BolNumber.Value, out BolInfo? bol))
                    continue;

                if (!string.IsNullOrWhiteSpace(bol.InvoiceeState))
                    shipment.BillToState = bol.InvoiceeState;
                if (!string.IsNullOrWhiteSpace(bol.ConsigneeState))
                    shipment.ShipToState = bol.ConsigneeState;
                if (!string.IsNullOrWhiteSpace(bol.Whse))
                    shipment.Whse = bol.Whse;
                if (!string.IsNullOrWhiteSpace(bol.CarrierCode))
                    shipment.CarrierCode = bol.CarrierCode;
                if (!string.IsNullOrWhiteSpace(bol.ShipCode))
                    shipment.ShipCode = bol.ShipCode;
                else if (!string.IsNullOrWhiteSpace(bol.ShipCodeDesc))
                    shipment.ShipCode = bol.ShipCodeDesc;
                if (!string.IsNullOrWhiteSpace(bol.BillTransportationTo))
                    shipment.FreightTerms = bol.BillTransportationTo;
                if (!string.IsNullOrWhiteSpace(bol.TrackingNumber))
                    shipment.TrackingNumber = bol.TrackingNumber;
            }

            await LogReportUsageAsync(repCode, "GetShipmentsDataApi");
            return shipments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping shipment data");
            return new List<CustomerShipment>(); // or partial results
        }
    }

    private class BolInfo
    {
        [CsiField("ShipmentId")] public int? ShipmentId { get; set; }
        [CsiField("InvoiceeState")] public string? InvoiceeState { get; set; }
        [CsiField("ConsigneeState")] public string? ConsigneeState { get; set; }
        [CsiField("Whse")] public string? Whse { get; set; }
        [CsiField("CarrierCode")] public string? CarrierCode { get; set; }
        [CsiField("ShipCode")] public string? ShipCode { get; set; }
        [CsiField("ShipCodeDesc")] public string? ShipCodeDesc { get; set; }
        [CsiField("ShipDate")] public DateTime? ShipDate { get; set; }
        [CsiField("BillTransportationTo")] public string? BillTransportationTo { get; set; }
        [CsiField("TrackingNumber")] public string? TrackingNumber { get; set; }
        [CsiField("InvoiceeName")] public string? InvoiceeName { get; set; }
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int? CustSeq { get; set; }
    }

    public class ShipmentsParameters
    {
        public DateTime BeginShipDate { get; set; }
        public DateTime EndShipDate { get; set; }
        public string? RepCode { get; set; }
        public string? CustNum { get; set; }
        public string? CorpNum { get; set; }
        public string? CustType { get; set; }
        public string? EndUserType { get; set; }
        public string? ShipToRegion { get; set; }
    }

    public string? GetRepAgency(string repCode)
    {
        using var connection = new SqlConnection(_connectionString);
        return connection.QueryFirstOrDefault<string>(
            "SELECT name as AgencyName FROM Chap_SlsmanNameV WHERE slsman = @RepCode",
            new { RepCode = repCode });
    }

    public async Task<List<string>> GetAllRepCodesAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            SELECT slsman 
            FROM Chap_SlsmanNameV  
            WHERE slsman IN (
                SELECT DISTINCT slsman 
                FROM customer_mst 
                WHERE stat = 'A' AND cust_seq = 0 AND cust_num <> 'LILBOY'
            )
            AND slsman NOT IN ('CHA', 'REP', 'KBM')
            ORDER BY slsman;";
        var results = await connection.QueryAsync<string>(sql);
        return results.ToList();
    }

    public async Task<List<string>> GetRegionsForRepCodeAsync(string repCode)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var results = await connection.QueryAsync<string>(
            @"SELECT DISTINCT TRIM(Uf_SalesRegion) AS Uf_SalesRegion
              FROM customer_mst 
              WHERE slsman = @RepCode AND Uf_SalesRegion IS NOT NULL AND Uf_SalesRegion <> ''
              ORDER BY TRIM(Uf_SalesRegion);", new { RepCode = repCode });

        return results.ToList();
    }

    public async Task<List<RegionItem>> GetRegionInfoForRepCodeAsync(string repCode)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var results = await connection.QueryAsync<RegionItem>(
            @"SELECT *
              FROM Chap_RegionNames
              WHERE Region <> 'L'
              ORDER BY Region;",
            new { RepCode = repCode });

        return results.ToList();
    }

    public async Task<List<RepInfo>> GetAllRepCodeInfoAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var results = await connection.QueryAsync<RepInfo>(
            @"SELECT slsman AS RepCode, UPPER(name) AS RepName
              FROM Chap_SlsmanNameV  
              WHERE slsman IN (
                  SELECT DISTINCT slsman 
                  FROM customer_mst 
                  WHERE stat = 'A' AND cust_seq = 0 AND cust_num <> 'LILBOY'
              )
              ORDER BY slsman;");
        return results.ToList();
    }

    public async Task<List<RegionInfo>> GetAllRegionsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var results = await connection.QueryAsync<RegionInfo>(
            @"SELECT Region, RegionName 
              FROM Chap_RegionNames  
              WHERE LEN(Region) > 1 
              ORDER BY Region;");
        return results.ToList();
    }

    public async Task<List<CustomerOrderSummary>> GetOpenOrderSummariesAsync()
    {
        EnsureRepContext();

        const string sql = @"
            SELECT
                OpenOrders.Cust,
                Cust.CorpName AS Name,
                OpenOrders.Shippable_U AS ShippableUnits,
                OpenOrders.Future_U AS FutureUnits,
                OpenOrders.Total_U AS TotalUnits,
                OpenOrders.Shippable_D AS ShippableDollars,
                OpenOrders.Future_D AS FutureDollars,
                OpenOrders.Total_D AS TotalDollars
            FROM CIISQL10.INTRANET.DBO.Cust Cust
            INNER JOIN CIISQL10.INTRANET.DBO.OpenOrders OpenOrders
                ON Cust.CustSeq = OpenOrders.CustSeq AND Cust.Cust = OpenOrders.Cust
            WHERE Cust.slsman = @RepCode
            ORDER BY Cust.CorpName;";

        using var connection = new SqlConnection(_connectionString);
        var summaries = await connection.QueryAsync<CustomerOrderSummary>(sql,
            new { RepCode = _repCodeContext!.CurrentRepCode });
        return summaries.ToList();
    }

    public async Task<List<OrderDetail>> GetOpenOrderDetailsAsync(string customerId)
    {
        const string detailSql = @"
            SELECT
                ORDERS.CUST AS Cust,
                Cust.CorpName AS Name,
                ORDERS.DUEDATE AS DueDate,
                ORDERS.ORDDATE AS OrdDate,
                ORDERS.PromDate AS PromDate,
                ORDERS.CustPO,
                ORDERS.CONUM AS CoNum,
                ORDERS.ITEM AS Item,
                ORDERS.PRICE AS Price,
                ORDERS.ORDQTY AS OrdQty,
                (ORDERS.Price * ORDERS.OrdQty) AS Dollars,
                Cust.B2Name AS ShipToName
            FROM CIISQL10.INTRANET.DBO.ORDERS ORDERS
            LEFT JOIN CIISQL10.INTRANET.DBO.Cust CUST
                ON ORDERS.CUST = Cust.Cust AND ORDERS.CUSTSEQ = Cust.CustSeq
            WHERE ORDERS.STAT = 'O' AND ORDERS.CUST = @CustomerId
            ORDER BY Orders.DueDate, ORDERS.PromDate, Orders.CoNum;";

        using var connection = new SqlConnection(_connectionString);
        var details = await connection.QueryAsync<OrderDetail>(detailSql, new { CustomerId = customerId });
        return details.ToList();
    }

    public async Task<List<OrderDetail>> GetAllOpenOrderDetailsAsync()
    {
        EnsureRepContext();

        var repCode = _repCodeContext!.CurrentRepCode;
        var allowedRegions = _repCodeContext.CurrentRegions;

        _logger?.LogInformation(
            "GetAllOpenOrderDetailsAsync started. Rep={RepCode}, UseApi={UseApi}, RegionCount={RegionCount}",
            repCode,
            _csiOptions.UseApi,
            allowedRegions?.Count ?? 0
        );

        if (!_csiOptions.UseApi)
        {

            var sql = @"
        SELECT
              co.Cust_Num AS Cust
            , cc.Name AS CustName
            , co.cust_seq AS ShipToNum
            , ca.name AS ShipToName
            , ci.due_date AS DueDate
            , co.order_date AS OrdDate
            , co.Cust_PO AS CustPO
            , co.co_num AS CoNum
            , ci.ITEM AS Item
            , ISNULL(Item.Description, ci.Item) AS ItemDesc
            , ci.PRICE AS Price
            , ci.qty_ordered AS OrdQty
            , ci.qty_ordered - ci.qty_shipped AS OpenQty
            , (ci.qty_ordered - ci.qty_shipped) * ci.price AS OpenDollars
            , cu.Uf_SalesRegion AS ShipToRegion
        FROM BAT_App.dbo.coitem_mst ci
        JOIN BAT_App.dbo.co_mst co ON co.co_num = ci.co_num 
        JOIN Bat_App.dbo.customer_mst cu ON co.cust_num = cu.cust_num AND co.cust_seq = cu.cust_seq
        JOIN Bat_App.dbo.custaddr_mst ca ON co.cust_num = ca.cust_num AND ca.cust_seq = 0
        LEFT JOIN CIISQL10.BAT_App.DBO.Item_mst Item ON Item.Item = ci.Item
        LEFT JOIN CIISQL10.BAT_App.dbo.Customer_CorpCust_Vw cc ON cu.cust_num = cc.cust_num
        WHERE ci.STAT = 'O'
          AND co.slsman = @RepCode
          AND ci.qty_ordered - ci.qty_shipped > 0";

            if (allowedRegions != null && allowedRegions.Any())
                sql += " AND cu.Uf_SalesRegion IN @AllowedRegions";

            sql += " ORDER BY cc.Name, co.cust_seq;";

            using var connection = new SqlConnection(_connectionString);
            _logger?.LogInformation("Executing SQL: {Sql}", sql);

            var parameters = new { RepCode = repCode, AllowedRegions = allowedRegions?.ToArray() };
            var allDetails = await connection.QueryAsync<OrderDetail>(sql, parameters);
            return allDetails.ToList();
        }
        else
        {
            return await GetAllOpenOrderDetailsApiAsync();
        }
    }


    public async Task<List<OrderDetail>> GetAllOpenOrderDetailsApiAsync()
    {
        string repCode = _repCodeContext.CurrentRepCode;
        List<string> salesRegions = _repCodeContext.CurrentRegions;

        await LogReportUsageAsync(repCode, "GetAllOpenOrderDetailsAsync");
        var filters = new List<string>
        {
            "QtyOrdered > QtyShipped",
            Eq("CoSlsman", repCode)
        };
        if (_csiOptions.OpenOrderCutoffDate.HasValue)
        {
            filters.Add(DateGt("DerDueDate", _csiOptions.OpenOrderCutoffDate.Value));
        }
        // Apply SalesRegion filter ONLY for LAW
        if (salesRegions is { Count: > 0 })  // Not limited to LAW for future use
        {
            filters.Add(In("SalesRegion", salesRegions));
        }

        var filterClause = string.Join(" AND ", filters);
        var query = new Dictionary<string, string>
        {
            ["props"] =
                "CoCustNum,Adr0Name,CoOrderDate,CoCustPo,CoNum,DerDueDate,SalesRegion," +
                "Item,Description,Price,QtyOrdered,QtyShipped,AdrName,CoCustSeq",

            ["filter"] = filterClause,

            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var json = await _csiRestClient.GetAsync("json/SLCoitems/adv", query);

        var response = JsonSerializer.Deserialize<MgRestAdvResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        if (response.MessageCode != 0)
            throw new InvalidOperationException(response.Message);

        return response.Items
            .Select(row => MapRow<OrderDetail>(row))
            .ToList();
    }

    private static T MapRow<T>(List<MgNameValue> row)
        where T : new()
    {
        var obj = new T();
        var props = typeof(T).GetProperties();

        foreach (var prop in props)
        {
            var attr = prop.GetCustomAttribute<CsiFieldAttribute>();
            if (attr == null)
                continue;

            var cell = row.FirstOrDefault(c =>
                string.Equals(c.Name, attr.FieldName, StringComparison.OrdinalIgnoreCase));

            if (cell?.Value == null)
                continue;

            var targetType = Nullable.GetUnderlyingType(prop.PropertyType)
                             ?? prop.PropertyType;

            object? value = ConvertTo(cell.Value, targetType);
            prop.SetValue(obj, value);
        }

        return obj;
    }


    private static object? ConvertTo(string? raw, Type targetType)
    {
        if (targetType == null)
            throw new ArgumentNullException(nameof(targetType));

        // Handle nullable<T>
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        var isNullable = underlyingType != null;
        var effectiveType = underlyingType ?? targetType;

        if (string.IsNullOrWhiteSpace(raw))
        {
            if (isNullable || !effectiveType.IsValueType)
                return null;

            throw new FormatException($"Cannot convert null/empty value to non-nullable type '{effectiveType.Name}'.");
        }

        try
        {
            if (effectiveType == typeof(string))
                return raw;

            if (effectiveType == typeof(DateTime))
            {
                string[] formats =
                {
                "yyyyMMdd HH:mm:ss.fff",
                "yyyyMMdd",
                "yyyy-MM-dd",
                "yyyy-MM-dd HH:mm:ss",
                "o" // ISO 8601
            };

                if (DateTime.TryParseExact(
                        raw,
                        formats,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt))
                    return dt;

                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal, out dt))
                    return dt;

                throw new FormatException($"Cannot convert '{raw}' to DateTime.");
            }

            if (effectiveType == typeof(decimal))
            {
                if (decimal.TryParse(raw, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out var dec))
                    return dec;

                throw new FormatException($"Cannot convert '{raw}' to decimal.");
            }

            if (effectiveType == typeof(int))
            {
                if (int.TryParse(raw, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var i))
                    return i;

                if (decimal.TryParse(raw, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out var d))
                {
                    if (d % 1 != 0)
                        throw new FormatException(
                            $"Value '{raw}' is not a whole number and cannot be converted to int.");

                    if (d > int.MaxValue || d < int.MinValue)
                        throw new OverflowException(
                            $"Value '{raw}' is outside the range of Int32.");

                    return (int)d;
                }

                throw new FormatException($"Cannot convert '{raw}' to int.");
            }

            if (effectiveType == typeof(bool))
            {
                if (bool.TryParse(raw, out var b))
                    return b;

                if (raw == "0") return false;
                if (raw == "1") return true;

                throw new FormatException($"Cannot convert '{raw}' to bool.");
            }

            if (effectiveType == typeof(Guid))
            {
                if (Guid.TryParse(raw, out var g))
                    return g;

                throw new FormatException($"Cannot convert '{raw}' to Guid.");
            }

            if (effectiveType.IsEnum)
            {
                if (Enum.TryParse(effectiveType, raw, true, out var enumValue))
                    return enumValue;

                throw new FormatException(
                    $"Cannot convert '{raw}' to enum '{effectiveType.Name}'.");
            }

            return Convert.ChangeType(raw, effectiveType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new FormatException(
                $"Failed to convert '{raw}' to type '{effectiveType.Name}'.", ex);
        }
    }

    private static object ConvertToOLD(string raw, Type targetType)
    {
        if (targetType == typeof(DateTime))
            return DateTime.ParseExact(
                raw,
                "yyyyMMdd HH:mm:ss.fff",
                CultureInfo.InvariantCulture);

        if (targetType == typeof(decimal))
            return decimal.Parse(raw, CultureInfo.InvariantCulture);

        if (targetType == typeof(int))
        {
            if (int.TryParse(raw, out var i))
                return i;

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return (int)d;

            throw new FormatException($"Cannot convert '{raw}' to int.");
        }

        return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
    }
    // Other ISalesService methods remain NotImplemented for now

    private static string Eq(string field, string value) =>
        $"{field} = '{value.Replace("'", "''")}'";

    private static string In(string field, IEnumerable<string> values)
    {
        var safeValues = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => $"'{v.Replace("'", "''")}'");

        return $"{field} IN ({string.Join(",", safeValues)})";
    }

    private static string DateGt(string field, DateTime date) =>
        $"{field} > '{date:yyyyMMdd}'";








    public async Task LogReportUsageAsync(string repCode, string reportName)
    {
        var adminUser = _repCodeContext?.CurrentLastName;

        const string sql = @"
        INSERT INTO CIISQL10.RepPortal.dbo.ReportUsageHistory (RepCode, ReportName, RunTime, AdminUser)
        VALUES (@RepCode, @ReportName, @RunTime, @AdminUser);";

        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new
        {
            RepCode = repCode,
            ReportName = reportName,
            RunTime = DateTime.Now,
            AdminUser = adminUser
        });
    }

    public async Task<List<Dictionary<string, object>>> RunDynamicQueryAsync(string sql)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var rows = await connection.QueryAsync(sql);

        return MaterializeToDictionaries(rows);
    }

    public async Task<List<InvoiceRptDetail>> GetInvoiceRptData(InvoiceRptParameters parameters)
    {
        EnsureRepContext();

        parameters.RepCode = _repCodeContext!.CurrentRepCode;
        parameters.AllowedRegions = parameters.RepCode == "LAW"
            ? _repCodeContext.CurrentRegions?.ToList() ?? new List<string>()
            : new List<string>();

        if (_csiOptions.UseApi)
        {
            return await GetInvoiceRptDataApiAsync(parameters);
        }

        using var connection = new SqlConnection(_connectionString);

        var allowedRegionsCsv = parameters.AllowedRegions.Any()
            ? string.Join(",", parameters.AllowedRegions)
            : null;

        await connection.OpenAsync();

        var results = await connection.QueryAsync<InvoiceRptDetail>(@"
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
                parameters.BeginInvoiceDate,
                parameters.EndInvoiceDate,
                RepCode = parameters.RepCode,   // security
                parameters.CustNum,
                parameters.CorpNum,
                parameters.CustType,
                parameters.EndUserType,
                AllowedRegions = allowedRegionsCsv
            });

        if (!string.IsNullOrWhiteSpace(parameters.CoNum))
        {
            var match = parameters.CoNum.Trim().ToUpperInvariant();
            results = results.Where(r => !string.IsNullOrWhiteSpace(r.CoNum) &&
                                         r.CoNum.Trim().ToUpperInvariant() == match);
        }

        return results.ToList();
    }

    /// <summary>
    /// Fetches invoice data from Syteline APIs (SLInvHdrs + SLInvItemAlls + SLCoitems)
    /// and merges the results into InvoiceRptDetail records.
    /// </summary>
    ///
    ///
    /// 
   
    
    
    
    
    public async Task<List<InvoiceRptDetail>> GetInvoiceRptDataApiAsync(InvoiceRptParameters parameters)
    {
        EnsureRepContext();

        if (_csiRestClient == null)
            throw new InvalidOperationException("CSI REST client is not available.");

        string repCode = parameters.RepCode ?? _repCodeContext!.CurrentRepCode;

        // ── 1. SLInvHdrs (invoice headers) ──
        var hdrFilters = new List<string>
        {
            Eq("Slsman", repCode),
            $"InvDate >= '{parameters.BeginInvoiceDate:yyyyMMdd}'",
            $"InvDate <= '{parameters.EndInvoiceDate:yyyyMMdd}'"
        };
        if (!string.IsNullOrWhiteSpace(parameters.CustNum))
            hdrFilters.Add(Eq("CustNum", parameters.CustNum));

        var hdrProps = string.Join(",", new[]
        {
            "InvNum", "InvSeq", "CustNum", "CustSeq", "AddrName",
            "State", "ShipDate", "CustPo", "InvDate"
        });

        var hdrQuery = new Dictionary<string, string>
        {
            ["props"] = hdrProps,
            ["filter"] = string.Join(" AND ", hdrFilters),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var hdrJson = await _csiRestClient.GetAsync("json/SLInvHdrs/adv", hdrQuery);
        var hdrResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(hdrJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        if (hdrResponse.MessageCode != 0)
            throw new InvalidOperationException(hdrResponse.Message);

        var headers = hdrResponse.Items
            .Select(row => MapRow<InvHdrInfo>(row))
            .ToList();

        if (headers.Count == 0)
        {
            await LogReportUsageAsync(repCode, "GetInvoiceRptDataApi");
            return new List<InvoiceRptDetail>();
        }

        // Build lookup: (InvNum, InvSeq) → header
        var hdrLookup = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum))
            .GroupBy(h => (h.InvNum!, h.InvSeq))
            .ToDictionary(g => g.Key, g => g.First());

        // ── 2. SLInvItemAlls (invoice line items) ──
        var invNums = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum))
            .Select(h => h.InvNum!)
            .Distinct()
            .ToList();

        var itemFilters = new List<string>
        {
            In("InvNum", invNums)
        };

        var itemProps = string.Join(",", new[]
        {
            "InvNum", "InvSeq", "Item", "QtyInvoiced", "Price", "CoNum", "CoLine", "SiteRef"
        });

        var itemQuery = new Dictionary<string, string>
        {
            ["props"] = itemProps,
            ["filter"] = string.Join(" AND ", itemFilters),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var itemJson = await _csiRestClient.GetAsync("json/SLInvItemAlls/adv", itemQuery);
        var itemResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(itemJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        if (itemResponse.MessageCode != 0)
            throw new InvalidOperationException(itemResponse.Message);

        var invoiceItems = itemResponse.Items
            .Select(row => MapRow<InvoiceRptDetail>(row))
            .ToList();

        // Merge header data into items by (InvNum, InvSeq)
        foreach (var item in invoiceItems)
        {
            // Try to find InvSeq from the raw row for matching
            var rawRow = itemResponse.Items[invoiceItems.IndexOf(item)];
            var invSeqCell = rawRow.FirstOrDefault(c =>
                string.Equals(c.Name, "InvSeq", StringComparison.OrdinalIgnoreCase));
            int invSeq = 0;
            if (invSeqCell?.Value != null)
                int.TryParse(invSeqCell.Value, out invSeq);

            var key = (item.InvNum ?? "", invSeq);
            if (hdrLookup.TryGetValue(key, out InvHdrInfo? hdr))
            {
                item.Cust = hdr.CustNum ?? "";
                item.CustSeq = hdr.CustSeq;
                item.Name = hdr.AddrName ?? "";
                item.State = hdr.State ?? "";
                item.Ship_Date = hdr.ShipDate;
                item.CustPO = hdr.CustPo ?? "";
                item.InvDate = hdr.InvDate ?? DateTime.MinValue;
            }
        }

        // ── 3. SLCoitems (CO enrichment) ──
        var coNums = invoiceItems
            .Where(i => !string.IsNullOrWhiteSpace(i.CoNum))
            .Select(i => i.CoNum!)
            .Distinct()
            .ToList();

        if (coNums.Count > 0)
        {
            var coFilters = new List<string>
            {
                In("CoNum", coNums)
            };

            var coProps = string.Join(",", new[]
            {
                "CoNum", "CoLine", "Adr0Name", "DueDate", "CoOrderDate"
            });

            var coQuery = new Dictionary<string, string>
            {
                ["props"] = coProps,
                ["filter"] = string.Join(" AND ", coFilters),
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var coJson = await _csiRestClient.GetAsync("json/SLCoitems/adv", coQuery);
            var coResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(coJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            if (coResponse.MessageCode == 0)
            {
                var coItems = coResponse.Items
                    .Select(row => MapRow<CoItemInfo>(row))
                    .ToList();

                // Build lookup: (CoNum, CoLine) → CO item
                var coLookup = coItems
                    .Where(c => !string.IsNullOrWhiteSpace(c.CoNum))
                    .GroupBy(c => (c.CoNum!, c.CoLine))
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var item in invoiceItems)
                {
                    if (string.IsNullOrWhiteSpace(item.CoNum))
                        continue;

                    // Try to find CoLine from the raw item row
                    int itemIndex = invoiceItems.IndexOf(item);
                    var rawRow = itemResponse.Items[itemIndex];
                    var coLineCell = rawRow.FirstOrDefault(c =>
                        string.Equals(c.Name, "CoLine", StringComparison.OrdinalIgnoreCase));
                    int coLine = 0;
                    if (coLineCell?.Value != null)
                        int.TryParse(coLineCell.Value, out coLine);

                    var coKey = (item.CoNum!, coLine);
                    if (coLookup.TryGetValue(coKey, out CoItemInfo? co))
                    {
                        item.B2Name = co.Adr0Name ?? "";
                        item.DueDate = co.DueDate;
                        item.OrdDate = co.CoOrderDate;
                    }
                }
            }
        }

        // ── Calculate ExtPrice and set Slsman ──
        foreach (var item in invoiceItems)
        {
            item.ExtPrice = item.InvQty * item.Price;
            item.Slsman = repCode;
        }

        // ── Apply optional CoNum client-side filter ──
        IEnumerable<InvoiceRptDetail> results = invoiceItems;
        if (!string.IsNullOrWhiteSpace(parameters.CoNum))
        {
            var match = parameters.CoNum.Trim().ToUpperInvariant();
            results = results.Where(r => !string.IsNullOrWhiteSpace(r.CoNum) &&
                                         r.CoNum.Trim().ToUpperInvariant() == match);
        }

        await LogReportUsageAsync(repCode, "GetInvoiceRptDataApi");
        return results.ToList();
    }

    private class InvHdrInfo
    {
        [CsiField("InvNum")] public string? InvNum { get; set; }
        [CsiField("InvSeq")] public int InvSeq { get; set; }
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int CustSeq { get; set; }
        [CsiField("Slsman")] public string? Slsman { get; set; }
        [CsiField("AddrName")] public string? AddrName { get; set; }
        [CsiField("State")] public string? State { get; set; }
        [CsiField("ShipDate")] public DateTime? ShipDate { get; set; }
        [CsiField("CustPo")] public string? CustPo { get; set; }
        [CsiField("InvDate")] public DateTime? InvDate { get; set; }
    }

    private class CoItemInfo
    {
        [CsiField("CoNum")] public string? CoNum { get; set; }
        [CsiField("CoLine")] public int CoLine { get; set; }
        [CsiField("Adr0Name")] public string? Adr0Name { get; set; }
        [CsiField("DueDate")] public DateTime? DueDate { get; set; }
        [CsiField("CoOrderDate")] public DateTime? CoOrderDate { get; set; }
    }

    private class InvItemInfo
    {
        [CsiField("InvNum")] public string? InvNum { get; set; }
        [CsiField("InvSeq")] public int InvSeq { get; set; }
        [CsiField("QtyInvoiced")] public decimal QtyInvoiced { get; set; }
        [CsiField("Price")] public decimal Price { get; set; }
    }

    private class CustAddrInfo
    {
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int CustSeq { get; set; }
        [CsiField("Name")] public string? Name { get; set; }
        [CsiField("City")] public string? City { get; set; }
        [CsiField("State")] public string? State { get; set; }
    }

    // Fixed: instance method; corrected aliases; Dapper mapping; no conn/connection mixup
    public async Task<List<SaleRow>> GetRecentSalesAsync()
    {
        var repCode = _repCodeContext!.CurrentRepCode;
        var allowedRegions = _repCodeContext.CurrentRegions;

        string sql = @"
           SELECT 
     ih.site_ref,
      ih.inv_date AS OrderDate,
      ca.cust_num AS CustNum,
      ca.name     AS ShipToName,
      ca0.name as CustomerName,
      im.description AS ProductName,
      ii.item     AS ItemNum,
      ii.qty_invoiced AS Quantity,
      ISNULL((ii.qty_invoiced * ii.price),0) AS SalesAmount,
      fc.MonthShort, fc.DayOfMonth, fc.DayShort, fc.FiscalYear, fc.QuarterOfFiscalYear, fc.MonthOfFiscalYear
      ,rn.RegionName
      ,ca.City, ca.State, ca.Zip
  FROM inv_hdr_mst_all ih
  JOIN inv_item_mst_all ii ON ih.inv_num = ii.inv_num AND ih.inv_seq = ii.inv_seq
  --JOIN co_mst co        ON ih.co_num = co.co_num
  --JOIN coitem_mst ci    ON co.co_num = ci.co_num AND ii.co_line = ci.co_line
  JOIN custaddr_mst ca  ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
  join custaddr_mst ca0 on ih.cust_num = ca0.cust_num and ca0.cust_seq = 0
  JOIN customer_mst cu on ih.cust_num=cu.cust_num and cu.cust_seq = ih.cust_seq
  JOIN item_mst im      ON ii.item = im.item
LEFT JOIN Bat_App.dbo.Chap_RegionNames rn WITH (NOLOCK) ON rn.Region = cu.Uf_SalesRegion
  Join tempwork.dbo.FiscalCalendarVw fc on Cast(ih.inv_date as date)=fc.[Date]
            WHERE 1 = 1 
              AND ih.inv_date > dbo.midnightof('9/1/2022')
              AND cu.slsman = @RepCode";


        if (allowedRegions != null && allowedRegions.Any())
            sql += " AND cu.Uf_SalesRegion IN @AllowedRegions";





        using var connection = new SqlConnection(_connectionString);
        _logger?.LogInformation("Executing SQL: {Sql}", sql);

        var parameters = new { RepCode = repCode, AllowedRegions = allowedRegions?.ToArray() };


        var rows = await connection.QueryAsync<SaleRow>(sql, parameters);
        return rows.ToList();



    }

    public async Task<List<CustType>> GetCustomerTypesListAsync()
    {
        var x = new List<CustType>();
        return x;
    }

    public class InvoiceRptParameters
    {
        public DateTime BeginInvoiceDate { get; set; }
        public DateTime EndInvoiceDate { get; set; }
        public string RepCode { get; set; } = "";
        public string? CustNum { get; set; }
        public string? CorpNum { get; set; }
        public string? CustType { get; set; }
        public string? EndUserType { get; set; }
        public List<string> AllowedRegions { get; set; } = new();
        public string? CoNum { get; set; }
    }

    public async Task<List<Dictionary<string, object>>> GetSalesReportDataUsingInvRepApiAsync()
    {
        EnsureRepContext();

        if (_csiRestClient == null)
            throw new InvalidOperationException("CSI REST client is not available.");

        string repCode = _repCodeContext!.CurrentRepCode;
        IEnumerable<string>? allowedRegions = repCode == "LAW"
            ? _repCodeContext.CurrentRegions
            : null;

        // ── Fiscal year boundaries ──
        var today = DateTime.Today;
        int fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;
        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyMinus3Start = new DateTime(fiscalYear - 4, 9, 1);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var allMonths = Enumerable.Range(0, 36 + currentFiscalMonth)
            .Select(i => fyMinus3Start.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        var fyMinus3Months = allMonths.Take(12).ToList();
        var fyMinus2Months = allMonths.Skip(12).Take(12).ToList();
        var fyMinus1Months = allMonths.Skip(24).Take(12).ToList();
        var currentFYMonths = allMonths.Skip(36).Take(currentFiscalMonth).ToList();

        // ── 1. SLInvHdrs ──
        var hdrFilters = new List<string>
        {
            Eq("Slsman", repCode),
            $"InvDate >= '{fyMinus3Start:yyyyMMdd}'"
        };

        var hdrProps = string.Join(",", new[]
        {
            "InvNum", "InvSeq", "CustNum", "CustSeq", "Slsman",
            "InvDate", "AddrName", "State"
        });

        var hdrQuery = new Dictionary<string, string>
        {
            ["props"] = hdrProps,
            ["filter"] = string.Join(" AND ", hdrFilters),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var hdrJson = await _csiRestClient.GetAsync("json/SLInvHdrs/adv", hdrQuery);
        var hdrResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(hdrJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        if (hdrResponse.MessageCode != 0)
            throw new InvalidOperationException(hdrResponse.Message);

        var headers = hdrResponse.Items
            .Select(row => MapRow<InvHdrInfo>(row))
            .ToList();

        if (headers.Count == 0)
        {
            await LogReportUsageAsync(repCode, "GetSalesReportDataUsingInvRepApi");
            return new List<Dictionary<string, object>>();
        }

        // ── 2. SLInvItemAlls (batched to avoid URL length limits) ──
        var invNums = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum))
            .Select(h => h.InvNum!)
            .Distinct()
            .ToList();

        var itemProps = string.Join(",", new[] { "InvNum", "InvSeq", "QtyInvoiced", "Price" });
        const int batchSize = 30;
        var invoiceItems = new List<InvItemInfo>();

        foreach (var batch in invNums.Chunk(batchSize))
        {
            var itemQuery = new Dictionary<string, string>
            {
                ["props"] = itemProps,
                ["filter"] = In("InvNum", batch),
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var itemJson = await _csiRestClient.GetAsync("json/SLInvItemAlls/adv", itemQuery);
            var itemResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(itemJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            if (itemResponse.MessageCode != 0)
                throw new InvalidOperationException(itemResponse.Message);

            invoiceItems.AddRange(itemResponse.Items.Select(row => MapRow<InvItemInfo>(row)));
        }

        // ── 3. SLCustAddrs (Name, City, State) ──
        var custNums = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.CustNum))
            .Select(h => h.CustNum!)
            .Distinct()
            .ToList();

        var addrProps = string.Join(",", new[]
        {
            "CustNum", "CustSeq", "Name", "City", "State"
        });

        var addrQuery = new Dictionary<string, string>
        {
            ["props"] = addrProps,
            ["filter"] = In("CustNum", custNums),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var addrJson = await _csiRestClient.GetAsync("json/SLCustAddrs/adv", addrQuery);
        var addrResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(addrJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var custAddrs = addrResponse.MessageCode == 0
            ? addrResponse.Items.Select(row => MapRow<CustAddrInfo>(row)).ToList()
            : new List<CustAddrInfo>();

        // Build lookups: (CustNum, CustSeq) → CustAddrInfo
        var custAddrLookup = custAddrs
            .Where(c => !string.IsNullOrWhiteSpace(c.CustNum))
            .GroupBy(c => (c.CustNum!, c.CustSeq))
            .ToDictionary(g => g.Key, g => g.First());

        // Bill-to lookup: CustNum → CustAddrInfo where CustSeq=0
        var billToLookup = custAddrs
            .Where(c => !string.IsNullOrWhiteSpace(c.CustNum) && c.CustSeq == 0)
            .GroupBy(c => c.CustNum!)
            .ToDictionary(g => g.Key, g => g.First());

        // ── 3b. SLCustomers (Uf_SalesRegion) ──
        var custRegionProps = string.Join(",", new[] { "CustNum", "CustSeq", "Uf_SalesRegion" });

        var custRegionQuery = new Dictionary<string, string>
        {
            ["props"] = custRegionProps,
            ["filter"] = In("CustNum", custNums),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var custRegionJson = await _csiRestClient.GetAsync("json/SLCustomers/adv", custRegionQuery);
        var custRegionResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(custRegionJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Build region lookup: (CustNum, CustSeq) → Uf_SalesRegion
        var regionLookup = new Dictionary<(string, int), string>();
        if (custRegionResponse.MessageCode == 0)
        {
            foreach (var row in custRegionResponse.Items)
            {
                var cn = row.FirstOrDefault(c =>
                    string.Equals(c.Name, "CustNum", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                var cs = row.FirstOrDefault(c =>
                    string.Equals(c.Name, "CustSeq", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? "0";
                var region = row.FirstOrDefault(c =>
                    string.Equals(c.Name, "Uf_SalesRegion", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(cn) && int.TryParse(cs, out int custSeq))
                {
                    regionLookup[(cn, custSeq)] = region ?? "";
                }
            }
        }

        // ── 4. Region name lookup ──
        using var connection = _dbConnectionFactory!.CreateBatConnection();
        var regionRows = await connection.QueryAsync<(string Region, string RegionName)>(
            "SELECT Region, RegionName FROM Chap_RegionNames WITH (NOLOCK)");
        var regionNameLookup = regionRows.ToDictionary(r => r.Region ?? "", r => r.RegionName ?? "", StringComparer.OrdinalIgnoreCase);

        // ── 5. LAW region filter ──
        HashSet<string>? allowedCustKeys = null;
        if (allowedRegions != null && allowedRegions.Any())
        {
            allowedCustKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in regionLookup)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value) && allowedRegions.Contains(kvp.Value))
                {
                    allowedCustKeys.Add($"{kvp.Key.Item1}|{kvp.Key.Item2}");
                }
            }
        }

        // ── 6. Build header lookup ──
        var hdrLookup = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum))
            .GroupBy(h => (h.InvNum!, h.InvSeq))
            .ToDictionary(g => g.Key, g => g.First());

        // ── 7. Join + aggregate ──
        var joined = new List<(string Customer, string CustomerName, int ShipToNum,
            string ShipToCity, string ShipToState, string Slsman, string Name,
            string BillToState, string UfSalesRegion, string RegionName, string Period,
            decimal ExtPrice)>();

        foreach (var item in invoiceItems)
        {
            if (string.IsNullOrWhiteSpace(item.InvNum))
                continue;

            var key = (item.InvNum!, item.InvSeq);
            if (!hdrLookup.TryGetValue(key, out InvHdrInfo? hdr))
                continue;

            if (hdr.InvDate == null || string.IsNullOrWhiteSpace(hdr.CustNum))
                continue;

            // LAW region filter
            if (allowedCustKeys != null)
            {
                var custKey = $"{hdr.CustNum}|{hdr.CustSeq}";
                if (!allowedCustKeys.Contains(custKey))
                    continue;
            }

            // Get customer address data from SLCustAddrs
            var custAddrKey = (hdr.CustNum!, hdr.CustSeq);
            custAddrLookup.TryGetValue(custAddrKey, out CustAddrInfo? shipToCust);
            billToLookup.TryGetValue(hdr.CustNum!, out CustAddrInfo? billToCust);

            // Get region from SLCustomers
            regionLookup.TryGetValue(custAddrKey, out string? ufSalesRegion);
            ufSalesRegion ??= "";
            string regionName = !string.IsNullOrWhiteSpace(ufSalesRegion) && regionNameLookup.TryGetValue(ufSalesRegion, out string? rn)
                ? rn : "";

            string period = hdr.InvDate.Value.ToString("MMM") + hdr.InvDate.Value.Year;
            decimal extPrice = item.QtyInvoiced * item.Price;

            joined.Add((
                Customer: hdr.CustNum!,
                CustomerName: billToCust?.Name ?? hdr.AddrName ?? "",
                ShipToNum: hdr.CustSeq,
                ShipToCity: shipToCust?.City ?? "",
                ShipToState: hdr.State ?? shipToCust?.State ?? "",
                Slsman: hdr.Slsman ?? repCode,
                Name: billToCust?.Name ?? hdr.AddrName ?? "",
                BillToState: billToCust?.State ?? "",
                UfSalesRegion: ufSalesRegion,
                RegionName: regionName,
                Period: period,
                ExtPrice: extPrice
            ));
        }

        // Group by fixed columns, sum ExtPrice per Period
        var grouped = joined
            .GroupBy(r => new
            {
                r.Customer, r.CustomerName, r.ShipToNum, r.ShipToCity, r.ShipToState,
                r.Slsman, r.Name, r.BillToState, r.UfSalesRegion, r.RegionName
            })
            .Select(g => new
            {
                g.Key,
                PeriodTotals = g.GroupBy(x => x.Period)
                    .ToDictionary(pg => pg.Key, pg => pg.Sum(x => x.ExtPrice))
            })
            .ToList();

        // ── 8. Build pivot dictionaries ──
        var fyMinus3Label = $"FY{fiscalYear - 3}";
        var fyMinus2Label = $"FY{fiscalYear - 2}";
        var fyMinus1Label = $"FY{fiscalYear - 1}";
        var fyCurrentLabel = $"FY{fiscalYear}";

        var results = new List<Dictionary<string, object>>();

        foreach (var g in grouped)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customer"] = g.Key.Customer,
                ["Customer Name"] = g.Key.CustomerName,
                ["Ship To Num"] = g.Key.ShipToNum,
                ["Ship To City"] = g.Key.ShipToCity,
                ["Ship To State"] = g.Key.ShipToState,
                ["slsman"] = g.Key.Slsman,
                ["name"] = g.Key.Name,
                ["Bill To State"] = g.Key.BillToState,
                ["Uf_SalesRegion"] = g.Key.UfSalesRegion,
                ["RegionName"] = g.Key.RegionName,
            };

            // FY totals
            decimal SumFy(List<string> months) =>
                months.Sum(m => g.PeriodTotals.TryGetValue(m, out decimal v) ? v : 0m);

            dict[fyMinus3Label] = SumFy(fyMinus3Months);
            dict[fyMinus2Label] = SumFy(fyMinus2Months);
            dict[fyMinus1Label] = SumFy(fyMinus1Months);
            dict[fyCurrentLabel] = SumFy(currentFYMonths);

            // Monthly columns for current FY only
            foreach (var month in currentFYMonths)
            {
                dict[month] = g.PeriodTotals.TryGetValue(month, out decimal v) ? v : 0m;
            }

            results.Add(dict);
        }

        // Order by FY-1 descending (matches SQL)
        results = results
            .OrderByDescending(d => d.TryGetValue(fyMinus1Label, out object? v) && v is decimal dec ? dec : 0m)
            .ToList();

        await LogReportUsageAsync(repCode, "GetSalesReportDataUsingInvRepApi");

        _logger?.LogInformation(
            "GetSalesReportDataUsingInvRepApiAsync returned {Count} rows for rep {RepCode}",
            results.Count, repCode);

        return results;
    }

    public async Task<List<Dictionary<string, object>>> GetSalesReportDataApiAsync()
    {
        EnsureRepContext();

        if (_csiRestClient == null)
            throw new InvalidOperationException("CSI REST client is not available.");

        string repCode = _repCodeContext!.CurrentRepCode;
        IEnumerable<string>? allowedRegions = repCode == "LAW"
            ? _repCodeContext.CurrentRegions
            : null;

        // ── Fiscal year boundaries ──
        var today = DateTime.Today;
        int fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;
        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyMinus3Start = new DateTime(fiscalYear - 4, 9, 1);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var allMonths = Enumerable.Range(0, 36 + currentFiscalMonth)
            .Select(i => fyMinus3Start.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        var fyMinus3Months = allMonths.Take(12).ToList();
        var fyMinus2Months = allMonths.Skip(12).Take(12).ToList();
        var fyMinus1Months = allMonths.Skip(24).Take(12).ToList();
        var currentFYMonths = allMonths.Skip(36).Take(currentFiscalMonth).ToList();

        // ── 1. SLCustomers — find customers where Slsman matches repCode (shared across sites) ──
        var custFilters = new List<string> { Eq("Slsman", repCode) };
        var custProps = string.Join(",", new[]
        {
            "CustNum", "CustSeq", "Slsman", "Uf_SalesRegion"
        });

        var custQuery = new Dictionary<string, string>
        {
            ["props"] = custProps,
            ["filter"] = string.Join(" AND ", custFilters),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var custJson = await _csiRestClient.GetAsync("json/SLCustomers/adv", custQuery);
        var custResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(custJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        if (custResponse.MessageCode != 0)
            throw new InvalidOperationException(custResponse.Message);

        // Parse customer rows to get CustNum, CustSeq, Slsman, Uf_SalesRegion
        // Note: do NOT trim CustNum — the API requires the exact value for IN filters
        var customers = custResponse.Items.Select(row =>
        {
            var cn = row.FirstOrDefault(c =>
                string.Equals(c.Name, "CustNum", StringComparison.OrdinalIgnoreCase))?.Value;
            var cs = row.FirstOrDefault(c =>
                string.Equals(c.Name, "CustSeq", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? "0";
            var slsman = row.FirstOrDefault(c =>
                string.Equals(c.Name, "Slsman", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
            var region = row.FirstOrDefault(c =>
                string.Equals(c.Name, "Uf_SalesRegion", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
            int.TryParse(cs, out int custSeq);
            return new { CustNum = cn, CustSeq = custSeq, Slsman = slsman ?? repCode, UfSalesRegion = region ?? "" };
        })
        .Where(c => !string.IsNullOrWhiteSpace(c.CustNum))
        .ToList();

        if (customers.Count == 0)
        {
            await LogReportUsageAsync(repCode, "GetSalesReportDataApi");
            return new List<Dictionary<string, object>>();
        }

        // Build lookups from customer data
        var custSlsmanLookup = customers
            .GroupBy(c => (c.CustNum!, c.CustSeq))
            .ToDictionary(g => g.Key, g => g.First().Slsman);

        var regionLookup = customers
            .GroupBy(c => (c.CustNum!, c.CustSeq))
            .ToDictionary(g => g.Key, g => g.First().UfSalesRegion);

        // LAW region filter
        HashSet<string>? allowedCustKeys = null;
        if (allowedRegions != null && allowedRegions.Any())
        {
            allowedCustKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in customers)
            {
                if (!string.IsNullOrWhiteSpace(c.UfSalesRegion) && allowedRegions.Contains(c.UfSalesRegion))
                {
                    allowedCustKeys.Add($"{c.CustNum}|{c.CustSeq}");
                }
            }
        }

        var custNums = customers
            .Select(c => c.CustNum!)
            .Distinct()
            .ToList();

        // ── 2. SLInvHdrs — get invoices for these customers from both BAT + KENT sites ──
        var hdrProps = string.Join(",", new[]
        {
            "InvNum", "InvSeq", "CustNum", "CustSeq",
            "InvDate", "AddrName", "State"
        });

        const int custBatchSize = 30;

        // Build the list of auth tokens to query: BAT (default) + KENT (if configured)
        var siteAuths = new List<(string Site, string? AuthOverride)> { ("BAT", null) };
        if (!string.IsNullOrWhiteSpace(_csiOptions.KentAuthorization))
            siteAuths.Add(("KENT", _csiOptions.KentAuthorization));

        // Tag each header/item with a site prefix so InvNum collisions between sites don't merge
        var headers = new List<(string Site, InvHdrInfo Hdr)>();

        foreach (var (site, authOverride) in siteAuths)
        {
            foreach (var batch in custNums.Chunk(custBatchSize))
            {
                var hdrQuery = new Dictionary<string, string>
                {
                    ["props"] = hdrProps,
                    ["filter"] = In("CustNum", batch) + $" AND InvDate >= '{fyMinus3Start:yyyyMMdd}'",
                    ["rowcap"] = "0",
                    ["loadtype"] = "FIRST",
                    ["bookmark"] = "0",
                    ["readonly"] = "1"
                };

                string hdrJson = authOverride != null
                    ? await _csiRestClient.GetAsync("json/SLInvHdrs/adv", hdrQuery, authOverride)
                    : await _csiRestClient.GetAsync("json/SLInvHdrs/adv", hdrQuery);

                var hdrResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(hdrJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

                if (hdrResponse.MessageCode != 0)
                    throw new InvalidOperationException(hdrResponse.Message);

                headers.AddRange(hdrResponse.Items.Select(row => (site, MapRow<InvHdrInfo>(row))));
            }
        }

        if (headers.Count == 0)
        {
            await LogReportUsageAsync(repCode, "GetSalesReportDataApi");
            return new List<Dictionary<string, object>>();
        }

        // ── 3. SLInvItems — batched, both BAT + KENT sites ──
        var itemProps = string.Join(",", new[] { "InvNum", "InvSeq", "QtyInvoiced", "Price" });
        const int batchSize = 30;
        var invoiceItems = new List<(string Site, InvItemInfo Item)>();

        foreach (var (site, authOverride) in siteAuths)
        {
            var siteInvNums = headers
                .Where(h => h.Site == site && !string.IsNullOrWhiteSpace(h.Hdr.InvNum))
                .Select(h => h.Hdr.InvNum!)
                .Distinct()
                .ToList();

            foreach (var batch in siteInvNums.Chunk(batchSize))
            {
                var itemQuery = new Dictionary<string, string>
                {
                    ["props"] = itemProps,
                    ["filter"] = In("InvNum", batch),
                    ["rowcap"] = "0",
                    ["loadtype"] = "FIRST",
                    ["bookmark"] = "0",
                    ["readonly"] = "1"
                };

                string itemJson = authOverride != null
                    ? await _csiRestClient.GetAsync("json/SLInvItemAlls/adv", itemQuery, authOverride)
                    : await _csiRestClient.GetAsync("json/SLInvItemAlls/adv", itemQuery);

                var itemResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(itemJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

                if (itemResponse.MessageCode != 0)
                    throw new InvalidOperationException(itemResponse.Message);

                invoiceItems.AddRange(itemResponse.Items.Select(row => (site, MapRow<InvItemInfo>(row))));
            }
        }

        // ── 4. SLCustAddrs (Name, City, State) — batched (shared across sites) ──
        var addrProps = string.Join(",", new[]
        {
            "CustNum", "CustSeq", "Name", "City", "State"
        });

        var custAddrs = new List<CustAddrInfo>();

        foreach (var batch in custNums.Chunk(custBatchSize))
        {
            var addrQuery = new Dictionary<string, string>
            {
                ["props"] = addrProps,
                ["filter"] = In("CustNum", batch),
                ["rowcap"] = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var addrJson = await _csiRestClient.GetAsync("json/SLCustAddrs/adv", addrQuery);
            var addrResponse = JsonSerializer.Deserialize<MgRestAdvResponse>(addrJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            if (addrResponse.MessageCode == 0)
                custAddrs.AddRange(addrResponse.Items.Select(row => MapRow<CustAddrInfo>(row)));
        }

        // Build lookups: (CustNum, CustSeq) → CustAddrInfo
        var custAddrLookup = custAddrs
            .Where(c => !string.IsNullOrWhiteSpace(c.CustNum))
            .GroupBy(c => (c.CustNum!, c.CustSeq))
            .ToDictionary(g => g.Key, g => g.First());

        // Bill-to lookup: CustNum → CustAddrInfo where CustSeq=0
        var billToLookup = custAddrs
            .Where(c => !string.IsNullOrWhiteSpace(c.CustNum) && c.CustSeq == 0)
            .GroupBy(c => c.CustNum!)
            .ToDictionary(g => g.Key, g => g.First());

        // ── 5. Region name lookup ──
        using var connection = _dbConnectionFactory!.CreateBatConnection();
        var regionRows = await connection.QueryAsync<(string Region, string RegionName)>(
            "SELECT Region, RegionName FROM Chap_RegionNames WITH (NOLOCK)");
        var regionNameLookup = regionRows.ToDictionary(r => r.Region ?? "", r => r.RegionName ?? "", StringComparer.OrdinalIgnoreCase);

        // ── 6. Build header lookup — keyed by (Site, InvNum, InvSeq) to avoid cross-site collisions ──
        var hdrLookup = headers
            .Where(h => !string.IsNullOrWhiteSpace(h.Hdr.InvNum))
            .GroupBy(h => (h.Site, h.Hdr.InvNum!, h.Hdr.InvSeq))
            .ToDictionary(g => g.Key, g => g.First().Hdr);

        // ── 7. Join + aggregate ──
        var joined = new List<(string Customer, string CustomerName, int ShipToNum,
            string ShipToCity, string ShipToState, string Slsman, string Name,
            string BillToState, string UfSalesRegion, string RegionName, string Period,
            decimal ExtPrice)>();

        foreach (var (site, item) in invoiceItems)
        {
            if (string.IsNullOrWhiteSpace(item.InvNum))
                continue;

            var key = (site, item.InvNum!, item.InvSeq);
            if (!hdrLookup.TryGetValue(key, out InvHdrInfo? hdr))
                continue;

            if (hdr.InvDate == null || string.IsNullOrWhiteSpace(hdr.CustNum))
                continue;

            // LAW region filter
            if (allowedCustKeys != null)
            {
                var custKey = $"{hdr.CustNum}|{hdr.CustSeq}";
                if (!allowedCustKeys.Contains(custKey))
                    continue;
            }

            // Get customer address data from SLCustAddrs
            var custAddrKey = (hdr.CustNum!, hdr.CustSeq);
            custAddrLookup.TryGetValue(custAddrKey, out CustAddrInfo? shipToCust);
            billToLookup.TryGetValue(hdr.CustNum!, out CustAddrInfo? billToCust);

            // Get region from SLCustomers (already fetched in step 1)
            regionLookup.TryGetValue(custAddrKey, out string? ufSalesRegion);
            ufSalesRegion ??= "";
            string regionName = !string.IsNullOrWhiteSpace(ufSalesRegion) && regionNameLookup.TryGetValue(ufSalesRegion, out string? rn)
                ? rn : "";

            // Use slsman from customer record (cu.slsman), not invoice header
            custSlsmanLookup.TryGetValue(custAddrKey, out string? slsman);
            slsman ??= repCode;

            string period = hdr.InvDate.Value.ToString("MMM") + hdr.InvDate.Value.Year;
            decimal extPrice = item.QtyInvoiced * item.Price;

            joined.Add((
                Customer: hdr.CustNum!,
                CustomerName: billToCust?.Name ?? hdr.AddrName ?? "",
                ShipToNum: hdr.CustSeq,
                ShipToCity: shipToCust?.City ?? "",
                ShipToState: hdr.State ?? shipToCust?.State ?? "",
                Slsman: slsman,
                Name: billToCust?.Name ?? hdr.AddrName ?? "",
                BillToState: billToCust?.State ?? "",
                UfSalesRegion: ufSalesRegion,
                RegionName: regionName,
                Period: period,
                ExtPrice: extPrice
            ));
        }

        // Group by fixed columns, sum ExtPrice per Period
        var grouped = joined
            .GroupBy(r => new
            {
                r.Customer, r.CustomerName, r.ShipToNum, r.ShipToCity, r.ShipToState,
                r.Slsman, r.Name, r.BillToState, r.UfSalesRegion, r.RegionName
            })
            .Select(g => new
            {
                g.Key,
                PeriodTotals = g.GroupBy(x => x.Period)
                    .ToDictionary(pg => pg.Key, pg => pg.Sum(x => x.ExtPrice))
            })
            .ToList();

        // ── 8. Build pivot dictionaries ──
        var fyMinus3Label = $"FY{fiscalYear - 3}";
        var fyMinus2Label = $"FY{fiscalYear - 2}";
        var fyMinus1Label = $"FY{fiscalYear - 1}";
        var fyCurrentLabel = $"FY{fiscalYear}";

        var results = new List<Dictionary<string, object>>();

        foreach (var g in grouped)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customer"] = g.Key.Customer,
                ["Customer Name"] = g.Key.CustomerName,
                ["Ship To Num"] = g.Key.ShipToNum,
                ["Ship To City"] = g.Key.ShipToCity,
                ["Ship To State"] = g.Key.ShipToState,
                ["slsman"] = g.Key.Slsman,
                ["name"] = g.Key.Name,
                ["Bill To State"] = g.Key.BillToState,
                ["Uf_SalesRegion"] = g.Key.UfSalesRegion,
                ["RegionName"] = g.Key.RegionName,
            };

            // FY totals
            decimal SumFy(List<string> months) =>
                months.Sum(m => g.PeriodTotals.TryGetValue(m, out decimal v) ? v : 0m);

            dict[fyMinus3Label] = SumFy(fyMinus3Months);
            dict[fyMinus2Label] = SumFy(fyMinus2Months);
            dict[fyMinus1Label] = SumFy(fyMinus1Months);
            dict[fyCurrentLabel] = SumFy(currentFYMonths);

            // Monthly columns for current FY only
            foreach (var month in currentFYMonths)
            {
                dict[month] = g.PeriodTotals.TryGetValue(month, out decimal v) ? v : 0m;
            }

            results.Add(dict);
        }

        // Order by FY-1 descending (matches SQL)
        results = results
            .OrderByDescending(d => d.TryGetValue(fyMinus1Label, out object? v) && v is decimal dec ? dec : 0m)
            .ToList();

        await LogReportUsageAsync(repCode, "GetSalesReportDataApi");

        _logger?.LogInformation(
            "GetSalesReportDataApiAsync returned {Count} rows for rep {RepCode}",
            results.Count, repCode);

        return results;
    }

    // ----------------------
    // Private helpers
    // ----------------------

    private (string query, int fiscalYear) BuildSalesPivotQuery(IEnumerable<string>? allowedRegions = null)
    {
        var today = DateTime.Today;

        // Fiscal year rolls on Sep 1 -> Aug 31 (FY = next calendar year once we hit Sep)
        var fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;

        // current FY start (Sep 1 of prior year) and 3 FYs ago
        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyMinus3Start = new DateTime(fiscalYear - 4, 9, 1);
        var fyCurrentEnd = new DateTime(fiscalYear, 8, 31);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var monthsCount = 36 + currentFiscalMonth;
        var allMonths = Enumerable.Range(0, monthsCount)
            .Select(i => fyMinus3Start.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        var fyMinus3Months = allMonths.Take(12).ToList();
        var fyMinus2Months = allMonths.Skip(12).Take(12).ToList();
        var fyMinus1Months = allMonths.Skip(24).Take(12).ToList();
        var currentFYMonths = allMonths.Skip(36).Take(currentFiscalMonth).ToList();

        var colsPivot = string.Join(",", allMonths.Select(m => $"[{m}]"));
        var currentFYColList = string.Join(",", currentFYMonths.Select(m => $"ISNULL([{m}], 0) AS [{m}]"));

        var fyMinus3Sum = string.Join(" + ", fyMinus3Months.Select(m => $"ISNULL([{m}],0)"));
        var fyMinus2Sum = string.Join(" + ", fyMinus2Months.Select(m => $"ISNULL([{m}],0)"));
        var fyMinus1Sum = string.Join(" + ", fyMinus1Months.Select(m => $"ISNULL([{m}],0)"));
        var fyCurrentSum = currentFYMonths.Any()
            ? string.Join(" + ", currentFYMonths.Select(m => $"ISNULL([{m}],0)"))
            : "0";

        var regionFilter = (allowedRegions != null && allowedRegions.Any())
            ? " AND cu.Uf_SalesRegion IN @AllowedRegions"
            : string.Empty;

        string BaseSelect(string db) => $@"
    SELECT 
        ih.cust_num AS Customer,
        ca0.Name AS [Customer Name],
        ih.cust_seq AS [Ship To Num],
        ca.City AS [Ship To City],
        ca.State AS [Ship To State],
        cu.slsman,
        ca0.name,
        ca0.state AS [Bill To State],
        cu.Uf_SalesRegion,
        rn.RegionName,
        FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR) AS Period,
        ISNULL(SUM(ii.qty_invoiced * ii.price), 0) AS ExtPrice
    FROM {db}.dbo.inv_item_mst ii 
    JOIN {db}.dbo.inv_hdr_mst ih ON ii.inv_num = ih.inv_num AND ii.inv_seq = ih.inv_seq
    JOIN Bat_App.dbo.custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0 
    JOIN Bat_App.dbo.custaddr_mst ca ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
    JOIN Bat_App.dbo.customer_mst cu ON ih.cust_num = cu.cust_num AND cu.cust_seq = ih.cust_seq
    LEFT JOIN Bat_App.dbo.Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
    WHERE ih.inv_date >= '{fyMinus3Start:yyyy-MM-dd}'
      AND cu.slsman = @RepCode{regionFilter}
    GROUP BY 
        ih.cust_num, ca0.Name, ih.cust_seq, ca.City, ca.State,
        ca0.name, ca0.state, cu.Uf_SalesRegion, rn.RegionName,
        cu.slsman,
        FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR)";

        var query = $@"
SELECT 
    Customer,
    [Customer Name],
    [Ship To Num],
    [Ship To City],
    [Ship To State],
    slsman,
    name,
    [Bill To State],
    Uf_SalesRegion,
    RegionName,
    {fyMinus3Sum} AS FY{fiscalYear - 3},
    {fyMinus2Sum} AS FY{fiscalYear - 2},
    {fyMinus1Sum} AS FY{fiscalYear - 1},
    {fyCurrentSum} AS FY{fiscalYear},
    {currentFYColList}
FROM (
    {BaseSelect("Bat_App")}
    UNION ALL
    {BaseSelect("Kent_App")}
) AS src
PIVOT (
    SUM(ExtPrice)
    FOR Period IN ({colsPivot})
) AS pvt
ORDER BY FY{fiscalYear - 1} DESC;";

        return (query, fiscalYear);
    }

    private string BuildItemsMonthlyQuery(IEnumerable<string>? allowedRegions)
    {
        var today = DateTime.Today;
        int fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;

        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyPriorStart = fyCurrentStart.AddYears(-1);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var allMonths = Enumerable.Range(0, 24)
            .Select(i => fyPriorStart.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        var currentFYMonths = allMonths.Skip(12).Take(currentFiscalMonth).ToList();
        var priorFYMonths = allMonths.Take(12).ToList();

        var colsPivot = string.Join(",", allMonths.Select(m => $"[{m}]"));
        var currentFYColList = string.Join(",", currentFYMonths.Select(m => $"ISNULL([{m}], 0) AS [{m}]"));
        var fyPriorSum = string.Join(" + ", priorFYMonths.Select(m => $"ISNULL([{m}],0)"));
        var fyCurrentSum = string.Join(" + ", currentFYMonths.Select(m => $"ISNULL([{m}],0)"));

        var regionFilter = (allowedRegions != null && allowedRegions.Any())
            ? " AND cu.Uf_SalesRegion IN @AllowedRegions"
            : string.Empty;

        string BaseSelect(string db) => $@"
        SELECT 
            ih.cust_num AS Customer,
            ca0.Name AS [Customer Name],
            ih.cust_seq AS [Ship To Num],
            ca.City AS [Ship To City],
            ca.State AS [Ship To State],
            cu.slsman,
            ca0.name,
            ca0.state AS [Bill To State],
            cu.Uf_SalesRegion,
            rn.RegionName,
            ii.item AS Item,
            im.Description AS ItemDescription,
            FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR) AS Period,
            ISNULL(SUM(ii.qty_invoiced * (ii.price * ((100 - ISNULL(ih.disc, 0.0)) / 100))), 0) AS ExtPrice
        FROM {db}.dbo.inv_item_mst ii 
        JOIN {db}.dbo.inv_hdr_mst ih ON ii.inv_num = ih.inv_num AND ii.inv_seq = ih.inv_seq
        JOIN Bat_App.dbo.custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0 
        JOIN Bat_App.dbo.custaddr_mst ca ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
        JOIN Bat_App.dbo.customer_mst cu ON ih.cust_num = cu.cust_num AND cu.cust_seq = ih.cust_seq
        LEFT JOIN Bat_App.dbo.Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
        LEFT JOIN Bat_App.dbo.Item_mst im ON ii.item = im.item
        WHERE ih.inv_date >= '{fyPriorStart:yyyy-MM-dd}'
          AND cu.slsman = @RepCode{regionFilter}
        GROUP BY 
            ih.cust_num, ca0.Name, ih.cust_seq, ca.City, ca.State,
            ca0.name, ca0.state, cu.Uf_SalesRegion, rn.RegionName,
            cu.slsman, ii.item, im.Description,
            FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR)";

        return $@"
    SELECT 
        Customer,
        [Customer Name],
        [Ship To Num],
        [Ship To City],
        [Ship To State],
        slsman,
        name,
        [Bill To State],
        Uf_SalesRegion,
        RegionName,
        Item,
        ItemDescription,
        {fyPriorSum} AS FY{fiscalYear - 1},
        {fyCurrentSum} AS FY{fiscalYear},
        {currentFYColList}
    FROM (
        {BaseSelect("Bat_App")}
        UNION ALL
        {BaseSelect("Kent_App")}
    ) AS src
    PIVOT (
        SUM(ExtPrice)
        FOR Period IN ({colsPivot})
    ) AS pvt
    ORDER BY FY{fiscalYear - 1} DESC;";
    }

    private static List<Dictionary<string, object>> MaterializeToDictionaries(IEnumerable<dynamic> rows)
    {
        var data = new List<Dictionary<string, object>>();
        foreach (var row in rows)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in (IDictionary<string, object>)row)
                dict[kvp.Key] = kvp.Value;
            data.Add(dict);
        }
        return data;
    }

    private void EnsureAuth()
    {
        if (_authenticationStateProvider == null)
            throw new InvalidOperationException("AuthenticationStateProvider is required for this operation.");
    }

    private void EnsureRepContext()
    {
        if (_repCodeContext == null)
            throw new InvalidOperationException("IRepCodeContext is required for this operation.");
    }


}
