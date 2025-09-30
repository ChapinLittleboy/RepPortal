namespace RepPortal.Services;

using Hangfire;

public sealed class SubscriptionService
{
    private readonly ReportRunner _runner;

    public SubscriptionService(ReportRunner runner)
    {
        _runner = runner;
    }

    public void RegisterOrUpdateJob(long subscriptionId, string cron, string timeZoneId)
    {
        // Build a stable Hangfire job id so updates overwrite the same job
        string jobId = $"report-sub-{subscriptionId}";

        var tz = SafeFindTimeZone(timeZoneId);

        // Put reporting work on the "reports" queue; pass timezone so 9am means 9am local
        RecurringJob.AddOrUpdate<ReportRunner>(
            recurringJobId: jobId,
            methodCall: r => r.RunSubscriptionAsync(subscriptionId),
            cronExpression: cron,
            timeZone: tz,
            queue: "reports"
        );
    }

    public void RemoveJob(long subscriptionId)
    {
        string jobId = $"report-sub-{subscriptionId}";
        RecurringJob.RemoveIfExists(jobId);
    }

    private static TimeZoneInfo SafeFindTimeZone(string tzId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { return TimeZoneInfo.Utc; }
    }
}
