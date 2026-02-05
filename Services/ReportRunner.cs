// Services/ReportRunner.cs
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using Hangfire;
using MailKit;
using Microsoft.Extensions.Logging;
using RepPortal.Models;
using RepPortal.Services.ReportExport;
using RepPortal.Services.Reports;

namespace RepPortal.Services
{
    public sealed class ReportRunner
    {
        private readonly IUserContextResolver _userCtx;
        private readonly SmtpEmailSender _email;

        private readonly IInvoicedAccountsReport _invoicedAccounts;
        private readonly IShipmentsReport _shipments;
        private readonly IExcelReportExporter _excel;
        private readonly IExpiringPcfNotificationsJob _expiringPcfNotificationsJob;

        // inject others as you implement…

        private readonly ILogger<ReportRunner> _logger;

        public ReportRunner(
            IUserContextResolver userCtx,
            SmtpEmailSender email,
            IInvoicedAccountsReport invoicedAccounts,
            IShipmentsReport shipments,
            IExcelReportExporter excel,
            ILogger<ReportRunner> logger,
            IExpiringPcfNotificationsJob expiringPcfNotificationsJob)
        {
            _userCtx = userCtx;
            _email = email;
            _invoicedAccounts = invoicedAccounts;
            _shipments = shipments;
            _logger = logger;
            _excel = excel;
            _expiringPcfNotificationsJob = expiringPcfNotificationsJob;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 3600)]
        public async Task RunAsync(ReportRequest req)
        {
            // Normalize (enforces server-side rules you added earlier)
            Normalize(req.ReportType, req.CustomerId, req.DateRangeCode,
                out var cust, out var rangeCode);

            var userCtx = await _userCtx.ResolveByEmailAsync(req.Email)
                          ?? throw new InvalidOperationException($"No user context for {req.Email}");
            var (repCode, regions) = (userCtx.RepCode, (IReadOnlyList<string>?)userCtx.AllowedRegions);

            // Translate DateRangeCodeType -> concrete range (use user's timezone if applicable)
            var (start, end) = ToDateRange(rangeCode, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

            List<Dictionary<string, object>> data;
            List<InvoiceRptDetail> invData;
            switch (req.ReportType)
            {
                case ReportType.InvoicedAccounts:
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); // or from subscription
                    await RunInvoicedAccountsAsync(req, tz);
                    break;
                }

                case ReportType.ShipmentsNotImplemented:
                    data = await _shipments.GetAsync(repCode, regions, cust, start, end);
                    await EmailAsync(req.Email, "Shipments", data, start, end);
                    break;

                case ReportType.ExpiringPCFNotications:
                    await _expiringPcfNotificationsJob.RunAsync();
                    
                    break;

                // TODO: other reports that don’t need customer/dateRange can have simpler calls
                default:
                    throw new NotSupportedException($"Report type not implemented: {req.ReportType}");
            }
        }

        private async Task EmailAsync(string to, string title, List<Dictionary<string, object>> rows,
            DateTime start, DateTime end)
        {
            var dt = ToDataTable(rows);
            var xlsx = BuildExcel(dt, $"{title} {start:yyyy-MM-dd}–{end.AddDays(-1):yyyy-MM-dd}");

            var subject = $"{title} ({start:yyyy-MM-dd}..{end.AddDays(-1):yyyy-MM-dd})";
            var body = $"<p>Attached: {title} from {start:yyyy-MM-dd} to {end.AddDays(-1):yyyy-MM-dd}.</p>";

            await _email.SendAsync(
                to, subject, body,
                new[]
                {
                    (FileName: $"{title.Replace(' ', '_')}_{start:yyyy-MM-dd}_{end.AddDays(-1):yyyy-MM-dd}.xlsx",
                        Bytes: xlsx,
                        ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                });
        }

        // ——— helpers ———

        private static (DateTime StartUtc, DateTime EndUtcExclusive) ToDateRange(string? code, TimeZoneInfo tz)
        {
            // interpret in local TZ, then convert to UTC for the DB if needed
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);

            var firstThis = new DateTime(nowLocal.Year, nowLocal.Month, 1);
            var firstNext = firstThis.AddMonths(1);
            var firstPrev = firstThis.AddMonths(-1);

            (DateTime lStart, DateTime lEnd) = code switch
            {
                nameof(DateRangeCodeType.PriorMonth) => (firstPrev, firstThis),
                nameof(DateRangeCodeType.CurrentMonth) => (firstThis, nowLocal.Date.AddDays(1)),
                nameof(DateRangeCodeType.PriorAndCurrentMonth) => (firstPrev, nowLocal.Date.AddDays(1)),
                nameof(DateRangeCodeType.AllDates) => (DateTime.MinValue, DateTime.MaxValue),
                _ => (firstThis, nowLocal.Date.AddDays(1))
            };

            var sUtc = TimeZoneInfo.ConvertTimeToUtc(lStart, tz);
            var eUtc = lEnd == DateTime.MaxValue ? DateTime.MaxValue : TimeZoneInfo.ConvertTimeToUtc(lEnd, tz);
            return (sUtc, eUtc);
        }

        private static void Normalize(ReportType type, string? customerId, string? dateRangeCode,
            out string? cust, out string? range)
        {
            bool scoped = type is ReportType.InvoicedAccounts or ReportType.Shipments;

            cust = scoped && !string.IsNullOrWhiteSpace(customerId) ? customerId.Trim() : null;

            range = scoped && !string.IsNullOrWhiteSpace(dateRangeCode)
                           && Enum.TryParse<DateRangeCodeType>(dateRangeCode, true, out _)
                ? dateRangeCode
                : null;
        }

        // reuse your existing helpers
        private static System.Data.DataTable ToDataTable(List<Dictionary<string, object>> data)
            => throw new NotImplementedException();

        private static byte[] BuildExcel(System.Data.DataTable dt, string sheetName)
            => throw new NotImplementedException();

        // SQL datetime bounds
        // private static readonly DateTime SqlMin = SqlDateTime.MinValue.Value; // 1753-01-01 00:00:00
        // private static readonly DateTime SqlMax = SqlDateTime.MaxValue.Value; // 9999-12-31 23:59:59.997
        private static readonly DateTime BusinessMinLocal = new DateTime(2022, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);

        private static readonly DateTime SqlMin = SqlDateTime.MinValue.Value; // 1753-01-01 00:00:00
        private static readonly DateTime SqlMax = SqlDateTime.MaxValue.Value; // 9999-12-31 23:59:59.997

        private static DateTime ClampToSqlDateTime(DateTime dt)
        {
            // First clamp to SQL's valid range, then to your business min
            if (dt < SqlMin) dt = SqlMin;
            if (dt > SqlMax) dt = SqlMax;
            if (dt < BusinessMinLocal) dt = BusinessMinLocal;
            return dt;
        }

        private static (DateTime BeginLocal, DateTime EndLocalExclusive)
            ToDateRangeLocal(DateRangeCodeType code, TimeZoneInfo tz)
        {
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            var firstThis = new DateTime(nowLocal.Year, nowLocal.Month, 1);
            var firstNext = firstThis.AddMonths(1);
            var firstPrev = firstThis.AddMonths(-1);

            return code switch
            {
                DateRangeCodeType.PriorMonth => (firstPrev, firstThis),
                DateRangeCodeType.CurrentMonth => (firstThis, nowLocal.Date.AddDays(1)),
                DateRangeCodeType.PriorAndCurrentMonth => (firstPrev, nowLocal.Date.AddDays(1)),
                DateRangeCodeType.AllDates => (DateTime.MinValue, DateTime.MaxValue),
                _ => (firstThis, nowLocal.Date.AddDays(1))
            };
        }
        private async Task RunInvoicedAccountsAsync(ReportRequest req, TimeZoneInfo tz)
        {
            var ctx = await _userCtx.ResolveByEmailAsync(req.Email)
                      ?? throw new InvalidOperationException($"No user context for {req.Email}");

            // Date range
            var code = Enum.TryParse<DateRangeCodeType>(req.DateRangeCode ?? "", true, out var parsed)
                ? parsed
                : DateRangeCodeType.CurrentMonth;
            var (beginLocal, endLocalExclusive) = ToDateRangeLocal(code, tz);

            // Fetch data
            var rows = await _invoicedAccounts.GetAsync(
                repCode: ctx.RepCode,
                allowedRegions: ctx.AllowedRegions?.ToList(),
                customerId: req.CustomerId,
                beginLocal: ClampToSqlDateTime(beginLocal),
                endLocalExclusive: endLocalExclusive);

            // Build Excel
            var title = "Invoiced Accounts";
            var subtitle = $"{beginLocal:yyyy-MM-dd} – {endLocalExclusive.AddDays(-1):yyyy-MM-dd}";
            var bytes = _excel.Export(rows, new ExcelExportOptions(
                WorksheetName: "InvoicedAccounts",
                Title: title,
                Subtitle: subtitle,
                DateColumns: new[] { "InvoiceDate" }, // <- adjust to your InvoiceRptDetail props
                CurrencyColumns: new[] { "Amount" } // <- adjust to your InvoiceRptDetail props
            ));

            // Send email
            var subject = $"{title} ({subtitle})";
            var body = $"<p>Attached: {title} for {subtitle}.</p>";

            await _email.SendAsync(
                toEmail: req.Email,
                subject: subject,
                htmlBody: body,
                attachments: new[]
                {
                    (FileName: $"Invoiced_Accounts_{ClampToSqlDateTime(beginLocal):yyyy-MM-dd}_{endLocalExclusive.AddDays(-1):yyyy-MM-dd}.xlsx",
                        Bytes: bytes,
                        ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                });
        }
    }
}
