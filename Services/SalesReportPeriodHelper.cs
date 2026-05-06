using System;
using System.Collections.Generic;
using System.Linq;

namespace RepPortal.Services;

internal sealed class SalesReportPeriod
{
    public required string Prefix { get; init; }
    public required int CurrentYear { get; init; }
    public required DateTime HistoryStart { get; init; }
    public required List<string> PriorYear3Months { get; init; }
    public required List<string> PriorYear2Months { get; init; }
    public required List<string> PriorYear1Months { get; init; }
    public required List<string> CurrentYearMonths { get; init; }

    public string PriorYear3Label => $"{Prefix}{CurrentYear - 3}";
    public string PriorYear2Label => $"{Prefix}{CurrentYear - 2}";
    public string PriorYear1Label => $"{Prefix}{CurrentYear - 1}";
    public string CurrentYearLabel => $"{Prefix}{CurrentYear}";

    public IReadOnlyList<string> YearLabels =>
        new[] { PriorYear3Label, PriorYear2Label, PriorYear1Label, CurrentYearLabel };

    public IReadOnlyList<string> AllMonths =>
        PriorYear3Months
            .Concat(PriorYear2Months)
            .Concat(PriorYear1Months)
            .Concat(CurrentYearMonths)
            .ToList();
}

internal static class SalesReportPeriodHelper
{
    public static SalesReportPeriod Create(string? yearMode, DateTime? today = null)
    {
        var asOf = today?.Date ?? DateTime.Today;
        var useCalendarYear = string.Equals(yearMode, "CY", StringComparison.OrdinalIgnoreCase);

        var prefix = useCalendarYear ? "CY" : "FY";
        var currentYear = useCalendarYear
            ? asOf.Year
            : asOf.Month >= 9 ? asOf.Year + 1 : asOf.Year;

        var historyStart = useCalendarYear
            ? new DateTime(currentYear - 3, 1, 1)
            : new DateTime(currentYear - 4, 9, 1);

        var currentMonthCount = useCalendarYear
            ? asOf.Month
            : asOf.Month >= 9 ? asOf.Month - 8 : asOf.Month + 4;

        var allMonths = Enumerable.Range(0, 36 + currentMonthCount)
            .Select(i => historyStart.AddMonths(i).ToString("MMM") + historyStart.AddMonths(i).Year)
            .ToList();

        return new SalesReportPeriod
        {
            Prefix = prefix,
            CurrentYear = currentYear,
            HistoryStart = historyStart,
            PriorYear3Months = allMonths.Take(12).ToList(),
            PriorYear2Months = allMonths.Skip(12).Take(12).ToList(),
            PriorYear1Months = allMonths.Skip(24).Take(12).ToList(),
            CurrentYearMonths = allMonths.Skip(36).Take(currentMonthCount).ToList()
        };
    }
}
