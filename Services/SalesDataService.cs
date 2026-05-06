// Services/SalesDataService.cs  (CORE: no AuthenticationStateProvider here)
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.SqlClient; // per your preference
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RepPortal.Services
{
    public sealed class SalesDataService : ISalesDataService
    {
        private readonly string _connectionString;
        private readonly ILogger<SalesDataService>? _logger;

        public SalesDataService(IConfiguration configuration, ILogger<SalesDataService>? logger = null)
        {
            _connectionString = configuration.GetRequiredResolvedConnectionString("RepPortalConnection");
            _logger = logger;
        }

        public async Task<List<Dictionary<string, object>>> GetItemSalesReportDataAsync(
            string repCode,
            IEnumerable<string>? allowedRegions)
        {
            if (string.IsNullOrWhiteSpace(repCode))
                throw new InvalidOperationException("repCode is required.");

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = BuildItemsMonthlyQuery(allowedRegions); // your existing helper
            _logger?.LogInformation("GetItemSalesReportData SQL:\n{Sql}", query);

            var rows = await connection.QueryAsync(query, new { RepCode = repCode }, commandType: CommandType.Text);
            return MaterializeToDictionaries(rows);            // your existing helper
        }

        // ---- reuse your existing helpers; included here only to show where they live now ----
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


        public async Task<List<Dictionary<string, object>>> GetSalesReportData(string repCode,
            IEnumerable<string>? allowedRegions,
            string yearMode = "FY")
        {

            if (string.IsNullOrWhiteSpace(repCode))
                throw new InvalidOperationException("repCode is required.");


        


            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var (query, yearLabel) = BuildSalesPivotQuery(allowedRegions, yearMode);
            var parameters = new { RepCode = repCode, AllowedRegions = allowedRegions };
            var rows = await connection.QueryAsync(query, parameters, commandType: CommandType.Text);

            _logger?.LogInformation("GetSalesReportData {YearMode} used: {YearLabel}", yearMode, yearLabel);

            return MaterializeToDictionaries(rows);
        }



        private (string query, string currentYearLabel) BuildSalesPivotQuery(
            IEnumerable<string>? allowedRegions = null,
            string yearMode = "FY")
        {
            var period = SalesReportPeriodHelper.Create(yearMode);

            var colsPivot = string.Join(",", period.AllMonths.Select(m => $"[{m}]"));
            var currentYearColList = string.Join(",", period.CurrentYearMonths.Select(m => $"ISNULL([{m}], 0) AS [{m}]"));

            var priorYear3Sum = string.Join(" + ", period.PriorYear3Months.Select(m => $"ISNULL([{m}],0)"));
            var priorYear2Sum = string.Join(" + ", period.PriorYear2Months.Select(m => $"ISNULL([{m}],0)"));
            var priorYear1Sum = string.Join(" + ", period.PriorYear1Months.Select(m => $"ISNULL([{m}],0)"));
            var currentYearSum = period.CurrentYearMonths.Any()
                ? string.Join(" + ", period.CurrentYearMonths.Select(m => $"ISNULL([{m}],0)"))
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
    WHERE ih.inv_date >= '{period.HistoryStart:yyyy-MM-dd}'
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
    {priorYear3Sum} AS {period.PriorYear3Label},
    {priorYear2Sum} AS {period.PriorYear2Label},
    {priorYear1Sum} AS {period.PriorYear1Label},
    {currentYearSum} AS {period.CurrentYearLabel},
    {currentYearColList}
FROM (
    {BaseSelect("Bat_App")}
    UNION ALL
    {BaseSelect("Kent_App")}
) AS src
PIVOT (
    SUM(ExtPrice)
    FOR Period IN ({colsPivot})
) AS pvt
ORDER BY {period.PriorYear1Label} DESC;";

            return (query, period.CurrentYearLabel);
        }
    }
}
