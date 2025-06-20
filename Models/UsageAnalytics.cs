// RepPortal.Models.UsageAnalytics.cs
// Description: Contains all the Plain Old C# Object (POCO) models required for the usage analytics dashboard.
// These classes represent the data structure of the ReportUsageHistory table and the aggregated summary data.

namespace RepPortal.Models
{
    /// <summary>
    /// Represents a single entry in the ReportUsageHistory table.
    /// This model maps directly to the columns in your database table.
    /// </summary>
    public class ReportUsageHistory
    {
        public int UsageId { get; set; }
        public string UserName { get; set; }
        public DateTime UsageDate { get; set; }
        public string RepCode { get; set; }
        public string Feature { get; set; }
        public string? SearchCriteria { get; set; }
    }

    /// <summary>
    /// Represents a summary of how many times each feature/report has been used.
    /// Used for the "Feature Usage" chart and grid.
    /// </summary>
    public class FeatureUsageSummary
    {
        public string Feature { get; set; }
        public int UsageCount { get; set; }
    }

    /// <summary>
    /// Represents a summary of site usage aggregated by RepCode.
    /// Used for the "RepCode Activity" chart and grid.
    /// </summary>
    public class RepCodeUsageSummary
    {
        public string RepCode { get; set; }
        public int UsageCount { get; set; }
    }

    /// <summary>
    /// Represents the count of unique users for each RepCode.
    /// Used for the "Users per RepCode" grid.
    /// </summary>
    public class UsersPerRepCodeSummary
    {
        public string RepCode { get; set; }
        public int UniqueUserCount { get; set; }
    }

    /// <summary>
    /// Represents the total usage count for a specific date.
    /// Used for the "Usage Over Time" line chart.
    /// </summary>
    public class DailyUsageSummary
    {
        public DateTime Date { get; set; }
        public int UsageCount { get; set; }
    }

    /// <summary>
    /// A comprehensive view model to hold all the data for the dashboard.
    /// This object will be populated by the analytics service and passed to the Blazor component.
    /// </summary>
    public class UsageDashboardViewModel
    {
        public int TotalPageViews { get; set; }
        public int UniqueUsers { get; set; }
        public int UniqueRepCodes { get; set; }
        public List<FeatureUsageSummary> FeatureUsage { get; set; } = new();
        public List<RepCodeUsageSummary> RepCodeUsage { get; set; } = new();
        public List<UsersPerRepCodeSummary> UsersPerRepCode { get; set; } = new();
        public List<DailyUsageSummary> UsageOverTime { get; set; } = new();
    }
}
