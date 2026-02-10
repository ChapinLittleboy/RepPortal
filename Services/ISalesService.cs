using RepPortal.Models;
using RepPortal.Pages;

namespace RepPortal.Services;

public interface ISalesService
{
    Task<List<OrderDetail>> GetAllOpenOrderDetailsAsync();
    Task<List<CustType>> GetCustomerTypesListAsync();

   
     Task<List<RegionInfo>> GetAllRegionsAsync();

Task<List<RepInfo>> GetAllRepCodeInfoAsync();
Task<List<string>> GetAllRepCodesAsync();
string? GetCurrentRepCode();
Task<string?> GetCustNumFromCoNum(string coNum);
string GetDynamicQueryForItemsMonthlyWithQty(IEnumerable<string>? allowedRegions = null);
Task<List<InvoiceRptDetail>> GetInvoiceRptData(SalesService.InvoiceRptParameters parameters);
Task<List<Dictionary<string, object>>> GetItemSalesReportData();
Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQty();
Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQtyOLD();
Task<List<OrderDetail>> GetOpenOrderDetailsAsync(string customerId);
Task<List<CustomerOrderSummary>> GetOpenOrderSummariesAsync();
Task<List<MonthlyItemSalesPivot.SaleRow>> GetRecentSalesAsync();
Task<List<RegionItem>> GetRegionInfoForRepCodeAsync(string repCode);
Task<List<string>> GetRegionsForRepCodeAsync(string repCode);
string? GetRepAgency(string repCode);
Task<string?> GetRepCodeByRegistrationCodeAsync(string registrationCode);
Task<string?> GetRepIDAsync();
Task<List<Dictionary<string, object>>> GetSalesReportData();
Task<List<Dictionary<string, object>>> GetSalesReportDataUsingInvRep();
Task<List<CustomerShipment>> GetShipmentsData(SalesService.ShipmentsParameters parameters);
    Task<List<CustomerShipment>> GetShipmentsDataApiAsync(SalesService.ShipmentsParameters parameters);
Task LogReportUsageAsync(string repCode, string reportName);
Task<List<Dictionary<string, object>>> RunDynamicQueryAsync(string sql);

}