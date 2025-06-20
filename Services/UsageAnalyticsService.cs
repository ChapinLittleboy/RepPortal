// RepPortal.Services.UsageAnalyticsService.cs
// Description: This service class contains all the data access logic for the usage dashboard.
// It uses Dapper to execute SQL queries against the ReportUsageHistory table.

using Dapper;
using RepPortal.Models;
using System.Data;
using Microsoft.Data.SqlClient;

namespace RepPortal.Services
{
    public interface IUsageAnalyticsService
    {
        Task<UsageDashboardViewModel> GetUsageDashboardDataAsync();
    }

    public class UsageAnalyticsService : IUsageAnalyticsService
    {
        private readonly string _connectionString;

        /// <summary>
        /// The service is initialized with the database connection string.
        /// This should be injected via DI from your app's configuration.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        public UsageAnalyticsService(IConfiguration configuration)
        {
            // Best practice: Store your connection string in appsettings.json
            _connectionString = configuration.GetConnectionString("RepPortalConnection");
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        /// <summary>
        /// Fetches and computes all necessary analytics data for the dashboard.
        /// This single method makes it efficient to load the page with one data call.
        /// </summary>
        /// <returns>A view model containing all data for the dashboard.</returns>
        public async Task<UsageDashboardViewModel> GetUsageDashboardDataAsync()
        {
            var viewModel = new UsageDashboardViewModel();

            using (var connection = CreateConnection())
            {
                // SQL query to gather multiple aggregations in one go for efficiency.
                // This reduces the number of round trips to the database.
                var sql = @"
                    -- 1. Feature Usage Summary
                    SELECT 
                       ReportName as Feature, 
                        COUNT(*) AS UsageCount 
                    FROM ReportUsageHistory 
                    GROUP BY ReportName;

                    -- 2. RepCode Usage Summary
                    SELECT 
                        RepCode, 
                        COUNT(*) AS UsageCount 
                    FROM ReportUsageHistory 
                    GROUP BY RepCode;

                    -- 3. Unique Users Per RepCode
                    SELECT 
                        RepCode, 
                        COUNT(DISTINCT AdminUser) AS UniqueUserCount 
                    FROM ReportUsageHistory 
                    GROUP BY RepCode;

                    -- 4. Daily Usage for Trend Analysis
                    SELECT 
                        CAST(RunTime AS DATE) AS Date, 
                        COUNT(*) AS UsageCount 
                    FROM ReportUsageHistory 
                    GROUP BY CAST(RunTime AS DATE);

                    -- 5. Overall Statistics
                    SELECT
                        (SELECT COUNT(*) FROM ReportUsageHistory) AS TotalPageViews,
                        (SELECT COUNT(DISTINCT AdminUser) FROM ReportUsageHistory) AS UniqueUsers,
                        (SELECT COUNT(DISTINCT RepCode) FROM ReportUsageHistory) AS UniqueRepCodes;
                ";

                using (var multi = await connection.QueryMultipleAsync(sql))
                {
                    viewModel.FeatureUsage = (await multi.ReadAsync<FeatureUsageSummary>()).ToList();
                    viewModel.RepCodeUsage = (await multi.ReadAsync<RepCodeUsageSummary>()).ToList();
                    viewModel.UsersPerRepCode = (await multi.ReadAsync<UsersPerRepCodeSummary>()).ToList();
                    viewModel.UsageOverTime = (await multi.ReadAsync<DailyUsageSummary>()).ToList();

                    var overallStats = await multi.ReadSingleAsync();
                    viewModel.TotalPageViews = overallStats.TotalPageViews;
                    viewModel.UniqueUsers = overallStats.UniqueUsers;
                    viewModel.UniqueRepCodes = overallStats.UniqueRepCodes;
                }
            }
            return viewModel;
        }
    }
}
