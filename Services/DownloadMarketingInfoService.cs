namespace RepPortal.Services;

using System.IO;
using Dapper;
using Microsoft.Data.SqlClient;
using RepPortal.Models;



public interface IMarketingService
{
    Task<List<MarketingFolder>> GetMarketingFoldersAsync();
}

public class DownloadMarketingInfoService : IMarketingService
{
    private readonly string? _connString;
    private readonly IWebHostEnvironment _env;
    private readonly string? _root;
    private readonly string _route;


    public DownloadMarketingInfoService(IConfiguration config, IWebHostEnvironment env)
    {
        _connString = config.GetRequiredResolvedConnectionString("RepPortalConnection");
        _env = env;
        _root = config["MarketingInfo:RootPath"];
        _route = config["MarketingInfo:RequestPath"] ?? "/RepDocs";
    }

    public async Task<List<MarketingFolder>> GetMarketingFoldersAsync()
    {
        const string folderSql = @"
        SELECT Id, DisplayName, FolderRelativePath
          FROM dbo.MarketingFolders
         ORDER BY DisplayOrder;
    ";

        const string filesSql = @"
        SELECT DisplayName, FolderRelativePath, FileName, DisplayOrder
          FROM dbo.MarketingFiles order by DisplayOrder;
    ";

        using var conn = new SqlConnection(_connString);

        var folders = (await conn.QueryAsync<MarketingFolder>(folderSql)).ToList();
        var allFiles = (await conn.QueryAsync<MarketingFileSingle>(filesSql)).ToList();

        //var allowedExtensions = new[] { ".xls", ".xlsx", ".doc", ".docx", ".zip" };

        foreach (var folder in folders)
        {
            var physical = Path.Combine(_root ?? string.Empty, folder.FolderRelativePath ?? string.Empty);
            var isFakeFolder = folder.FolderRelativePath == "FakePlaceholder";

            if (isFakeFolder)
            {
                // Pull files from the root (FolderRelativePath = '/')
                var folderFiles = allFiles
                     .Where(f => f.FolderRelativePath == "/")
                     .Select(f =>
                     {
                         var physicalPath = Path.Combine(_root ?? string.Empty, f.FileName ?? string.Empty);
                         if (!File.Exists(physicalPath))
                             return null;

                         var info = new FileInfo(physicalPath);
                         return new MarketingFile
                         {
                             Name = f.DisplayName ?? f.FileName,
                             Url = $"{_route.TrimEnd('/')}/{Uri.EscapeDataString(f.FileName ?? string.Empty)}",
                             SizeText = $"{Math.Round(info.Length / 1024.0, 2)} KB"
                         };
                     })
                     .OfType<MarketingFile>()
                     .ToList();

                folder.Files = folderFiles;
            }
            else
            {
                if (!Directory.Exists(physical))
                    continue;

                var folderFiles = Directory
                    .GetFiles(physical, "*.*", SearchOption.TopDirectoryOnly)
                    //.Where(fp => allowedExtensions.Contains(Path.GetExtension(fp), StringComparer.OrdinalIgnoreCase))
                    .Select(fp =>
                    {
                        var info = new FileInfo(fp);
                        var name = info.Name;
                        return new MarketingFile
                        {
                            Name = name,
                            Url = $"{_route.TrimEnd('/')}/{Uri.EscapeDataString(folder.FolderRelativePath ?? string.Empty)}/{Uri.EscapeDataString(name)}",
                            SizeText = $"{Math.Round(info.Length / 1024.0, 2)} KB"
                        };
                    })
                    .ToList();

                folder.Files = folderFiles;
            }
        }

        return folders;
    }

}

