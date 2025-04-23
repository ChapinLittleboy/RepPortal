using Microsoft.Data.SqlClient;
using RepPortal.Models;
using Dapper;


namespace RepPortal.Services;

public class FolderAdminService
{
    private readonly string _connString;

    public FolderAdminService(IConfiguration config)
    {
        _connString = config.GetConnectionString("RepPortalConnection");
    }

    public async Task<List<FolderRecord>> GetFoldersAsync(string tableName)
    {
        var sql = $"SELECT Id, DisplayName, FolderRelativePath, DisplayOrder FROM dbo.{tableName} ORDER BY DisplayOrder;";
        using var conn = new SqlConnection(_connString);
        return (await conn.QueryAsync<FolderRecord>(sql)).ToList();
    }

    public async Task UpdateFolderAsync(string tableName, FolderRecord folder)
    {
        var sql = $@"UPDATE dbo.{tableName}
                     SET DisplayName = @DisplayName,
                         FolderRelativePath = @FolderRelativePath,
                         DisplayOrder = @DisplayOrder
                     WHERE Id = @Id;";
        using var conn = new SqlConnection(_connString);
        await conn.ExecuteAsync(sql, folder);
    }

    public async Task AddFolderAsync(string tableName, FolderRecord folder)
    {
        var sql = $@"INSERT INTO dbo.{tableName} (DisplayName, FolderRelativePath, DisplayOrder)
                     VALUES (@DisplayName, @FolderRelativePath, @DisplayOrder);";
        using var conn = new SqlConnection(_connString);
        await conn.ExecuteAsync(sql, folder);
    }

    public async Task DeleteFolderAsync(string tableName, int id)
    {
        var sql = $"DELETE FROM dbo.{tableName} WHERE Id = @Id;";
        using var conn = new SqlConnection(_connString);
        await conn.ExecuteAsync(sql, new { Id = id });
    }
}
