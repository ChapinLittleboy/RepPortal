using Dapper;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Data.SqlClient;
using RepPortal.Models;

namespace RepPortal.Services;

public interface IAdminMarketingFileService
{
    Task<List<AdminFormsFolder>> GetFoldersAsync();
    Task<List<AdminManagedFile>> GetFilesAsync(string folderRelativePath);
    Task<List<string>> SaveFilesAsync(string folderRelativePath, IReadOnlyList<IBrowserFile> files, long maxAllowedSize);
    Task ArchiveFileAsync(string folderRelativePath, string fileRelativePath);
}

/// <summary>
/// Manages the physical files behind the Marketing Downloads page.
/// Mirrors <see cref="AdminFormsFileService"/> for the Forms section, with one addition:
/// the special "FakePlaceholder" folder maps to the root of the marketing share and its
/// files are driven by rows in dbo.MarketingFiles, so uploads and archives against that
/// folder keep the table in sync.
/// </summary>
public class AdminMarketingFileService : IAdminMarketingFileService
{
    private const string RootPlaceholderFolder = "FakePlaceholder";

    private readonly string? _connString;
    private readonly string _root;
    private readonly string _route;

    public AdminMarketingFileService(IConfiguration config)
    {
        _connString = config.GetRequiredResolvedConnectionString("RepPortalConnection");
        _root = Path.GetFullPath(config["MarketingInfo:RootPath"]
            ?? throw new InvalidOperationException("MarketingInfo:RootPath is not configured."));
        _route = config["MarketingInfo:RequestPath"] ?? "/RepDocs";
    }

    public async Task<List<AdminFormsFolder>> GetFoldersAsync()
    {
        const string sql = """
            SELECT Id, DisplayName, FolderRelativePath
            FROM dbo.MarketingFolders
            ORDER BY DisplayOrder;
        """;

        using var conn = new SqlConnection(_connString);
        return (await conn.QueryAsync<AdminFormsFolder>(sql)).ToList();
    }

    public Task<List<AdminManagedFile>> GetFilesAsync(string folderRelativePath)
    {
        var folderPath = ResolveFolderPath(folderRelativePath);
        if (!Directory.Exists(folderPath))
        {
            return Task.FromResult(new List<AdminManagedFile>());
        }

        var isRootFolder = IsRootPlaceholder(folderRelativePath);

        var files = Directory
            .GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var info = new FileInfo(path);

                return new AdminManagedFile
                {
                    Name = info.Name,
                    RelativePath = info.Name,
                    Url = BuildUrl(isRootFolder ? string.Empty : folderRelativePath, info.Name),
                    SizeText = $"{Math.Round(info.Length / 1024.0, 2)} KB",
                    LastModifiedText = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                };
            })
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(files);
    }

    public async Task<List<string>> SaveFilesAsync(string folderRelativePath, IReadOnlyList<IBrowserFile> files, long maxAllowedSize)
    {
        var folderPath = ResolveFolderPath(folderRelativePath);
        Directory.CreateDirectory(folderPath);

        var isRootFolder = IsRootPlaceholder(folderRelativePath);
        var replacedFiles = new List<string>();

        foreach (var file in files)
        {
            var safeFileName = Path.GetFileName(file.Name);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                continue;
            }

            var destinationPath = Path.Combine(folderPath, safeFileName);
            if (File.Exists(destinationPath))
            {
                ArchivePhysicalFile(destinationPath);
                replacedFiles.Add(safeFileName);
            }

            await using (var source = file.OpenReadStream(maxAllowedSize))
            await using (var destination = File.Create(destinationPath))
            {
                await source.CopyToAsync(destination);
            }

            if (isRootFolder)
            {
                await EnsureRootFileRecordAsync(safeFileName);
            }
        }

        return replacedFiles;
    }

    public async Task ArchiveFileAsync(string folderRelativePath, string fileRelativePath)
    {
        var folderPath = ResolveFolderPath(folderRelativePath);
        var safeFileName = Path.GetFileName(fileRelativePath);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new InvalidOperationException("File name is required.");
        }

        var sourcePath = Path.GetFullPath(Path.Combine(folderPath, safeFileName));
        EnsureWithinRoot(sourcePath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected file was not found.", sourcePath);
        }

        ArchivePhysicalFile(sourcePath);

        if (IsRootPlaceholder(folderRelativePath))
        {
            await DeleteRootFileRecordAsync(safeFileName);
        }
    }

    private async Task EnsureRootFileRecordAsync(string fileName)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.MarketingFiles
                           WHERE FolderRelativePath = '/'
                             AND UPPER(FileName) = UPPER(@FileName))
            BEGIN
                INSERT INTO dbo.MarketingFiles (DisplayName, FolderRelativePath, FileName, DisplayOrder)
                SELECT @DisplayName, '/', @FileName,
                       ISNULL(MAX(DisplayOrder), 0) + 10
                FROM dbo.MarketingFiles
                WHERE FolderRelativePath = '/';
            END
        """;

        using var conn = new SqlConnection(_connString);
        await conn.ExecuteAsync(sql, new
        {
            FileName = fileName,
            DisplayName = Path.GetFileNameWithoutExtension(fileName)
        });
    }

    private async Task DeleteRootFileRecordAsync(string fileName)
    {
        const string sql = """
            DELETE FROM dbo.MarketingFiles
            WHERE FolderRelativePath = '/'
              AND UPPER(FileName) = UPPER(@FileName);
        """;

        using var conn = new SqlConnection(_connString);
        await conn.ExecuteAsync(sql, new { FileName = fileName });
    }

    private static bool IsRootPlaceholder(string folderRelativePath) =>
        string.Equals(folderRelativePath, RootPlaceholderFolder, StringComparison.OrdinalIgnoreCase);

    private string ResolveFolderPath(string folderRelativePath)
    {
        if (string.IsNullOrWhiteSpace(folderRelativePath))
        {
            throw new InvalidOperationException("Folder path is required.");
        }

        if (IsRootPlaceholder(folderRelativePath))
        {
            return _root;
        }

        var candidate = Path.GetFullPath(Path.Combine(_root, folderRelativePath));
        EnsureWithinRoot(candidate);
        return candidate;
    }

    private void EnsureWithinRoot(string candidatePath)
    {
        var normalizedRoot = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                             + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidatePath, _root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The resolved path is outside the configured document root.");
        }
    }

    private string BuildUrl(string folderRelativePath, string fileName)
    {
        var segments = SplitSegments(folderRelativePath)
            .Append(fileName)
            .Select(Uri.EscapeDataString);

        return $"{_route.TrimEnd('/')}/{string.Join("/", segments)}";
    }

    private void ArchivePhysicalFile(string sourcePath)
    {
        var archivePath = Path.GetFullPath(Path.Combine(_root, "Archive"));
        Directory.CreateDirectory(archivePath);

        var sourceInfo = new FileInfo(sourcePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var archivedName = $"{Path.GetFileNameWithoutExtension(sourceInfo.Name)} Deleted {timestamp}{sourceInfo.Extension}";
        var destinationPath = Path.Combine(archivePath, archivedName);

        File.Move(sourcePath, destinationPath);
    }

    private static IEnumerable<string> SplitSegments(string path) =>
        path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
}
