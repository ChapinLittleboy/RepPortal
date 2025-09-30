// Services/ISalesDataService.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RepPortal.Services
{
    public interface ISalesDataService
    {
        Task<List<Dictionary<string, object>>> GetItemSalesReportDataAsync(
            string repCode,
            IEnumerable<string>? allowedRegions);
        Task<List<Dictionary<string, object>>> GetSalesReportData(
        
            string repCode,
            IEnumerable<string>? allowedRegions);
    }
}