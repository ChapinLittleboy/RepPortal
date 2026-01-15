// Services/Reports/ShipmentsReport.cs

using Dapper;
using RepPortal.Data;
namespace RepPortal.Services.Reports
{
    public interface IShipmentsReport
    {
        Task<List<Dictionary<string, object>>> GetAsync(
            string repCode,
            IEnumerable<string>? allowedRegions,
            string? customerId,
            DateTime startUtc,
            DateTime endUtcExclusive);
    }

    public sealed class ShipmentsReport : IShipmentsReport
    {
        private readonly IDbConnectionFactory _dbFactory;
        private readonly ILogger<ShipmentsReport>? _logger;

        public ShipmentsReport(IDbConnectionFactory dbFactory, ILogger<ShipmentsReport>? logger = null)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task<List<Dictionary<string, object>>> GetAsync(
            string repCode, 
            IEnumerable<string>? allowedRegions,
            string? customerId,
            DateTime startDate,
            DateTime endDate)

        {
            var minStart= new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local);
            var maxEnd = DateTime.Now.Date.AddDays(60);


            // Clamp startUtc
            if (startDate < minStart)
            {
                startDate = minStart;
            }

            // Clamp endUtcExclusive
            if (endDate >= maxEnd)
            {
                endDate = maxEnd;
            }

            // Final sanity check
            if (startDate >= endDate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startDate),
                    "The date range is invalid after applying limits."
                );
            }


            using var conn = _dbFactory.CreateRepConnection();
            conn.Open();

            var sql = BuildQuery(allowedRegions);

            var rows =  conn.Query(sql, new
            {
                RepCode = repCode,
                CustomerId = customerId,
                StartUtc = startDate,
                EndUtc = endDate
            });

            return InvoicedAccountsReport.Materialize(rows); // reuse helper if you like
        }

        private static string BuildQuery(IEnumerable<string>? allowedRegions)
        {
            var regionFilter = allowedRegions is null ? ""
                : "AND s.Region IN @AllowedRegions";

            return $@"
SELECT top 100 
    s.co_num,
    s.Ship_Date,
    co.Cust_Num,
    
    s.Qty_Shipped,
    ci.Item
FROM BAT_App.dbo.co_ship_mst s
join Bat_App.dbo.co_mst co on s.Co_num  = co.co_num
join Bat_App.dbo.coitem_mst ci on  co.co_num = ci.co_num 
WHERE 1 = 1 --s.RepCode = @RepCode
  AND (@CustomerId IS NULL OR co.Cust_Num = @CustomerId)
  AND s.Ship_Date >= @StartUtc
  AND s.Ship_Date <  @EndUtc
  {regionFilter}
ORDER BY s.Ship_Date DESC";
        }
    }
}