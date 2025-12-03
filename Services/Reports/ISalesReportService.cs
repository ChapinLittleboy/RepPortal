// Services/Reports/ISalesReportService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RepPortal.Services.Reports;



    public interface IShipmentsReport
    {
        Task<List<Dictionary<string, object>>> GetAsync(
            string repCode,
            IEnumerable<string>? allowedRegions,
            string? customerId,
            DateTime startUtc,
            DateTime endUtcExclusive);
    }

    // Add more as needed:
    public interface IMonthlyInvoicedSalesReport { /* no customer/date-range here */ }
    public interface IOpenOrdersReport { /* … */ }
    public interface IMonthlySalesReport { /* … */ }
    public interface IMonthlySalesByItemReport { /* … */ }
    public interface IPivotSalesByItemReport { /* … */ }
