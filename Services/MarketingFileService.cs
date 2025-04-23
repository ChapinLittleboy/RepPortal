using Microsoft.Data.SqlClient;
using RepPortal.Models;
using Dapper;


namespace RepPortal.Services;
public class MarketingFileService
{
    private readonly string _connString;

    public MarketingFileService(IConfiguration config)
    {
        _connString = config.GetConnectionString("RepPortalConnection");
    }

    public async Task<List<MarketingFileSingle>> GetFilesAsync()
    {
        const string sql = @"SELECT Id, DisplayName, FolderRelativePath, FileName, DisplayOrder 
                             FROM dbo.MarketingFiles ORDER BY FolderRelativePath, DisplayOrder;";
        using var conn = new SqlConnection(_connString);
        return (await conn.QueryAsync<MarketingFileSingle>(sql)).ToList();
    }

    public async Task AddFileAsync(MarketingFile file)
    {
        const string sql = @"INSERT INTO dbo.MarketingFiles (DisplayName, FolderRelativePath, FileName, DisplayOrder)
                             VALUES (@DisplayName, @FolderRelativePath, @FileName, @DisplayOrder);";
        using var conn = new SqlConnection(_connString);
        await conn.ExecuteAsync(sql, file);
    }

    public async Task UpdateFileAsync(MarketingFile file)
    {
        const string sql = @"UPDATE dbo.MarketingFiles
                             SET DisplayName = @DisplayName,
                                 FolderRelativePath = @FolderRelativePath,
                                 FileName = @FileName,
                                 DisplayOrder = @DisplayOrder
                             WHERE Id = @Id;";
        using var conn = new SqlConnection(_connString);
        await conn.ExecuteAsync(sql, file);
    }

    public async Task DeleteFileAsync(int id)
    {
        const string sql = @"DELETE FROM dbo.MarketingFiles WHERE Id = @Id;";
        using var conn = new SqlConnection(_connString);
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<List<string>> GetFolderPathsAsync()
    {
        const string sql = @"SELECT DISTINCT FolderRelativePath FROM dbo.MarketingFolders ORDER BY FolderRelativePath;";
        using var conn = new SqlConnection(_connString);
        return (await conn.QueryAsync<string>(sql)).ToList();
    }
}
