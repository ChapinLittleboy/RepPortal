using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.SqlClient;
using RepPortal.Models;
using System.Data;

namespace RepPortal.Services;

public class SalesService
{
    private readonly string _connectionString;
    private readonly AuthenticationStateProvider _authenticationStateProvider;


    public SalesService(IConfiguration configuration, AuthenticationStateProvider authenticationStateProvider)
    {
        _connectionString = configuration.GetConnectionString("BatAppConnection");
        _authenticationStateProvider = authenticationStateProvider;

    }

    public async Task<string> GetRepIDAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        return user?.FindFirst("RepID")?.Value;
    }





    public async Task<List<Dictionary<string, object>>> GetSalesReportData()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var repCode = user?.FindFirst("RepCode")?.Value;

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            var query = GetDynamicQuery();
            var parameters = new { RepCode = repCode };
            var results = await connection.QueryAsync(query, parameters, commandType: CommandType.Text);

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
                    parameters.RepCode,
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
        public string RepCode { get; set; }
        public string CustNum { get; set; }
        public string CorpNum { get; set; }
        public string CustType { get; set; }
        public string EndUserType { get; set; }
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


}