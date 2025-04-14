using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.SqlClient;
using RepPortal.Models;
using System.Data;
using RepPortal.Data;
using System.Text;


namespace RepPortal.Services;

public class SalesService
{
    private readonly string _connectionString;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IRepCodeContext _repCodeContext;
    private readonly DbConnectionFactory _dbConnectionFactory;
    private readonly ILogger<SalesService> _logger;



    public SalesService(IConfiguration configuration, AuthenticationStateProvider authenticationStateProvider, 
        IRepCodeContext repCodeContext, DbConnectionFactory dbConnectionFactory, ILogger<SalesService> logger)
    {
        _connectionString = configuration.GetConnectionString("BatAppConnection");
        _authenticationStateProvider = authenticationStateProvider;
        _repCodeContext = repCodeContext;
        _dbConnectionFactory = dbConnectionFactory;
        _logger = logger;

    }

    public async Task<string> GetRepIDAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        return user?.FindFirst("RepID")?.Value;
    }

    public string GetCurrentRepCode()
    {
        return _repCodeContext.CurrentRepCode;
    }



    public async Task<List<Dictionary<string, object>>> GetSalesReportData()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        //var repCode = user?.FindFirst("RepCode")?.Value;
        var repCode = _repCodeContext.CurrentRepCode;

        using (var connection = new SqlConnection(_connectionString))
        {
            int fiscalYear;  // it's being set by the GetDynamicQuery method

            await connection.OpenAsync();
            var query = GetDynamicQuery(out fiscalYear);
            var parameters = new { RepCode = repCode };
            var results = await connection.QueryAsync(query, parameters, commandType: CommandType.Text);

            Console.WriteLine($"Fiscal year used: {fiscalYear}");
            // Convert the results to a list of dictionaries
            var data = new List<Dictionary<string, object>>();
            foreach (var row in results)
            {
                var dict = new Dictionary<string, object>();
                foreach (var prop in row)
                {
                    dict[prop.Key] = prop.Value;
                }
                data.Add(dict);
            }

            return data;
        }

    }

    string GetDynamicQuery(out int fiscalYear)
    {
        var today = DateTime.Today;
        fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;

        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyCurrentEnd = new DateTime(fiscalYear, 8, 31);
        var fyPriorStart = fyCurrentStart.AddYears(-1);
        var fyPriorEnd = fyCurrentEnd.AddYears(-1);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        var monthNames = Enumerable.Range(1, currentFiscalMonth)
            .Select(i => fyCurrentStart.AddMonths(i - 1))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        var cols = string.Join(",", monthNames.Select(m => $"ISNULL([{m}], 0) AS [{m}]"));
        var colsPivot = string.Join(",", monthNames.Select(m => $"[{m}]"));

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
    ISNULL(FY{fiscalYear - 1}, 0) AS FY{fiscalYear - 1}, 
    ISNULL(FY{fiscalYear}, 0) AS FY{fiscalYear}, 
    {cols}
FROM (
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
        CASE 
            WHEN ih.inv_date BETWEEN '{fyPriorStart:yyyy-MM-dd}' AND '{fyPriorEnd:yyyy-MM-dd}' THEN 'FY{fiscalYear - 1}'
            ELSE FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR)
        END AS Period,
        ISNULL(SUM(ii.qty_invoiced * ii.price),0) AS ExtPrice
    FROM inv_item_mst ii 
    JOIN inv_hdr_mst ih ON ii.inv_num = ih.inv_num
    JOIN custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
    JOIN custaddr_mst ca ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
    JOIN customer_mst cu ON ih.cust_num = cu.cust_num AND cu.cust_seq = ih.cust_seq
    LEFT JOIN Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
    WHERE ih.inv_date >= '{fyPriorStart:yyyy-MM-dd}' 
        AND cu.slsman = @RepCode
    GROUP BY 
        ih.cust_num, 
        ca0.Name, 
        ih.cust_seq, 
        ca.City,
        ca.State,
        ca0.name, 
        ca0.state, 
        cu.Uf_SalesRegion, 
        rn.RegionName,
        cu.slsman,
        CASE 
            WHEN ih.inv_date BETWEEN '{fyPriorStart:yyyy-MM-dd}' AND '{fyPriorEnd:yyyy-MM-dd}' THEN 'FY{fiscalYear - 1}'
            ELSE FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR)
        END

    UNION ALL

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
        'FY{fiscalYear}' AS Period,
        ISNULL(SUM(ii.qty_invoiced * ii.price),0) AS ExtPrice
    FROM inv_item_mst ii 
    JOIN inv_hdr_mst ih ON ii.inv_num = ih.inv_num
    JOIN custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
    JOIN custaddr_mst ca ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
    JOIN customer_mst cu ON ih.cust_num = cu.cust_num AND cu.cust_seq = ih.cust_seq
    LEFT JOIN Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
    WHERE ih.inv_date >= '{fyCurrentStart:yyyy-MM-dd}' 
        AND cu.slsman = @RepCode
    GROUP BY 
        ih.cust_num, 
        ca0.Name, 
        ih.cust_seq, 
        ca.City,
        ca.State,
        ca0.name, 
        ca0.state, 
        cu.Uf_SalesRegion, 
        rn.RegionName,
        cu.slsman
) AS src
PIVOT (
    SUM(ExtPrice)
    FOR Period IN (FY{fiscalYear - 1}, FY{fiscalYear}, {colsPivot})
) AS pvt
ORDER BY FY{fiscalYear - 1} DESC;";

        return query;
    }



    public async Task<List<Dictionary<string, object>>> GetItemSalesReportData()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        //var repCode = user?.FindFirst("RepCode")?.Value;
        var repCode = _repCodeContext.CurrentRepCode;

        using (var connection = new SqlConnection(_connectionString))
        {
            int fiscalYear; // it's being set by the GetDynamicQuery method

            await connection.OpenAsync();
            var query = GetDynamicQueryForItemsMonthly(out fiscalYear);
            var parameters = new { RepCode = repCode };
            _logger.LogInformation($"{query}");
            var results = await connection.QueryAsync(query, parameters, commandType: CommandType.Text);

            Console.WriteLine($"Fiscal year used: {fiscalYear}");
            // Convert the results to a list of dictionaries
            var data = new List<Dictionary<string, object>>();
            foreach (var row in results)
            {
                var dict = new Dictionary<string, object>();
                foreach (var prop in row)
                {
                    dict[prop.Key] = prop.Value;
                }

                data.Add(dict);
            }

            return data;
        }



        string GetDynamicQueryForItemsMonthly(out int fiscalYear)
        {
            var today = DateTime.Today;
            fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;

            var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
            var fyCurrentEnd = new DateTime(fiscalYear, 8, 31);
            var fyPriorStart = fyCurrentStart.AddYears(-1);
            var fyPriorEnd = fyCurrentEnd.AddYears(-1);

            int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

            var monthNames = Enumerable.Range(1, currentFiscalMonth)
                .Select(i => fyCurrentStart.AddMonths(i - 1))
                .Select(d => d.ToString("MMM") + d.Year)
                .ToList();

            var cols = string.Join(",", monthNames.Select(m => $"ISNULL([{m}], 0) AS [{m}]"));
            var colsPivot = string.Join(",", monthNames.Select(m => $"[{m}]"));

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
    Item,
    ItemDescription,
    ISNULL(FY{fiscalYear - 1}, 0) AS FY{fiscalYear - 1}, 
    ISNULL(FY{fiscalYear}, 0) AS FY{fiscalYear}, 
    {cols}
FROM (
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
        CASE 
            WHEN ih.inv_date BETWEEN '{fyPriorStart:yyyy-MM-dd}' AND '{fyPriorEnd:yyyy-MM-dd}' THEN 'FY{fiscalYear - 1}'
            ELSE FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR)
        END AS Period,
        ISNULL(SUM(ii.qty_invoiced * ii.price),0) AS ExtPrice
    FROM inv_item_mst ii 
    JOIN inv_hdr_mst ih ON ii.inv_num = ih.inv_num
    JOIN custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
    JOIN custaddr_mst ca ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
    JOIN customer_mst cu ON ih.cust_num = cu.cust_num AND cu.cust_seq = ih.cust_seq
    LEFT JOIN Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
    LEFT JOIN Bat_App.dbo.Item_mst im ON ii.item = im.item
    WHERE ih.inv_date >= '{fyPriorStart:yyyy-MM-dd}' 
        AND cu.slsman = @RepCode
    GROUP BY 
        ih.cust_num, 
        ca0.Name, 
        ih.cust_seq, 
        ca.City,
        ca.State,
        ca0.name, 
        ca0.state, 
        cu.Uf_SalesRegion, 
        rn.RegionName,
        cu.slsman,
        ii.item,
        im.Description,
        CASE 
            WHEN ih.inv_date BETWEEN '{fyPriorStart:yyyy-MM-dd}' AND '{fyPriorEnd:yyyy-MM-dd}' THEN 'FY{fiscalYear - 1}'
            ELSE FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR)
        END

    UNION ALL

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
        'FY{fiscalYear}' AS Period,
        ISNULL(SUM(ii.qty_invoiced * ii.price),0) AS ExtPrice
    FROM inv_item_mst ii 
    JOIN inv_hdr_mst ih ON ii.inv_num = ih.inv_num
    JOIN custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
    JOIN custaddr_mst ca ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
    JOIN customer_mst cu ON ih.cust_num = cu.cust_num AND cu.cust_seq = ih.cust_seq
    LEFT JOIN Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
    LEFT JOIN Bat_App.dbo.Item_mst im ON ii.item = im.item
    WHERE ih.inv_date >= '{fyCurrentStart:yyyy-MM-dd}' 
        AND cu.slsman = @RepCode
    GROUP BY 
        ih.cust_num, 
        ca0.Name, 
        ih.cust_seq, 
        ca.City,
        ca.State,
        ca0.name, 
        ca0.state, 
        cu.Uf_SalesRegion, 
        rn.RegionName,
        cu.slsman,
        ii.item,
        im.Description
) AS src
PIVOT (
    SUM(ExtPrice)
    FOR Period IN (FY{fiscalYear - 1}, FY{fiscalYear}, {colsPivot})
) AS pvt
ORDER BY FY{fiscalYear - 1} DESC;";

            return query;
        }



    }




    public async Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQty()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        //var repCode = user?.FindFirst("RepCode")?.Value;
        var repCode = _repCodeContext.CurrentRepCode;

        using (var connection = new SqlConnection(_connectionString))
        {
            int fiscalYear; // it's being set by the GetDynamicQuery method

            await connection.OpenAsync();
            var query = GetDynamicQueryForItemsMonthlyWithQty(out fiscalYear);
            var parameters = new { RepCode = repCode };
            _logger.LogInformation($"The query is: {query}");
            var results = await connection.QueryAsync(query, parameters, commandType: CommandType.Text);

            Console.WriteLine($"Fiscal year used: {fiscalYear}");
            // Convert the results to a list of dictionaries
            var data = new List<Dictionary<string, object>>();
            foreach (var row in results)
            {
                var dict = new Dictionary<string, object>();
                foreach (var prop in row)
                {
                    dict[prop.Key] = prop.Value;
                }

                data.Add(dict);
            }

            return data;
        }
    }




    public string GetDynamicQueryForItemsMonthlyWithQty(out int fiscalYear)
    {
        var today = DateTime.Today;
        fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;

        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyCurrentEnd = new DateTime(fiscalYear, 8, 31);
        var fyPriorStart = fyCurrentStart.AddYears(-1);
        var fyPriorEnd = fyCurrentEnd.AddYears(-1);

        // Determine the number of months into the current fiscal year
        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        // Generate month names for current fiscal year up to current month
        var monthNames = Enumerable.Range(1, currentFiscalMonth)
            .Select(i => fyCurrentStart.AddMonths(i - 1))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        // Generate month columns dynamically
        var monthColumns = new StringBuilder();
        foreach (var monthName in monthNames)
        {
            string safeMonthName = monthName.Replace("'", "''");
            monthColumns.AppendLine($@"
            MAX(CASE WHEN Period = '{safeMonthName}' THEN RevAmount ELSE 0 END) AS [{safeMonthName}_Rev],
            MAX(CASE WHEN Period = '{safeMonthName}' THEN QtyInvoiced ELSE 0 END) AS [{safeMonthName}_Qty],");
        }

        if (monthColumns.Length > 0)
        {
            // Find the last index of the comma
            int lastCommaIndex = monthColumns.ToString().LastIndexOf(',');
            if (lastCommaIndex != -1)
            {
                monthColumns.Remove(lastCommaIndex, 1);
            }
        }

        // Build optimized query with CTE and fewer CASE expressions
            var query = $@"
-- Use WITH statement for improved readability and performance
WITH InvoiceData AS (
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
        -- Create single Period column with appropriate fiscal markers
        CASE
            WHEN ih.inv_date BETWEEN '{fyPriorStart:yyyy-MM-dd}' AND '{fyPriorEnd:yyyy-MM-dd}' 
                THEN 'FY{fiscalYear - 1}'
            WHEN ih.inv_date BETWEEN '{fyCurrentStart:yyyy-MM-dd}' AND '{fyCurrentEnd:yyyy-MM-dd}' 
                THEN FORMAT(ih.inv_date, 'MMM') + CAST(YEAR(ih.inv_date) AS VARCHAR)
        END AS Period,
        -- Pre-calculate fiscal year column for easier aggregation
        CASE
            WHEN ih.inv_date BETWEEN '{fyPriorStart:yyyy-MM-dd}' AND '{fyPriorEnd:yyyy-MM-dd}' 
                THEN 'FY{fiscalYear - 1}'
            WHEN ih.inv_date BETWEEN '{fyCurrentStart:yyyy-MM-dd}' AND '{fyCurrentEnd:yyyy-MM-dd}' 
                THEN 'FY{fiscalYear}'
        END AS FiscalYear,
        -- Pre-calculate the monetary values
        ii.qty_invoiced * ii.price AS RevAmount,
        ii.qty_invoiced AS QtyInvoiced
    FROM inv_item_mst ii 
    -- Add HINT to enforce join order if needed
    INNER JOIN inv_hdr_mst ih WITH (NOLOCK) ON ii.inv_num = ih.inv_num
    INNER JOIN customer_mst cu WITH (NOLOCK) ON ih.cust_num = cu.cust_num
    INNER JOIN custaddr_mst ca0 WITH (NOLOCK) ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
    INNER JOIN custaddr_mst ca WITH (NOLOCK) ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
    LEFT JOIN Chap_RegionNames rn WITH (NOLOCK) ON rn.Region = cu.Uf_SalesRegion
    LEFT JOIN Bat_App.dbo.Item_mst im WITH (NOLOCK) ON ii.item = im.item
    WHERE 
        -- Use a simplified date range that covers both fiscal years
        ih.inv_date BETWEEN '{fyPriorStart:yyyy-MM-dd}' AND '{fyCurrentEnd:yyyy-MM-dd}'
        AND cu.slsman = @RepCode
),
-- Create a separate CTE for aggregated values
AggregatedData AS (
    SELECT
        Customer,
        [Customer Name],
        [Ship To Num],
        [Ship To City],
        [Ship To State],
        slsman,
        SalespersonName,
        [Bill To State],
        Uf_SalesRegion,
        RegionName,
        Item,
        ItemDescription,
        Period,
        FiscalYear,
        SUM(RevAmount) AS RevAmount,
        SUM(QtyInvoiced) AS QtyInvoiced
    FROM InvoiceData
    GROUP BY
        Customer,
        [Customer Name],
        [Ship To Num],
        [Ship To City],
        [Ship To State],
        slsman,
        SalespersonName,
        [Bill To State],
        Uf_SalesRegion,
        RegionName,
        Item,
        ItemDescription,
        Period,
        FiscalYear
)
-- Final result set
SELECT
    Customer,
    [Customer Name],
    [Ship To Num],
    [Ship To City],
    [Ship To State],
    slsman,
    SalespersonName,
    [Bill To State],
    Uf_SalesRegion,
    RegionName,
    Item,
    ItemDescription,
    -- Prior Fiscal Year Totals
    SUM(CASE WHEN FiscalYear = 'FY{fiscalYear - 1}' THEN RevAmount ELSE 0 END) AS [FY{fiscalYear - 1}_Rev],
    SUM(CASE WHEN FiscalYear = 'FY{fiscalYear - 1}' THEN QtyInvoiced ELSE 0 END) AS [FY{fiscalYear - 1}_Qty],
    -- Current Fiscal Year Totals
    SUM(CASE WHEN FiscalYear = 'FY{fiscalYear}' THEN RevAmount ELSE 0 END) AS [FY{fiscalYear}_Rev],
    SUM(CASE WHEN FiscalYear = 'FY{fiscalYear}' THEN QtyInvoiced ELSE 0 END) AS [FY{fiscalYear}_Qty],
    -- Monthly columns for current fiscal year
    {monthColumns}
FROM AggregatedData
GROUP BY
    Customer,
    [Customer Name],
    [Ship To Num],
    [Ship To City],
    [Ship To State],
    slsman,
    SalespersonName,
    [Bill To State],
    Uf_SalesRegion,
    RegionName,
    Item,
    ItemDescription
OPTION (RECOMPILE, OPTIMIZE FOR UNKNOWN);
";

        return query;
    }










    private string GetDynamicQueryOrig()
    {
        return @"
    DECLARE @cols NVARCHAR(MAX),
            @colsPivot NVARCHAR(MAX),
            @query NVARCHAR(MAX),
            @currentMonth INT = MONTH(GETDATE()),
            @currentYear INT = YEAR(GETDATE()),
            @fy25StartYear INT = 2024;

    DECLARE @currentFiscalMonth INT = 
        CASE 
            WHEN MONTH(GETDATE()) >= 9 THEN MONTH(GETDATE()) - 8
            ELSE MONTH(GETDATE()) + 4
        END;

    SET @cols = STUFF(
            (SELECT ',ISNULL([' + fymonth + '], 0) AS [' + fymonth + ']'
         FROM (
             SELECT TOP (@currentFiscalMonth)
                 CASE 
                     WHEN n + 8 > 12 THEN FORMAT(DATEFROMPARTS(@fy25StartYear, n + 8 - 12, 1), 'MMM') + CAST(@fy25StartYear + 1 AS VARCHAR)
                     ELSE FORMAT(DATEFROMPARTS(@fy25StartYear, n + 8, 1), 'MMM') + CAST(@fy25StartYear AS VARCHAR)
                 END AS fymonth
             FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS numbers(n)
             WHERE DATEFROMPARTS(
                 CASE WHEN n + 8 > 12 THEN @fy25StartYear + 1 ELSE @fy25StartYear END,
                 CASE WHEN n + 8 > 12 THEN n + 8 - 12 ELSE n + 8 END,
                 1
             ) <= GETDATE()
         ) AS cols
         ORDER BY fymonth
         FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, '');

    SET @colsPivot = STUFF(
        (SELECT ',[' + fymonth + ']'
         FROM (
             SELECT TOP (@currentFiscalMonth)
                 CASE 
                     WHEN n + 8 > 12 THEN FORMAT(DATEFROMPARTS(@fy25StartYear, n + 8 - 12, 1), 'MMM') + CAST(@fy25StartYear + 1 AS VARCHAR)
                     ELSE FORMAT(DATEFROMPARTS(@fy25StartYear, n + 8, 1), 'MMM') + CAST(@fy25StartYear AS VARCHAR)
                 END AS fymonth
             FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS numbers(n)
             WHERE DATEFROMPARTS(
                 CASE WHEN n + 8 > 12 THEN @fy25StartYear + 1 ELSE @fy25StartYear END,
                 CASE WHEN n + 8 > 12 THEN n + 8 - 12 ELSE n + 8 END,
                 1
             ) <= GETDATE()
         ) AS cols
         ORDER BY fymonth
         FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, '');

    SET @query = '
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
        ISNULL(FY2024, 0) AS FY2024, ISNULL(FY2025, 0) AS FY2025, ' + @cols + '
    FROM (
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
            CASE 
                WHEN ih.inv_date BETWEEN ''2023-09-01'' AND ''2024-08-31'' THEN ''FY2024''
                ELSE FORMAT(ih.inv_date, ''MMM'') + CAST(YEAR(ih.inv_date) AS VARCHAR)
            END AS Period,
            ISNULL(SUM(ii.qty_invoiced * ii.price),0) AS ExtPrice
        FROM inv_item_mst ii 
        JOIN inv_hdr_mst ih ON ii.inv_num = ih.inv_num
        JOIN custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
        JOIN custaddr_mst ca ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
        JOIN customer_mst cu ON ih.cust_num = cu.cust_num AND cu.cust_seq = ih.cust_seq
        LEFT JOIN Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
        WHERE ih.inv_date >= ''2023-09-01'' 
            AND cu.slsman = @RepCode
        GROUP BY 
            ih.cust_num, 
            ca0.Name, 
            ih.cust_seq, 
            ca.City,
            ca.State,
            ca0.name, 
            ca0.state, 
            cu.Uf_SalesRegion, 
            rn.RegionName,
            cu.slsman,
            CASE 
                WHEN ih.inv_date BETWEEN ''2023-09-01'' AND ''2024-08-31'' THEN ''FY2024''
                ELSE FORMAT(ih.inv_date, ''MMM'') + CAST(YEAR(ih.inv_date) AS VARCHAR)
            END
 UNION ALL

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
        ''FY2025'' AS Period,
        ISNULL(SUM(ii.qty_invoiced * ii.price),0) AS ExtPrice
    FROM inv_item_mst ii 
    JOIN inv_hdr_mst ih ON ii.inv_num = ih.inv_num
    JOIN custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
    JOIN custaddr_mst ca ON ih.cust_num = ca.cust_num AND ih.cust_seq = ca.cust_seq
    JOIN customer_mst cu ON ih.cust_num = cu.cust_num AND cu.cust_seq = ih.cust_seq
    LEFT JOIN Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
    WHERE ih.inv_date >= ''2024-09-01'' 
        AND cu.slsman = @RepCode
    GROUP BY 
        ih.cust_num, 
        ca0.Name, 
        ih.cust_seq, 
        ca.City,
        ca.State,
        ca0.name, 
        ca0.state, 
        cu.Uf_SalesRegion, 
        rn.RegionName,
        cu.slsman
    ) AS src
    PIVOT (
        SUM(ExtPrice)
        FOR Period IN (FY2024, FY2025, ' + @colsPivot + ')
    ) AS pvt
    ORDER BY FY2024 DESC;';

 EXEC sp_executesql @query, N'@RepCode NVARCHAR(50)', @RepCode;";


    }



    public async Task<List<CustomerShipment>> GetShipmentsData(ShipmentsParameters parameters)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var results = await connection.QueryAsync<CustomerShipment>(@"
                EXEC RepPortal_GetShipmentsSp 
                    @BeginShipDate, 
                    @EndShipDate, 
                    @RepCode, 
                    @CustNum, 
                    @CorpNum, 
                    @CustType, 
                    @EndUserType",
                new
                {
                    parameters.BeginShipDate,
                    parameters.EndShipDate,
                    RepCode = _repCodeContext.CurrentRepCode,   // Security, mind you
                    parameters.CustNum,
                    parameters.CorpNum,
                    parameters.CustType,
                    parameters.EndUserType
                });

            return results.ToList();
        }
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
    }

    public string GetRepAgency(string repCode)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            var repAgency = connection.QueryFirstOrDefault<string>(
                "SELECT name as AgencyName FROM Chap_SlsmanNameV WHERE slsman = @RepCode",
                new { RepCode = repCode });

            return repAgency;
        }
    }

    public async Task<List<string>> GetAllRepCodesAsync()
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var results = await connection.QueryAsync<string>("SELECT slsman FROM Chap_SlsmanNameV  where slsman " +
                                                              "in (Select distinct slsman from customer_mst where stat = 'A' and cust_seq = 0 and cust_num <> 'LILBOY') ORDER BY slsman");
            return results.ToList();
        }
    }
    public async Task<List<CustomerOrderSummary>> GetOpenOrderSummariesAsync(/* string salesRepId */)
    {
        // Adjust SQL to filter by Sales Rep if applicable, or filter customers later
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
            WHERE Cust.slsman = @RepCode -- Example filter
            ORDER BY Cust.CorpName; ";

        using var connection = new SqlConnection(_connectionString);
        var summaries = await connection.QueryAsync<CustomerOrderSummary>(sql, new { RepCode = _repCodeContext.CurrentRepCode });
        return summaries.ToList();
    }

    // --- Get Detail Data for ONE Customer ---
    public async Task<List<OrderDetail>> GetOpenOrderDetailsAsync(string customerId)
    {
        // This query fetches ALL open order lines for the customer.
        // The distinction between Shippable/Future can be made in C# or added here.
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

        // Use the current date from the server for comparison
        // Note: The date literal in your original query ('5/01/2025') is hardcoded.
        // Fetching all 'O' status orders and determining shippable/future dynamically is often better.

        using var connection = new SqlConnection(_connectionString);
        var details = await connection.QueryAsync<OrderDetail>(detailSql, new { CustomerId = customerId });
        return details.ToList();
    }

    // --- Optional: Method to get ALL details for ALL relevant customers (for combined export) ---
    public async Task<List<OrderDetail>> GetAllOpenOrderDetailsAsync(/* string salesRepId */)
    {
        // Similar to GetOpenOrderDetailsAsync, but potentially joining with Cust table
        // first based on SalesRepId to get relevant Customer IDs, then fetching details.
        // Or fetch all details and filter in C# if the dataset isn't excessively large.
        const string allDetailSql = @"
            SELECT
                O.CUST AS Cust, C.CorpName AS Name, O.DUEDATE AS DueDate, O.ORDDATE AS OrdDate,
                O.PromDate AS PromDate, O.CustPO, O.CONUM AS CoNum, O.ITEM AS Item,
                O.PRICE AS Price, O.ORDQTY AS OrdQty, (O.Price * O.OrdQty) AS Dollars,
                C.B2Name AS ShipToName
            FROM CIISQL10.INTRANET.DBO.ORDERS O
            INNER JOIN CIISQL10.INTRANET.DBO.Cust C ON O.CUST = C.Cust AND O.CUSTSEQ = C.CustSeq
            WHERE O.STAT = 'O'
            AND C.slsman = @RepCode 
            ORDER BY C.Cust, O.DueDate, O.PromDate, O.CoNum;";

        using var connection = new SqlConnection(_connectionString);
        var allDetails = await connection.QueryAsync<OrderDetail>(allDetailSql, new { RepCode = _repCodeContext.CurrentRepCode });
        return allDetails.ToList();
    }


    public async Task LogReportUsageAsync(string repCode, string reportName)
    {
        string? adminUser = _repCodeContext.CurrentLastName;


        var sql = @"
        INSERT INTO CIISQL10.RepPortal.dbo.ReportUsageHistory (RepCode, ReportName, RunTime, AdminUser)
        VALUES (@RepCode, @ReportName, @RunTime, @AdminUser)";

        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new
        {
            RepCode = repCode,
            ReportName = reportName,
            RunTime = DateTime.Now, // Use UTC for consistency unless local is preferred
            AdminUser = adminUser
        });
    }



    public async Task<List<InvoiceRptDetail>> GetInvoiceRptData(InvoiceRptParameters parameters)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var results = await connection.QueryAsync<InvoiceRptDetail>(@"
                EXEC RepPortal.dbo.sp_GetInvoices 
                    @BeginInvoiceDate, 
                    @EndInvoiceDate, 
                    @RepCode, 
                    @CustNum, 
                    @CorpNum, 
                    @CustType, 
                    @EndUserType",
                new
                {
                    parameters.BeginInvoiceDate,
                    parameters.EndInvoiceDate,
                    RepCode = _repCodeContext.CurrentRepCode,   // Security, mind you
                    parameters.CustNum,
                    parameters.CorpNum,
                    parameters.CustType,
                    parameters.EndUserType
                });

            return results.ToList();
        }
    }


    public class InvoiceRptParameters
    {
        public DateTime BeginInvoiceDate { get; set; }
        public DateTime EndInvoiceDate { get; set; }
        public string RepCode { get; set; }
        public string CustNum { get; set; }
        public string CorpNum { get; set; }
        public string CustType { get; set; }
        public string EndUserType { get; set; }
    }






}



