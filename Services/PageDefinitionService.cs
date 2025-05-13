using Dapper;
using RepPortal.Data;
using RepPortal.Models;

namespace RepPortal.Services;

public interface IPageDefinitionService
{
    Task<List<PageDefinition>> GetAllPagesAsync();
}

public class PageDefinitionService : IPageDefinitionService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PageDefinitionService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<PageDefinition>> GetAllPagesAsync()
    {
        using var conn = _connectionFactory.CreateRepConnection();
        string sql = "SELECT PageKey, PageDescription FROM PageDefinitions WHERE IsActive = 1 ORDER BY SortOrder";
        return (await conn.QueryAsync<PageDefinition>(sql)).ToList();
    }
}
