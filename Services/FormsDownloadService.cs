namespace RepPortal.Services;



using Dapper;
using RepPortal.Models;
using Microsoft.Data.SqlClient;
using System.IO;
using global::RepPortal.Models;

public interface IFormsDownloadService
{
    Task<List<FormsDownloadFolder>> GetFormsDownFoldersAsync();
}

public class FormsDownloadService : IFormsDownloadService
{
    private readonly string _connString;
    private readonly IWebHostEnvironment _env;
    private readonly string _root;
    private readonly string _route;


    public FormsDownloadService(IConfiguration config, IWebHostEnvironment env)
    {
        _connString = config.GetConnectionString("RepPortalConnection");
        _env = env;
        _root = config["PriceBooks:RootPath"];
        _route = config["PriceBooks:RequestPath"] ?? "/RepDocs";
    }

    public async Task<List<FormsDownloadFolder>> GetFormsDownFoldersAsync()
    {
        const string sql = @"
            SELECT Id, DisplayName, FolderRelativePath
              FROM dbo.FormsFolders
             ORDER BY DisplayOrder;
        ";

        using var conn = new SqlConnection(_connString);
        var folders = (await conn.QueryAsync<FormsDownloadFolder>(sql)).ToList();

        foreach (var folder in folders)
        {
            // _root comes from config["PriceBooks:RootPath"] == "\\\\ciiws01\\ChapinRepDocs"
            var physical = Path.Combine(_root, folder.FolderRelativePath);
            if (!Directory.Exists(physical))
            {
                // you may log a warning here
                continue;
            }

            // only pick up Excel files in that folder
            var filePaths = Directory
                .GetFiles(physical, "*.*", SearchOption.AllDirectories);

            folder.Files = filePaths
                .Select(fp =>
                {
                    var info = new FileInfo(fp);
                    var name = info.Name;
                    // _route comes from config["PriceBooks:RequestPath"] == "/RepDocs"
                    var url = $"{_route.TrimEnd('/')}/{Uri.EscapeDataString(folder.FolderRelativePath)}/{Uri.EscapeDataString(name)}";
                    var sizeKb = Math.Round(info.Length / 1024.0, 2);

                    return new FormsDownloadFile
                    {
                        Name = name,
                        Url = url,
                        SizeText = $"{sizeKb} KB"
                    };
                })
                .ToList();
        }

        return folders;
    }
}

