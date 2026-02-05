using System.Runtime.InteropServices;
using Hangfire;
using RepPortal.Services;

namespace RepPortal.Models;

public static class ReportSubscriptions
{
    private static bool IsCustomerScoped(ReportType rt)
        => rt is ReportType.InvoicedAccounts or ReportType.Shipments;
    private static bool IsDateRangeScoped(ReportType rt)
        => rt is ReportType.InvoicedAccounts or ReportType.Shipments;

    private static void Normalize(ReportType type, string? customerId, string? dateRangeCode,
        out string? normalizedCustomerId, out string? normalizedDateRangeCode)
    {
        // Only allow CustomerId when report type supports it
        normalizedCustomerId = IsCustomerScoped(type) ? TrimOrNull(customerId) : null;

        // Only allow DateRangeCode when report type supports it, and it must be a valid enum
        if (IsDateRangeScoped(type) && !string.IsNullOrWhiteSpace(dateRangeCode)
                                    && Enum.TryParse<DateRangeCodeType>(dateRangeCode, ignoreCase: true, out var _))
        {
            normalizedDateRangeCode = dateRangeCode;
        }
        else
        {
            normalizedDateRangeCode = null; // treat as DEFAULT for ID purposes
        }
    }

    private static string? TrimOrNull(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();



    public static void Upsert(
        ReportType type,
        string email,
        string? customerId,
        string? dateRangeCode,
        string cron,
        string timeZoneId = "Eastern Standard Time",
        string queue = "reports",
        ILogger? logger = null)
    {
        Normalize(type, customerId, dateRangeCode, out var cust, out var range);

        // Build a stable, unique ID—include range so multiple date ranges can coexist
        var id = $"subs:{type}:{email}:{(cust ?? "ALL")}:{(range ?? "DEFAULT")}";

        if (cust != customerId || range != dateRangeCode)
        {
            logger?.LogInformation("Normalized subscription params for {Email}/{Type}: CustomerId={CustomerId} DateRange={DateRange}",
                email, type, cust ?? "null", range ?? "null");
        }

        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        else
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");  //America/New_York
        }
        RecurringJob.AddOrUpdate<ReportRunner>(
            recurringJobId: id,
            queue: queue,
            methodCall: r => r.RunAsync(new ReportRequest(type, email, cust, range)),
            cronExpression: cron,
            options: new RecurringJobOptions
            {
                TimeZone = tz,
                MisfireHandling = MisfireHandlingMode.Relaxed
            });
    }

    public static void Remove(ReportType type, string email, int? customerId)
        => RecurringJob.RemoveIfExists($"subs:{type}:{email}:{(customerId?.ToString() ?? "ALL")}");



}