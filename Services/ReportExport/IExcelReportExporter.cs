namespace RepPortal.Services.ReportExport;

public sealed record ExcelExportOptions(
    string WorksheetName,
    string? Title = null,
    string? Subtitle = null,
    string[]? DateColumns = null,      // property names to format as dates
    string[]? CurrencyColumns = null,  // property names to format as currency
    bool AutoFilter = true,
    bool CreateTable = true,
    bool FreezeHeader = true);

public interface IExcelReportExporter
{
    byte[] Export<T>(IReadOnlyList<T> rows, ExcelExportOptions options);
}