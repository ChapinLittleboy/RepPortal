
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Syncfusion.XlsIO;

namespace RepPortal.Services.ReportExport;


public sealed class ExcelReportExporter: IExcelReportExporter
    {
        public byte[] Export<T>(IReadOnlyList<T> rows, ExcelExportOptions options)
        {
            using var excelEngine = new ExcelEngine();
            var app = excelEngine.Excel;
            app.DefaultVersion = ExcelVersion.Xlsx;

        IWorkbook workbook = app.Workbooks.Create(1);
        IWorksheet ws = workbook.Worksheets[0];
        ws.Name = Truncate(options.WorksheetName, 31);

            int currentRow = 1;

            // Optional title & subtitle
            if (!string.IsNullOrWhiteSpace(options.Title))
            {
                ws.Range[currentRow, 1].Text = options.Title;
                ws.Range[currentRow, 1].CellStyle.Font.Bold = true;
                ws.Range[currentRow, 1].CellStyle.Font.Size = 14;
                currentRow++;
            }
            if (!string.IsNullOrWhiteSpace(options.Subtitle))
            {
                ws.Range[currentRow, 1].Text = options.Subtitle;
                ws.Range[currentRow, 1].CellStyle.Font.Italic = true;
                currentRow++;
            }

            // Import data (header row included)
            // XlsIO can import IEnumerable<T> directly:
            // Start at currentRow, col 1
            if (rows.Count > 0)
            {
                ws.ImportData(rows, currentRow, 1, true);
            }
            else
            {
                // When empty, still write headers from T’s public props to be consistent
                var props = GetReadableProps(typeof(T));
                for (int c = 0; c < props.Length; c++)
                    ws[currentRow, c + 1].Text = props[c].Name;
            }

            // Determine used range starting at the header row
            int headerRow = currentRow;
            int lastRow = Math.Max(headerRow + Math.Max(rows.Count, 1), headerRow);
            int lastCol = ws.UsedRange?.LastColumn ?? GetReadableProps(typeof(T)).Length;

            // Pretty up: bold header
            ws.Range[headerRow, 1, headerRow, lastCol].CellStyle.Font.Bold = true;

            // Optional table & autofilter
            /*
            if (options.CreateTable && lastCol > 0 && lastRow >= headerRow)
            {
                var lo = ws.ListObjects.Create("Table1", ws.Range[headerRow, 1, lastRow, lastCol]);
                lo.BuiltInTableStyle = TableBuiltInStyles.TableStyleMedium2;
            }
            if (options.AutoFilter && lastCol > 0)
            {
                ws.AutoFilters.FilterRange = ws.Range[headerRow, 1, lastRow, lastCol];
               // ws.AutoFilters.Autofit = true;
            }
            */

            // Freeze header row (below it)
            if (options.FreezeHeader && lastCol > 0)
                ws.Range[headerRow + 1, 1].FreezePanes();

            // Column formatting
            var dateCols = new HashSet<string>(options.DateColumns ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var currencyCols = new HashSet<string>(options.CurrencyColumns ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            if (lastCol > 0 && lastRow >= headerRow)
            {
                // Map prop name -> column index from header row
                var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= lastCol; c++)
                {
                    var name = ws[headerRow, c].DisplayText?.Trim();
                    if (!string.IsNullOrEmpty(name) && !headerMap.ContainsKey(name))
                        headerMap[name] = c;
                }

                foreach (var dc in dateCols)
                {
                    if (headerMap.TryGetValue(dc, out int col))
                        ws.Range[headerRow + 1, col, lastRow, col].NumberFormat = "yyyy-mm-dd";
                }
                foreach (var cc in currencyCols)
                {
                    if (headerMap.TryGetValue(cc, out int col))
                        ws.Range[headerRow + 1, col, lastRow, col].NumberFormat = "$#,##0.00";
                }
            }

            ws.UsedRange.AutofitColumns();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        private static PropertyInfo[] GetReadableProps(Type t) =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.CanRead)
             .ToArray();

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max);
    }

