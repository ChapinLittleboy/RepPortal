// Services/Reports/ShipmentsReport.cs

using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepPortal.Data;
using RepPortal.Models;

namespace RepPortal.Services.Reports
{
    public interface IShipmentsReport
    {
        Task<List<CustomerShipment>> GetAsync(
            string repCode,
            IEnumerable<string>? allowedRegions,
            string? customerId,
            DateTime startDate,
            DateTime endDate);
    }

    public sealed class ShipmentsReport : IShipmentsReport
    {
        private readonly IDbConnectionFactory _dbFactory;
        private readonly ILogger<ShipmentsReport>? _logger;
        private readonly IIdoService _idoService;
        private readonly CsiOptions _csiOptions;

        public ShipmentsReport(
            IDbConnectionFactory dbFactory,
            IIdoService idoService,
            IOptions<CsiOptions> csiOptions,
            ILogger<ShipmentsReport>? logger = null)
        {
            _dbFactory = dbFactory;
            _idoService = idoService;
            _csiOptions = csiOptions.Value;
            _logger = logger;
        }

        public async Task<List<CustomerShipment>> GetAsync(
            string repCode,
            IEnumerable<string>? allowedRegions,
            string? customerId,
            DateTime startDate,
            DateTime endDate)
        {
            var minStart = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local);
            var maxEnd = DateTime.Now.Date.AddDays(60);

            if (startDate < minStart) startDate = minStart;
            if (endDate >= maxEnd)    endDate = maxEnd;

            if (startDate >= endDate)
                throw new ArgumentOutOfRangeException(nameof(startDate),
                    "The date range is invalid after applying limits.");

            _logger?.LogInformation(
                "Shipments: Rep={Rep} Cust={Cust} Range=[{Start}->{End}) UseApi={UseApi}",
                repCode, customerId ?? "(ALL)", startDate, endDate, _csiOptions.UseApi);

            if (_csiOptions.UseApi)
            {
                var parameters = new SalesService.ShipmentsParameters
                {
                    RepCode       = repCode,
                    BeginShipDate = startDate,
                    EndShipDate   = endDate,
                    CustNum       = customerId,
                };
                var regions = allowedRegions?.ToList();
                return await _idoService.GetShipmentsDataAsync(parameters, repCode, regions);
            }

            // SQL path — kept for on-premise fallback
            using var conn = _dbFactory.CreateRepConnection();
            conn.Open();

            var regionList = allowedRegions?.ToList();
            var sql = BuildQuery(regionList);

            var rows = conn.Query<CustomerShipment>(sql, new
            {
                RepCode    = repCode,
                CustomerId = customerId,
                StartUtc   = startDate,
                EndUtc     = endDate
            });

            return rows.ToList();
        }

        private static string BuildQuery(IEnumerable<string>? allowedRegions)
        {
            var regionFilter = allowedRegions is null ? ""
                : "AND s.Uf_SalesRegion IN @AllowedRegions";

            // Note: SQL path is on-premise only. Cloud path uses GetShipmentsDataAsync via IDO.
            return $@"
SELECT
    co.co_num       AS OrderNumber,
    s.ship_date     AS ShipDate,
    co.cust_num     AS CustNum,
    si.qty_shipped  AS ShipQty,
    si.item         AS ItemNum
FROM BAT_App.dbo.coship_mst s
JOIN BAT_App.dbo.co_mst co   ON s.co_num = co.co_num
JOIN BAT_App.dbo.coshipitem_mst si ON s.co_num = si.co_num AND s.shipment_id = si.shipment_id
JOIN BAT_App.dbo.customer_mst cu  ON co.cust_num = cu.cust_num AND co.cust_seq = cu.cust_seq
WHERE co.slsman = @RepCode
  AND (@CustomerId IS NULL OR co.cust_num = @CustomerId)
  AND s.ship_date >= @StartUtc
  AND s.ship_date <  @EndUtc
  {regionFilter}
ORDER BY s.ship_date DESC";
        }
    }
}
