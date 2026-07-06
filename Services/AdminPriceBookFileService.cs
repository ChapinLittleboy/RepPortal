using Dapper;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Data.SqlClient;
using RepPortal.Models;

namespace RepPortal.Services;

public interface IAdminPriceBookFileService
{
    Task<List<AdminFormsFolder>> GetFoldersAsync();
    Task<List<AdminManagedFile>> GetFilesAsync(string folderRelativePath);
    Task<List<string>> SaveFilesAsync(string folderRelativePath, IReadOnlyList<IBrowserFile> files, long maxAllowedSize);
    Task ArchiveFileAsync(string folderRelativePath, string fileRelativePath);
}

/// <summary>
/// Manages the physical files behind the Price Book download section (GetItemPricing page).
/// Mirrors <see cref="AdminFormsFileService"/>, reading folder definitions from
/// dbo.PriceBookFolders. Note: the download page only displays .xlsx files, so other
/// file types uploaded here will not be visible to reps.
/// </summary>
public class AdminPriceBookFileService : IAdminPriceBookFileService
{
    private readonly string? _connString;
    private readonly string _root;
    private readonly string _route;

    public AdminPriceBookFileService(IConfiguration config)
    {
        _connString = config.GetRequiredResolvedConnectionString("RepPortalConnection");
        _root = Path.GetFullPath(config["PriceBooks:RootPath"]
            ?? throw new InvalidOperationException("PriceBooks:RootPath is not configured."));
        _route = config["PriceBooks:RequestPath"] ?? "/RepDocs";
    }

    public async Task<List<AdminFormsFolder>> GetFoldersAsync()
    {
        const string sql = """
            SELECT Id, DisplayName, FolderRelativePath
            FROM dbo.PriceBookFolders
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

        var files = Directory
            .GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var info = new FileInfo(path);

                return new AdminManagedFile
                {
                    Name = info.Name,
                    RelativePath = info.Name,
                    Url = BuildUrl(folderRelativePath, info.Name),
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

            await using var source = file.OpenReadStream(maxAllowedSize);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination);
        }

        return replacedFiles;
    }

    public Task ArchiveFileAsync(string folderRelativePath, string fileRelativePath)
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
        return Task.CompletedTask;
    }

    private string ResolveFolderPath(string folderRelativePath)
    {
        if (string.IsNullOrWhiteSpace(folderRelativePath))
        {
            throw new InvalidOperationException("Folder path is required.");
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
