using System.Data;
using Dapper;
using RepPortal.Data;
using RepPortal.Models;


namespace RepPortal.Services;

public class HelpContentService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection _connection;

    public HelpContentService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        _connection = _connectionFactory.CreateRepConnection();
    }

    public async Task<HelpContent> GetHelpContentAsync(string pageKey)
    {
        var sql = "SELECT * FROM PageHelpContent WHERE PageKey = @PageKey";
        return await _connection.QueryFirstOrDefaultAsync<HelpContent>(sql, new { PageKey = pageKey });
    }

    public async Task SaveHelpContentAsync(HelpContent content)
    {
        var existing = await GetHelpContentAsync(content.PageKey);
        if (existing == null)
        {
            var insert = @"
                INSERT INTO PageHelpContent (PageKey, HtmlContent, LastUpdatedBy, LastUpdatedAt)
                VALUES (@PageKey, @HtmlContent, @LastUpdatedBy, GETDATE())";
            await _connection.ExecuteAsync(insert, content);
        }
        else
        {
            var update = @"
                UPDATE PageHelpContent
                SET HtmlContent = @HtmlContent, LastUpdatedBy = @LastUpdatedBy, LastUpdatedAt = GETDATE()
                WHERE PageKey = @PageKey";
            await _connection.ExecuteAsync(update, content);
        }
    }
}
