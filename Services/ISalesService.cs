using RepPortal.Models;
using RepPortal.Pages;

namespace RepPortal.Services;

public interface ISalesService
{
    Task<List<OrderDetail>> GetAllOpenOrderDetailsAsync();
Task<(OrderLookupHeader? Header, List<OrderLookupLine> Lines)> GetOrderLookupAsync(
    string custNum, string normalizedPo, string repCode);
    Task<List<CustType>> GetCustomerTypesListAsync();

   
     Task<List<RegionInfo>> GetAllRegionsAsync();

Task<List<RepInfo>> GetAllRepCodeInfoAsync();
Task<List<string>> GetAllRepCodesAsync();
string? GetCurrentRepCode();
Task<string?> GetCustNumFromCoNum(string coNum);
string GetDynamicQueryForItemsMonthlyWithQty(IEnumerable<string>? allowedRegions = null);
Task<List<InvoiceRptDetail>> GetInvoiceRptData(SalesService.InvoiceRptParameters parameters);
    Task<List<InvoiceRptDetail>> GetInvoiceRptDataApiAsync(SalesService.InvoiceRptParameters parameters);
Task<List<Dictionary<string, object>>> GetItemSalesReportData();
Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQty(string yearMode = "FY");
Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQtyApiAsync(string yearMode = "FY");
Task<List<Dictionary<string, object>>> GetItemSalesReportDataWithQtyOLD();
Task<List<OrderDetail>> GetOpenOrderDetailsAsync(string customerId);
Task<List<CustomerOrderSummary>> GetOpenOrderSummariesAsync();
Task<List<MonthlyItemSalesPivot.SaleRow>> GetRecentSalesAsync(string periodBasis = "Fiscal");
Task<List<RegionItem>> GetRegionInfoForRepCodeAsync(string repCode);
Task<List<string>> GetRegionsForRepCodeAsync(string repCode);
string? GetRepAgency(string repCode);
Task<string?> GetRepCodeByRegistrationCodeAsync(string registrationCode);
Task<string?> GetRepIDAsync();
Task<List<Dictionary<string, object>>> GetSalesReportData(string yearMode = "FY");
Task<List<Dictionary<string, object>>> GetSalesReportDataApiAsync(string yearMode = "FY");
Task<List<Dictionary<string, object>>> GetSalesReportDataUsingInvRep(string yearMode = "FY");
Task<List<Dictionary<string, object>>> GetSalesReportDataUsingInvRepApiAsync(string yearMode = "FY");
Task<List<CustomerShipment>> GetShipmentsData(SalesService.ShipmentsParameters parameters);
    Task<List<CustomerShipment>> GetShipmentsDataApiAsync(SalesService.ShipmentsParameters parameters);
Task LogReportUsageAsync(string repCode, string reportName);
Task<List<Dictionary<string, object>>> RunDynamicQueryAsync(string sql);

}
