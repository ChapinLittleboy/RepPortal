using Hangfire;
using Hangfire.Storage;
using RepPortal.Models;

namespace RepPortal.Services;

public sealed class ReportSubscriptionEntry
{
    public string Id { get; set; } = default!;
    public ReportType ReportType { get; set; }
    public string Email { get; set; } = default!;
    public string? CustomerId { get; set; }
    public string? DateRangeCode { get; set; }
    public string Cron { get; set; } = default!;
    public string TimeZone { get; set; } = default!;
}

public interface IReportSubscriptionStore
{
    List<ReportSubscriptionEntry> GetSubscriptionsForUser(string email);
    void Upsert(ReportSubscriptionEntry entry);
    void Remove(string id);
}

public sealed class HangfireReportSubscriptionStore : IReportSubscriptionStore
{
    public List<ReportSubscriptionEntry> GetSubscriptionsForUser(string email)
    {
        using var conn = JobStorage.Current.GetConnection();
        var jobs = conn.GetRecurringJobs()
            .Where(j => j.Id.StartsWith("subs:") && j.Id.Contains(email, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return jobs.Select(Parse).ToList();
    }

    public void Upsert(ReportSubscriptionEntry entry)
    {
        ReportSubscriptions.Upsert(
            entry.ReportType,
            entry.Email,
            entry.CustomerId,
            entry.DateRangeCode,
            entry.Cron,
            entry.TimeZone);
    }

    public void Remove(string id)
        => RecurringJob.RemoveIfExists(id);

    private static ReportSubscriptionEntry Parse(RecurringJobDto job)
    {
        var parts = job.Id.Split(':');
        return new ReportSubscriptionEntry
        {
            Id = job.Id,
            ReportType = Enum.TryParse<ReportType>(parts[1], out var rt) ? rt : ReportType.MonthlySales,
            Email = parts[2],
            CustomerId = parts.Length > 3 && parts[3] != "ALL" ? parts[3] : null,
            DateRangeCode = parts.Length > 4 && parts[4] != "DEFAULT" ? parts[4] : null,
            Cron = job.Cron,
            TimeZone = job.TimeZoneId
        };
    }
}
