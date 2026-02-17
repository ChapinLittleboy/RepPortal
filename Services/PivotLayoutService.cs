using Dapper;
using RepPortal.Data;

namespace RepPortal.Services;

public interface IPivotLayoutService
{
    Task<List<string>> GetReportNamesAsync(string pageKey);
    Task<string?> GetReportAsync(string pageKey, string reportName);
    Task SaveReportAsync(string pageKey, string reportName, string report);
    Task RenameReportAsync(string pageKey, string oldName, string newName);
    Task RemoveReportAsync(string pageKey, string reportName);
}

public class PivotLayoutService : IPivotLayoutService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IRepCodeContext _repCodeContext;
    private readonly ILogger<PivotLayoutService> _logger;

    public PivotLayoutService(
        IDbConnectionFactory dbFactory,
        IRepCodeContext repCodeContext,
        ILogger<PivotLayoutService> logger)
    {
        _dbFactory = dbFactory;
        _repCodeContext = repCodeContext;
        _logger = logger;
    }

    public async Task<List<string>> GetReportNamesAsync(string pageKey)
    {
        const string sql = @"
            SELECT ReportName
            FROM   PivotLayouts
            WHERE  RepCode = @RepCode
              AND  PageKey = @PageKey
            ORDER BY ReportName;";

        try
        {
            using var connection = _dbFactory.CreateRepConnection();
            var names = await connection.QueryAsync<string>(sql, new
            {
                RepCode = _repCodeContext.CurrentRepCode,
                PageKey = pageKey
            });
            return names.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to fetch pivot layout names for RepCode {RepCode}, Page {PageKey}",
                _repCodeContext.CurrentRepCode, pageKey);
            return new List<string>();
        }
    }

    public async Task<string?> GetReportAsync(string pageKey, string reportName)
    {
        const string sql = @"
            SELECT Report
            FROM   PivotLayouts
            WHERE  RepCode    = @RepCode
              AND  PageKey    = @PageKey
              AND  ReportName = @ReportName;";

        try
        {
            using var connection = _dbFactory.CreateRepConnection();
            return await connection.QueryFirstOrDefaultAsync<string>(sql, new
            {
                RepCode = _repCodeContext.CurrentRepCode,
                PageKey = pageKey,
                ReportName = reportName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to load pivot layout '{ReportName}' for RepCode {RepCode}, Page {PageKey}",
                reportName, _repCodeContext.CurrentRepCode, pageKey);
            return null;
        }
    }

    public async Task SaveReportAsync(string pageKey, string reportName, string report)
    {
        const string sql = @"
            MERGE PivotLayouts AS target
            USING (SELECT @RepCode AS RepCode, @PageKey AS PageKey, @ReportName AS ReportName) AS source
               ON target.RepCode    = source.RepCode
              AND target.PageKey    = source.PageKey
              AND target.ReportName = source.ReportName
            WHEN MATCHED THEN
                UPDATE SET Report    = @Report,
                           UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (RepCode, PageKey, ReportName, Report, CreatedAt, UpdatedAt)
                VALUES (@RepCode, @PageKey, @ReportName, @Report, GETDATE(), GETDATE());";

        try
        {
            using var connection = _dbFactory.CreateRepConnection();
            await connection.ExecuteAsync(sql, new
            {
                RepCode = _repCodeContext.CurrentRepCode,
                PageKey = pageKey,
                ReportName = reportName,
                Report = report
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to save pivot layout '{ReportName}' for RepCode {RepCode}, Page {PageKey}",
                reportName, _repCodeContext.CurrentRepCode, pageKey);
        }
    }

    public async Task RenameReportAsync(string pageKey, string oldName, string newName)
    {
        const string sql = @"
            UPDATE PivotLayouts
            SET    ReportName = @NewName,
                   UpdatedAt  = GETDATE()
            WHERE  RepCode    = @RepCode
              AND  PageKey    = @PageKey
              AND  ReportName = @OldName;";

        try
        {
            using var connection = _dbFactory.CreateRepConnection();
            await connection.ExecuteAsync(sql, new
            {
                RepCode = _repCodeContext.CurrentRepCode,
                PageKey = pageKey,
                OldName = oldName,
                NewName = newName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to rename pivot layout '{OldName}' to '{NewName}' for RepCode {RepCode}, Page {PageKey}",
                oldName, newName, _repCodeContext.CurrentRepCode, pageKey);
        }
    }

    public async Task RemoveReportAsync(string pageKey, string reportName)
    {
        const string sql = @"
            DELETE FROM PivotLayouts
            WHERE  RepCode    = @RepCode
              AND  PageKey    = @PageKey
              AND  ReportName = @ReportName;";

        try
        {
            using var connection = _dbFactory.CreateRepConnection();
            await connection.ExecuteAsync(sql, new
            {
                RepCode = _repCodeContext.CurrentRepCode,
                PageKey = pageKey,
                ReportName = reportName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to remove pivot layout '{ReportName}' for RepCode {RepCode}, Page {PageKey}",
                reportName, _repCodeContext.CurrentRepCode, pageKey);
        }
    }
}
