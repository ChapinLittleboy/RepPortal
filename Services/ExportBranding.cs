using Syncfusion.Blazor.Grids;
using Syncfusion.Pdf.Graphics;
using Syncfusion.XlsIO;

namespace RepPortal.Services;

public static class ExportBranding
{
    public const string DocumentFontName = "Arial";

    public static ExcelExportProperties CreateExcelExportProperties(string fileName)
        => new()
        {
            FileName = fileName,
            Theme = CreateExcelTheme()
        };

    public static ExcelTheme CreateExcelTheme()
        => new()
        {
            Header = new ExcelStyle { FontName = DocumentFontName },
            Record = new ExcelStyle { FontName = DocumentFontName },
            Caption = new ExcelStyle { FontName = DocumentFontName }
        };

    public static void ApplyTo(IApplication application)
    {
        application.StandardFont = DocumentFontName;
    }

    public static void ApplyTo(IWorkbook workbook)
    {
        foreach (IStyle style in workbook.Styles)
        {
            style.Font.FontName = DocumentFontName;
        }

        foreach (IWorksheet worksheet in workbook.Worksheets)
        {
            if (worksheet.UsedRange is { } usedRange)
            {
                usedRange.CellStyle.Font.FontName = DocumentFontName;
            }
        }
    }

    public static PdfFont CreatePdfFont(float size, PdfFontStyle style = PdfFontStyle.Regular)
    {
        string fontFileName = style.HasFlag(PdfFontStyle.Bold) ? "arialbd.ttf" : "arial.ttf";
        string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), fontFileName);

        return File.Exists(fontPath)
            ? new PdfTrueTypeFont(fontPath, size, style)
            : new PdfStandardFont(PdfFontFamily.Helvetica, size, style);
    }
}
