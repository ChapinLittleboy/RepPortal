using Dapper;
using Microsoft.Data.SqlClient;
using RepPortal.Models;


namespace RepPortal.Services;

public class FolderAdminService
{
    private static readonly HashSet<string> AllowedFolderTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "FormsFolders",
        "MarketingFolders",
        "PricebookFolders"
    };

    private readonly IConfiguration _config;
    private readonly string? _connString;

    public FolderAdminService(IConfiguration config)
    {
        _config = config;
        _connString = config.GetRequiredResolvedConnectionString("RepPortalConnection");
    }

    public async Task<List<FolderRecord>> GetFoldersAsync(string tableName)
    {
        EnsureAllowedTable(tableName);

        var sql = $"SELECT Id, DisplayName, FolderRelativePath, DisplayOrder FROM dbo.{tableName} ORDER BY DisplayOrder;";
        using var conn = new SqlConnection(_connString);
        return (await conn.QueryAsync<FolderRecord>(sql)).ToList();
    }

    public Task<List<string>> GetExistingPhysicalFoldersAsync(string tableName)
    {
        EnsureAllowedTable(tableName);

        var root = GetDocumentRoot(tableName);
        if (!Directory.Exists(root))
        {
            return Task.FromResult(new List<string>());
        }

        var folders = Directory
            .EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(folders);
    }

    public async Task UpdateFolderAsync(string tableName, FolderRecord folder)
    {
        EnsureAllowedTable(tableName);
        var folderName = NormalizeOneLevelFolderName(folder.FolderRelativePath);
        await EnsureFolderNameUnusedAsync(tableName, folder.Id, folderName);
        EnsurePhysicalFolder(tableName, folderName);
        folder.FolderRelativePath = folderName;

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
        EnsureAllowedTable(tableName);
        var folderName = NormalizeOneLevelFolderName(folder.FolderRelativePath);
        await EnsureFolderNameUnusedAsync(tableName, folder.Id, folderName);
        EnsurePhysicalFolder(tableName, folderName);
        folder.FolderRelativePath = folderName;

        var sql = $@"INSERT INTO dbo.{tableName} (DisplayName, FolderRelativePath, DisplayOrder)
                     VALUES (@DisplayName, @FolderRelativePath, @DisplayOrder);";
        using var conn = new SqlConnection(_connString);
        await conn.ExecuteAsync(sql, folder);
    }

    public async Task DeleteFolderAsync(string tableName, int id)
    {
        EnsureAllowedTable(tableName);

        var sql = $"DELETE FROM dbo.{tableName} WHERE Id = @Id;";
        using var conn = new SqlConnection(_connString);
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    private void EnsurePhysicalFolder(string tableName, string folderName)
    {
        var root = GetDocumentRoot(tableName);
        var physicalPath = Path.GetFullPath(Path.Combine(root, folderName));

        EnsureWithinRoot(root, physicalPath);
        Directory.CreateDirectory(physicalPath);
    }

    private async Task EnsureFolderNameUnusedAsync(string tableName, int? id, string folderName)
    {
        var sql = $"""
            SELECT COUNT(1)
            FROM dbo.{tableName}
            WHERE UPPER(FolderRelativePath) = UPPER(@FolderRelativePath)
              AND (@Id IS NULL OR Id <> @Id);
            """;

        using var conn = new SqlConnection(_connString);
        var duplicateCount = await conn.ExecuteScalarAsync<int>(sql, new
        {
            Id = id,
            FolderRelativePath = folderName
        });

        if (duplicateCount > 0)
        {
            throw new InvalidOperationException("That folder is already used by another folder record.");
        }
    }

    private string GetDocumentRoot(string tableName)
    {
        var configKey = tableName.Equals("MarketingFolders", StringComparison.OrdinalIgnoreCase)
            ? "MarketingInfo:RootPath"
            : "PriceBooks:RootPath";

        var root = _config[configKey] ?? _config["PriceBooks:RootPath"];
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException($"{configKey} is not configured.");
        }

        return Path.GetFullPath(root);
    }

    private static string NormalizeOneLevelFolderName(string? folderRelativePath)
    {
        var folderName = folderRelativePath?.Trim();
        if (string.IsNullOrWhiteSpace(folderName))
        {
            throw new InvalidOperationException("Folder path is required.");
        }

        if (Path.IsPathRooted(folderName)
            || folderName.Contains(Path.DirectorySeparatorChar)
            || folderName.Contains(Path.AltDirectorySeparatorChar)
            || folderName is "." or ".."
            || folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("Folder path must be a single folder name under the document root.");
        }

        return folderName;
    }

    private static void EnsureWithinRoot(string root, string candidatePath)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                             + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The resolved folder path is outside the configured document root.");
        }
    }

    private static void EnsureAllowedTable(string tableName)
    {
        if (!AllowedFolderTables.Contains(tableName))
        {
            throw new InvalidOperationException("Unsupported folder table.");
        }
    }
}
