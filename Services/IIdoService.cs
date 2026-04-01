using RepPortal.Models;

namespace RepPortal.Services;

public interface IIdoService
{
    Task<List<OrderDetail>> GetAllOpenOrderDetailsAsync(string repCode, List<string> salesRegions);
    Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQtyAsync(string repCode, List<string> allowedRegions);
    Task<List<CustomerShipment>> GetShipmentsDataAsync(SalesService.ShipmentsParameters parameters, string repCode, IEnumerable<string>? allowedRegions);
    Task<List<InvoiceRptDetail>> GetInvoiceRptDataAsync(SalesService.InvoiceRptParameters parameters, string repCode);
    Task<List<Dictionary<string, object>>> GetSalesReportDataUsingInvRepAsync(string repCode, IEnumerable<string>? allowedRegions);
    Task<List<Dictionary<string, object>>> GetSalesReportDataAsync(string repCode, IEnumerable<string>? allowedRegions);
}
