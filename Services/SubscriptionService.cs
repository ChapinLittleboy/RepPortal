using System;
using Hangfire;
using Microsoft.Extensions.Logging;
using RepPortal.Models;

namespace RepPortal.Services
{
    public sealed class SubscriptionService
    {
        private readonly ReportRunner _runner;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(ReportRunner runner, ILogger<SubscriptionService> logger)
        {
            _runner = runner;
            _logger = logger;
        }

        // ----- Public API (call these from your UI / razor page) ----------------------------

        // Preferred: call with concrete fields
        public void RegisterOrUpdateJob(
            ReportType reportType,
            string email,
            string? customerId,
            string? dateRangeCode,   // pass enum.ToString() from the page, e.g. DateRangeCodeType.CurrentMonth.ToString()
            string cron,
            string timeZoneId)
        {
            Normalize(reportType, customerId, dateRangeCode, out var cust, out var range);
            var id = BuildJobId(reportType, email, cust, range);
            var tz = SafeFindTimeZone(timeZoneId);

            RecurringJob.AddOrUpdate<ReportRunner>(
                recurringJobId: id,
                queue: "reports",
                methodCall: r => r.RunAsync(new ReportRequest(reportType, email, cust, range)),
                cronExpression: cron,
                options: new RecurringJobOptions
                {
                    TimeZone = tz,
                    MisfireHandling = MisfireHandlingMode.Relaxed
                });

            _logger.LogInformation("Upserted job {JobId} for {Email} ({Type}) cron={Cron} tz={Tz}",
                id, email, reportType, cron, tz.Id);
        }

        // Convenience overload: schedule from your view model used on the page list
        public void RegisterOrUpdateJob(SubscriptionVm vm)
            => RegisterOrUpdateJob(vm.ReportType, vm.Email, vm.CustomerId, vm.DateRangeCode, vm.Cron, vm.TimeZone);

        // Remove by concrete fields (recommended)
        public void RemoveJob(ReportType reportType, string email, string? customerId, string? dateRangeCode)
        {
            Normalize(reportType, customerId, dateRangeCode, out var cust, out var range);
            var id = BuildJobId(reportType, email, cust, range);
            RecurringJob.RemoveIfExists(id);
            _logger.LogInformation("Removed job {JobId} for {Email} ({Type})", id, email, reportType);
        }

        // Convenience overload: remove from VM
        public void RemoveJob(SubscriptionVm vm) => RemoveJob(vm.ReportType, vm.Email, vm.CustomerId, vm.DateRangeCode);

        // ----- Helpers ---------------------------------------------------------------------

        private static string BuildJobId(ReportType type, string email, string? customerId, string? dateRangeCode)
            => $"subs:{type}:{email}:{(string.IsNullOrWhiteSpace(customerId) ? "ALL" : customerId)}:{(string.IsNullOrWhiteSpace(dateRangeCode) ? "DEFAULT" : dateRangeCode)}";

        private static void Normalize(ReportType type, string? customerId, string? dateRangeCode,
                                      out string? normCustomerId, out string? normDateRangeCode)
        {
            // Only these report types allow customer/date range
            bool scoped = type is ReportType.InvoicedAccounts or ReportType.Shipments;

            normCustomerId = scoped && !string.IsNullOrWhiteSpace(customerId) ? customerId.Trim() : null;

            normDateRangeCode = scoped
                && !string.IsNullOrWhiteSpace(dateRangeCode)
                && Enum.TryParse<DateRangeCodeType>(dateRangeCode, true, out _)
                ? dateRangeCode!.Trim()
                : null;
        }

        private static TimeZoneInfo SafeFindTimeZone(string tzId)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
                catch { return TimeZoneInfo.Utc; }
            }
        }
    }

    // Mirror your page VM here for the overloads; or put this in a shared namespace and reuse.
    public sealed class SubscriptionVm
    {
        public string Id { get; set; } = default!;
        public ReportType ReportType { get; set; }
        public string Email { get; set; } = default!;
        public string? CustomerId { get; set; }
        public string? DateRangeCode { get; set; } // store enum.ToString() here
        public string Cron { get; set; } = default!;
        public string TimeZone { get; set; } = default!;
    }
}
