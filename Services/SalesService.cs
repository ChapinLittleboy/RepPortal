using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace RepPortal.Services;

public class SalesService
{
    private readonly string _connectionString;

    public SalesService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("BatAppConnection");
    }

    public async Task<List<Dictionary<string, object>>> GetSalesReportData()
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var query = GetDynamicQuery();
            var results = await connection.QueryAsync(query, commandType: CommandType.Text);

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

    private string GetDynamicQuery()
    {
        return @"
    DECLARE @cols NVARCHAR(MAX),
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
        slsman,
        name,
        [Bill To State],
        Uf_SalesRegion,
        RegionName,
        FY2024,' + @cols + '
    FROM (
        SELECT 
            ih.cust_num AS Customer,
            ca0.Name AS [Customer Name],
            ih.cust_seq AS [Ship To Num],
            cu.slsman,
            ca0.name,
            ca0.state AS [Bill To State],
            cu.Uf_SalesRegion,
            rn.RegionName,
            CASE 
                WHEN ih.inv_date BETWEEN ''2023-09-01'' AND ''2024-08-31'' THEN ''FY2024''
                ELSE FORMAT(ih.inv_date, ''MMM'' + CASE WHEN MONTH(ih.inv_date) >= 9 THEN CAST(YEAR(ih.inv_date) AS VARCHAR) ELSE CAST(YEAR(ih.inv_date) + 1 AS VARCHAR) END)
            END AS Period,
            SUM(ii.qty_invoiced * ii.price) AS ExtPrice
        FROM inv_item_mst ii 
        JOIN inv_hdr_mst ih ON ii.inv_num = ih.inv_num
        JOIN custaddr_mst ca0 ON ih.cust_num = ca0.cust_num AND ca0.cust_seq = 0
        JOIN customer_mst cu ON ih.cust_num = cu.cust_num AND cu.cust_seq = ih.cust_seq
        LEFT JOIN Chap_RegionNames rn ON rn.Region = cu.Uf_SalesRegion
        WHERE ih.inv_date >= ''2023-09-01'' 
            AND cu.slsman = ''LAW''
        GROUP BY 
            ih.cust_num, 
            ca0.Name, 
            ih.cust_seq, 
            ca0.name, 
            ca0.state, 
            cu.Uf_SalesRegion, 
            rn.RegionName,
            cu.slsman,
            CASE 
                WHEN ih.inv_date BETWEEN ''2023-09-01'' AND ''2024-08-31'' THEN ''FY2024''
                ELSE FORMAT(ih.inv_date, ''MMM'' + CASE WHEN MONTH(ih.inv_date) >= 9 THEN CAST(YEAR(ih.inv_date) AS VARCHAR) ELSE CAST(YEAR(ih.inv_date) + 1 AS VARCHAR) END)
            END
    ) AS src
    PIVOT (
        SUM(ExtPrice)
        FOR Period IN (FY2024,' + @cols + ')
    ) AS pvt
    ORDER BY FY2024 DESC;';

    EXEC sp_executesql @query;";
    }
}