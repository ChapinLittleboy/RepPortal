using Syncfusion.XlsIO;
using static RepPortal.Pages.MonthlyItemSalesPivot;

namespace RepPortal.Services.ReportExport;

public sealed class MonthlyItemSalesPivotWorkbookExporter : IMonthlyItemSalesPivotWorkbookExporter
{
    private const string FiscalPeriodBasis = "Fiscal";
    private const string CalendarPeriodBasis = "Calendar";

    public byte[] Export(IReadOnlyList<SaleRow> rows, string periodBasis)
    {
        using var excelEngine = new ExcelEngine();
        IApplication application = excelEngine.Excel;
        application.DefaultVersion = ExcelVersion.Excel2016;

        IWorkbook workbook = application.Workbooks.Create(2);
        workbook.Version = ExcelVersion.Excel2016;

        IWorksheet dataSheet = workbook.Worksheets[0];
        dataSheet.Name = "Data";

        IWorksheet pivotSheet = workbook.Worksheets[1];
        pivotSheet.Name = "Pivot";

        WriteDataSheet(dataSheet, rows);
        CreatePivotSheet(workbook, dataSheet, pivotSheet, rows.Count, periodBasis);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteDataSheet(IWorksheet sheet, IReadOnlyList<SaleRow> rows)
    {
        string[] headers =
        [
            "Customer",
            "Item",
            "ShipTo",
            "FiscalYear",
            "FiscalQuarter",
            "FiscalMonth",
            "CalendarYear",
            "CalendarQuarter",
            "CalendarMonth",
            "Quantity",
            "SalesAmount"
        ];

        for (int column = 0; column < headers.Length; column++)
        {
            sheet.Range[1, column + 1].Text = headers[column];
            sheet.Range[1, column + 1].CellStyle.Font.Bold = true;
        }

        for (int index = 0; index < rows.Count; index++)
        {
            SaleRow row = rows[index];
            int excelRow = index + 2;

            sheet.Range[excelRow, 1].Text = row.CustNumWithName;
            sheet.Range[excelRow, 2].Text = row.ItemNumWithProduct;
            sheet.Range[excelRow, 3].Text = row.ShipToNameAddr;
            sheet.Range[excelRow, 4].Text = row.FiscalYearLabel;
            sheet.Range[excelRow, 5].Text = row.QuarterOfFiscalYearLabel;
            sheet.Range[excelRow, 6].Text = row.NumberedMonthShort;
            sheet.Range[excelRow, 7].Text = row.CalendarYearLabel;
            sheet.Range[excelRow, 8].Text = row.CalendarQuarterLabel;
            sheet.Range[excelRow, 9].Text = row.NumberedCalendarMonthShort;
            sheet.Range[excelRow, 10].Number = row.Quantity;
            sheet.Range[excelRow, 11].Number = decimal.ToDouble(row.SalesAmount);
        }

        sheet.Range[2, 11, Math.Max(rows.Count + 1, 2), 11].NumberFormat = "$#,##0";
        sheet.UsedRange.AutofitColumns();
    }

    private static void CreatePivotSheet(
        IWorkbook workbook,
        IWorksheet dataSheet,
        IWorksheet pivotSheet,
        int rowCount,
        string periodBasis)
    {
        int lastRow = Math.Max(rowCount + 1, 2);
        IPivotCache cache = workbook.PivotCaches.Add(dataSheet.Range[1, 1, lastRow, 11]);
        IPivotTable pivotTable = pivotSheet.PivotTables.Add("MonthlyItemSalesPivot", pivotSheet.Range["A1"], cache);

        pivotTable.Fields[0].Axis = PivotAxisTypes.Row;
        pivotTable.Fields[1].Axis = PivotAxisTypes.Row;
        pivotTable.Fields[2].Axis = PivotAxisTypes.Row;

        if (string.Equals(periodBasis, CalendarPeriodBasis, StringComparison.OrdinalIgnoreCase))
        {
            pivotTable.Fields[6].Axis = PivotAxisTypes.Column;
        }
        else
        {
            pivotTable.Fields[3].Axis = PivotAxisTypes.Column;
        }

        IPivotField quantityField = pivotTable.Fields[9];
        IPivotField salesAmountField = pivotTable.Fields[10];

        pivotTable.DataFields.Add(quantityField, "Qty", PivotSubtotalTypes.Sum);
        IPivotDataField salesAmountDataField = pivotTable.DataFields.Add(salesAmountField, "Amount", PivotSubtotalTypes.Sum);
        salesAmountDataField.NumberFormat = "$#,##0";

        pivotTable.BuiltInStyle = PivotBuiltInStyles.PivotStyleMedium2;
        pivotTable.Options.RowHeaderCaption = "Rows";
        pivotTable.Options.ColumnHeaderCaption = "Years";
        pivotTable.Options.ShowFieldList = true;
        pivotTable.Options.PreserveFormatting = true;

        pivotSheet.SetColumnWidth(1, 24);
        pivotSheet.SetColumnWidth(2, 28);
        pivotSheet.SetColumnWidth(3, 28);
        pivotSheet.Activate();
    }

}
