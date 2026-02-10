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

        var rows = await connection.QueryAsync(sql, param, commandType: CommandType.Text);
        return MaterializeToDictionaries(rows);
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

        // ── SLCoShips call ──
        var slFilters = new List<string> { Eq("CoSlsman", repCode) };
        if (parameters.BeginShipDate != default)
            slFilters.Add($"ShipDate >= '{parameters.BeginShipDate:yyyyMMdd}'");
        if (parameters.EndShipDate != default)
            slFilters.Add($"ShipDate <= '{parameters.EndShipDate:yyyyMMdd}'");
        if (allowedRegions != null && allowedRegions.Any())
            slFilters.Add(In("SalesRegion", allowedRegions));

        var slProps = string.Join(",", new[]
        {
            "CoCustNum", "CadrName", "CoCustPo", "CoNum", "CoLine",
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

        var shipments = slResponse.Items
            .Select(row => MapRow<CustomerShipment>(row))
            .ToList();

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



    private static object ConvertTo(string raw, Type targetType)
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

        using var connection = new SqlConnection(_connectionString);

        parameters.RepCode = _repCodeContext!.CurrentRepCode;
        parameters.AllowedRegions = parameters.RepCode == "LAW"
            ? _repCodeContext.CurrentRegions?.ToList() ?? new List<string>()
            : new List<string>();

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
      (ii.qty_invoiced * ii.price) AS SalesAmount,
      fc.MonthShort, fc.DayOfMonth, fc.DayShort, fc.FiscalYear, fc.QuarterOfFiscalYear, fc.MonthOfFiscalYear
      ,rn.RegionName
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
