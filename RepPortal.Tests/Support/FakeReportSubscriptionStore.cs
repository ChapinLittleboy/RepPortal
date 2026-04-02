using RepPortal.Services;

namespace RepPortal.Tests.Support;

internal sealed class FakeReportSubscriptionStore : IReportSubscriptionStore
{
    public List<ReportSubscriptionEntry> Entries { get; } = new();
    public string? LastRemovedId { get; private set; }
    public ReportSubscriptionEntry? LastUpserted { get; private set; }

    public List<ReportSubscriptionEntry> GetSubscriptionsForUser(string email)
        => Entries.Where(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)).ToList();

    public void Upsert(ReportSubscriptionEntry entry)
    {
        LastUpserted = entry;
        var existing = Entries.FirstOrDefault(x => x.Id == entry.Id && !string.IsNullOrWhiteSpace(entry.Id));
        if (existing != null)
            Entries.Remove(existing);

        Entries.Add(new ReportSubscriptionEntry
        {
            Id = string.IsNullOrWhiteSpace(entry.Id)
                ? $"subs:{entry.ReportType}:{entry.Email}:{entry.CustomerId ?? "ALL"}:{entry.DateRangeCode ?? "DEFAULT"}"
                : entry.Id,
            ReportType = entry.ReportType,
            Email = entry.Email,
            CustomerId = entry.CustomerId,
            DateRangeCode = entry.DateRangeCode,
            Cron = entry.Cron,
            TimeZone = entry.TimeZone
        });
    }

    public void Remove(string id)
    {
        LastRemovedId = id;
        Entries.RemoveAll(x => x.Id == id);
    }
}
