// Services/Reports/ShipmentsReport.cs

using Dapper;
using RepPortal.Data;
namespace RepPortal.Services.Reports
{
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
            DateTime startUtc,
            DateTime endUtcExclusive)
        {
            using var conn = _dbFactory.CreateRepConnection();
            conn.Open();

            var sql = BuildQuery(allowedRegions);

            var rows =  conn.Query(sql, new
            {
                RepCode = repCode,
                CustomerId = customerId,
                StartUtc = startUtc,
                EndUtc = endUtcExclusive
            });

            return InvoicedAccountsReport.Materialize(rows); // reuse helper if you like
        }

        private static string BuildQuery(IEnumerable<string>? allowedRegions)
        {
            var regionFilter = allowedRegions is null ? ""
                : "AND s.Region IN @AllowedRegions";

            return $@"
SELECT
    s.ShipmentNo,
    s.ShipDate,
    s.Cust_Num,
    s.Cust_Name,
    s.Qty,
    s.Item_No
FROM dbo.Shipments s
WHERE s.RepCode = @RepCode
  AND (@CustomerId IS NULL OR s.Cust_Num = @CustomerId)
  AND s.ShipDate >= @StartUtc
  AND s.ShipDate <  @EndUtc
  {regionFilter}
ORDER BY s.ShipDate DESC";
        }
    }
}