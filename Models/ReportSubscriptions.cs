using Hangfire;
using RepPortal.Services;

namespace RepPortal.Models;

public static class ReportSubscriptions
{
    public static void Upsert(
        ReportType type,
        string email,
        string? customerId,
        string? dateRangeCode,
        string cron,                // e.g. "0 9 * * 1-5" (weekdays 9:00)
        string timeZoneId = "America/New_York",
        string queue = "reports")
    {
        string id = $"subs:{type}:{email}:{(customerId ?? "ALL")}:{(dateRangeCode ?? "DEFAULT")}";


        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        RecurringJob.AddOrUpdate<ReportRunner>(
            recurringJobId: id,
            queue: queue,
            methodCall: r => r.RunAsync(new ReportRequest(type, email, customerId, dateRangeCode)),
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