using static RepPortal.Pages.MonthlyItemSalesPivot;

namespace RepPortal.Services.ReportExport;

public interface IMonthlyItemSalesPivotWorkbookExporter
{
    byte[] Export(IReadOnlyList<SaleRow> rows, string periodBasis);
}
