namespace RepPortal.Services;

using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Data;
// using System.Data.SqlClient; // No longer needed if factory provides IDbConnection
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Http; // For IHttpContextAccessor

using RepPortal.Services;
using RepPortal.Models;
using RepPortal.Data;

public interface IActivityLogService
{
    Task LogLoginAsync(string repCode, string? ipAddress, string? userAgent);
    Task LogReportUsageAsync(string reportName, string parameters);
    Task LogFileDownloadAsync(string fileName);
    Task LogReportUsageActivityAsync(string reportName, string parameters);
}

// --- Assume these services exist and are registered ---
// In YourProject.Services namespace or similar

public class ActivityLogService : IActivityLogService
{
    private readonly ILogger<ActivityLogService> _logger;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IRepCodeContext _repCodeContext; // User's service
    private readonly IDbConnectionFactory _dbConnectionFactory; // User's factory
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ActivityLogService(
        ILogger<ActivityLogService> logger,
        AuthenticationStateProvider authenticationStateProvider,
        IRepCodeContext repCodeContext,
        IDbConnectionFactory dbConnectionFactory, // Inject the factory
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _authenticationStateProvider = authenticationStateProvider;
        _repCodeContext = repCodeContext;
        _dbConnectionFactory = dbConnectionFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    // Helper to get current user's context considering impersonation
    private async Task<(string? EffectiveRepCode, string? AdminRepCode)> GetCurrentRepAndAdminAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        // --- Get Logged-in User's RepCode ---
        // IMPORTANT: Ensure "RepCode" is the correct claim type name you are using.
        var loggedInUserRepCode = user.FindFirstValue("RepCode");

        if (string.IsNullOrEmpty(loggedInUserRepCode))
        {
            _logger.LogWarning("Could not determine logged-in user's RepCode from claims.");
            // Decide how to handle - maybe return nulls, maybe throw?
            // Returning nulls means logging might be skipped later if RepCode is required.
            return (null, null);
        }

        // --- Get potentially impersonated RepCode ---
        var currentContextRepCode = _repCodeContext.CurrentRepCode; // Assuming this property exists and is up-to-date

        // --- Determine Effective Rep and Admin ---
        if (string.IsNullOrEmpty(currentContextRepCode) || loggedInUserRepCode.Equals(currentContextRepCode, StringComparison.OrdinalIgnoreCase))
        {
            // Case 1: Not impersonating (or context isn't set)
            // Logged-in user is the effective user, no admin involved.
            string? adminUser = _repCodeContext.CurrentLastName;
            return (loggedInUserRepCode, adminUser);
        }
        else
        {
            // Case 2: Impersonating
            // The context code is the user being acted upon (effective).
            // The logged-in user is the administrator performing the action.
            return (currentContextRepCode, loggedInUserRepCode);
        }
    }

    // --- Logging Methods ---

    public async Task LogLoginAsync(string repCode, string? ipAddress, string? userAgent)
    {
        // Login logs the actual user logging in. It does NOT use RepCodeContext for the main RepCode.
        // It uses the repCode passed in, which should come directly from the user object after successful authentication.
        const string sql = @"
            INSERT INTO RepLoginHistory (RepCode, LoginTime, IPAddress, UserAgent)
            VALUES (@RepCode, @LoginTime, @IPAddress, @UserAgent);";
        try
        {
            // Use the factory to get a connection
            using var connection = _dbConnectionFactory.CreateRepConnection(); // Use the factory method
            await connection.ExecuteAsync(sql, new
            {
                RepCode = repCode, // Use the RepCode of the user who just logged in
                LoginTime = DateTime.Now, // Use Local
                IPAddress = ipAddress ?? "Unknown",
                UserAgent = userAgent ?? "Unknown"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dapper: Failed to log login history for RepCode {RepCode}", repCode);
        }
    }

    public async Task LogReportUsageAsync(string reportName, string parameters)
    {
        var (effectiveRepCode, adminRepCode) = await GetCurrentRepAndAdminAsync();

        if (string.IsNullOrEmpty(effectiveRepCode))
        {
            _logger.LogWarning("Dapper: Cannot log report usage. Effective RepCode could not be determined.");
            return; // Don't log if we don't know who the effective user is
        }

        const string sql = @"
            INSERT INTO ReportUsageHistory (RepCode, ReportName, RunTime, Parameters, AdminUser)
            VALUES (@RepCode, @ReportName, @RunTime, @Parameters, @AdminUser);";
        try
        {
            using var connection = _dbConnectionFactory.CreateRepConnection();
            await connection.ExecuteAsync(sql, new
            {
                RepCode = effectiveRepCode, // The rep being acted upon/impersonated
                ReportName = reportName,
                RunTime = DateTime.Now,
                Parameters = parameters, // Ensure column allows length or handle truncation
                AdminUser = adminRepCode // The logged-in admin (null if not impersonating)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dapper: Failed to log report usage history for EffectiveRepCode {RepCode}, Report {ReportName}", effectiveRepCode, reportName);
        }
    }

    public async Task LogFileDownloadAsync(string fileName)
    {
        var (effectiveRepCode, adminRepCode) = await GetCurrentRepAndAdminAsync();

        if (string.IsNullOrEmpty(effectiveRepCode))
        {
            _logger.LogWarning("Dapper: Cannot log file download. Effective RepCode could not be determined.");
            return;
        }

        // Assuming table name is FileDownloadHistory and columns match
        const string sql = @"
            INSERT INTO FileDownloadHistory (RepCode, DownloadTime, FileName, AdminUser)
            VALUES (@RepCode, @DownloadTime, @FileName, @AdminUser);";
        try
        {
            using var connection = _dbConnectionFactory.CreateRepConnection();
            await connection.ExecuteAsync(sql, new
            {
                RepCode = effectiveRepCode,
                DownloadTime = DateTime.Now,
                FileName = fileName,
                AdminUser = adminRepCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dapper: Failed to log file download history for EffectiveRepCode {RepCode}, File {FileName}", effectiveRepCode, fileName);
        }
    }

    public async Task LogReportUsageActivityAsync(string reportName, string parameters)
    {
        var (effectiveRepCode, adminRepCode) = await GetCurrentRepAndAdminAsync();

        if (string.IsNullOrEmpty(effectiveRepCode))
        {
            _logger.LogWarning("Dapper: Cannot log report usage activity. Effective RepCode could not be determined.");
            return;
        }

        const string sql = @"
            INSERT INTO ReportUsageHistory (RepCode, ReportName, RunTime, Parameters, AdminUser)
            VALUES (@RepCode, @ReportName, @RunTime, @Parameters, @AdminUser);";
        try
        {
            using var connection = _dbConnectionFactory.CreateRepConnection();
            await connection.ExecuteAsync(sql, new
            {
                RepCode = effectiveRepCode,
                ReportName = reportName,
                RunTime = DateTime.Now,
                Parameters = parameters,
                AdminUser = adminRepCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dapper: Failed to log report usage activity for EffectiveRepCode {RepCode}, Report {ReportName}", effectiveRepCode, reportName);
        }
    }
}