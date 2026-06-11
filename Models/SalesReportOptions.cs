namespace RepPortal.Models;

public class SalesReportOptions
{
    public bool ShowCurrentMonthSales { get; set; } = true;
    public bool IncludeCurrentMonthInCurrentYearSales { get; set; } = true;
}
