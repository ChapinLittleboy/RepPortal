namespace RepPortal.Services;

using System.Data;

using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using MimeKit;
using Syncfusion.XlsIO;
using RepPortal.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RepPortal.Services;

public class ReportRunner
{
    private readonly IConfiguration _cfg;
    private readonly string _connString;
    private readonly IAttachmentEmailSender _email;
    private readonly ISalesDataService _salesData;
    private readonly IUserContextResolver _userCtx;



    public ReportRunner( IAttachmentEmailSender email, ISalesDataService salesData, IUserContextResolver userCtx )
    {
        _connString = @"Data Source=ciisql10;Database=RepPortal;User Id=sa;Password='*id10t*';TrustServerCertificate=True;";
        _email = email;
        _salesData = salesData;
        _userCtx = userCtx;


    }

    // This is what Hangfire calls per subscription id
    public async Task RunSubscriptionAsync(long subscriptionId)
    {
        using var conn = new SqlConnection(_cfg.GetConnectionString("RepPortalConnection"));
        await conn.OpenAsync();

        var sub = await LoadSubscriptionAsync(conn, subscriptionId);
        if (sub is null || !sub.IsActive) return;

        var data = await ExecuteReportAsync(conn, sub);
        var bytes = BuildExcel(data);

        // await SendEmailAsync(sub, bytes);
        await MarkRunAsync(conn, subscriptionId, success: true, null);
    }

    private async Task<ReportSub?> LoadSubscriptionAsync(SqlConnection conn, long id)
    {
        using var cmd = new SqlCommand(@"
SELECT s.SubscriptionId, s.ReportId, d.StoredProcName, s.TimeZoneId, s.Format, s.Recipients, s.IsActive
FROM dbo.ReportSubscription s
JOIN dbo.ReportDefinition d ON d.ReportId = s.ReportId
WHERE s.SubscriptionId = @id;", conn);
        cmd.Parameters.AddWithValue("@id", id);

        using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new ReportSub
        {
            SubscriptionId = r.GetInt64(0),
            ReportId = r.GetInt32(1),
            StoredProc = r.GetString(2),
            TimeZoneId = r.GetString(3),
            Format = r.GetString(4),
            Recipients = r.GetString(5),
            IsActive = r.GetBoolean(6),
        };
    }

    private async Task<DataTable> ExecuteReportAsync(SqlConnection conn, ReportSub sub)
    {
        // load filters
        var filters = new Dictionary<string, string>();
        using (var fcmd = new SqlCommand(
                   "SELECT Name, Value FROM dbo.ReportSubscriptionFilter WHERE SubscriptionId = @id", conn))
        {
            fcmd.Parameters.AddWithValue("@id", sub.SubscriptionId);
            using var fr = await fcmd.ExecuteReaderAsync();
            while (await fr.ReadAsync()) filters[fr.GetString(0)] = fr.GetString(1);
        }

        using var cmd = new SqlCommand(sub.StoredProc, conn) { CommandType = System.Data.CommandType.StoredProcedure };
        foreach (var kvp in filters)
            cmd.Parameters.AddWithValue("@" + kvp.Key, kvp.Value);

        using var da = new SqlDataAdapter(cmd);
        var dt = new DataTable();
        da.Fill(dt);
        return dt;
    }

    private byte[] BuildExcel(DataTable dt)
    {
        if (dt is null) throw new ArgumentNullException(nameof(dt));

        using var engine = new ExcelEngine();
        IApplication app = engine.Excel;
        app.DefaultVersion = ExcelVersion.Xlsx; // ensures .xlsx formatting

        // NOTE: Do NOT wrap wb in 'using' — IWorkbook is not IDisposable.
        IWorkbook wb = app.Workbooks.Create(1);
        IWorksheet ws = wb.Worksheets[0];

        ws.Name = "Report";
        ws.ImportDataTable(dt, isFieldNameShown: true, firstRow: 1, firstColumn: 1);
        ws.UsedRange.AutofitColumns();

        using var ms = new MemoryStream();
        wb.SaveAs(ms); // writes the workbook to the stream
        // No need to reset Position when using ToArray()
        return ms.ToArray(); // engine.Dispose() will close workbook/resources
    }



    private async Task MarkRunAsync(SqlConnection conn, long id, bool success, string? error)
    {
        using var cmd = new SqlCommand("rpt.SubscriptionMarkRun", conn)
            { CommandType = System.Data.CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@SubscriptionId", id);
        cmd.Parameters.AddWithValue("@Success", success);
        cmd.Parameters.AddWithValue("@Error", (object?)error ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Entry point for Hangfire recurring job. Computes the month, generates xlsx, emails it.
    /// </summary>
    public async Task SendMonthlyItemSalesEmailAsync(string toEmail, int year, int month)
    {
        if (year == 0 || month == 0)
        {
            var prev = DateTime.Today.AddMonths(-1);
            year = prev.Year;
            month = prev.Month;
        }

        var (start, end) = (new DateTime(year, month, 1), new DateTime(year, month, 1).AddMonths(1));
        //var dt = await GetMonthlyItemSalesAsync(start, end);
        var userCtx = await _userCtx.ResolveByEmailAsync(toEmail);
        if (userCtx is null)
            throw new InvalidOperationException($"Could not resolve user context for {toEmail}");

       

        var rawDatat = await _salesData.GetSalesReportData(repCode: userCtx.RepCode, allowedRegions: userCtx.AllowedRegions);

        var dt = ToDataTable(rawDatat);
        var xlsx = BuildExcel(dt, $"Monthly Item Sales {year}-{month:00}");

        var subject = $"Monthly Item Sales - {year}-{month:00}";
        var body = $"<p>Attached is the Monthly Item Sales report for {year}-{month:00}.</p>";

        await _email.SendAsync(
            toEmail,
            subject,
            body,
            new[]
            {
                (FileName: $"Monthly_Item_Sales_{year}-{month:00}.xlsx",
                    Bytes: xlsx,
                    ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            });
    }

    private static (DateTime Start, DateTime End) MonthRange(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1); // [start, end)
        return (start, end);
    }

    // TODO: Replace the SQL with your actual schema/logic
    private async Task<DataTable> GetMonthlyItemSalesAsync(DateTime start, DateTime end)
    {
        const string sql = @"
SELECT i.ItemNumber, i.ItemName,
       SUM(s.Quantity) AS Quantity,
       SUM(s.ExtendedPrice) AS Amount
FROM dbo.Sales s
JOIN dbo.Items i ON i.ItemId = s.ItemId
WHERE s.InvoiceDate >= @start AND s.InvoiceDate < @end
GROUP BY i.ItemNumber, i.ItemName
ORDER BY i.ItemNumber;";

        var dt = new DataTable("MonthlyItemSales");
        await using var conn = new SqlConnection(_connString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@start", start);
        cmd.Parameters.AddWithValue("@end", end);
        await using var rdr = await cmd.ExecuteReaderAsync();
        dt.Load(rdr);
        return dt;
    }

    // Uses Syncfusion.XlsIO to build an .xlsx byte[]
    private static byte[] BuildExcel(DataTable dt, string sheetName)
    {
        using var engine = new ExcelEngine();
        var app = engine.Excel;
        app.DefaultVersion = ExcelVersion.Xlsx;

        IWorkbook wb = app.Workbooks.Create(1);
        IWorksheet ws = wb.Worksheets[0];
        ws.Name = sheetName;

        ws.ImportDataTable(dt, true, 1, 1);
        ws.UsedRange.AutofitColumns();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static DataTable ToDataTable(
        IEnumerable<IDictionary<string, object?>> rows,
        string? tableName = null)
    {
        var table = new DataTable(tableName ?? "Table");

        // Build columns dynamically as we encounter new keys
        foreach (var dict in rows)
        {
            if (dict == null) continue;

            // Ensure all columns for this row exist
            foreach (var key in dict.Keys)
            {
                if (key == null) throw new ArgumentException("Dictionary contains a null key.");
                if (!table.Columns.Contains(key))
                {
                    // Use object to allow mixed types across rows (e.g., int in one row, double in another)
                    table.Columns.Add(key, typeof(object));
                }
            }
        }
        return table;
    }
}

public sealed record ReportSub
{
    public long SubscriptionId { get; init; }
    public int ReportId { get; init; }
    public string StoredProc { get; init; } = "";
    public string TimeZoneId { get; init; } = "America/New_York";
    public string Format { get; init; } = "xlsx";
    public string Recipients { get; init; } = "";
    public bool IsActive { get; init; }
}
