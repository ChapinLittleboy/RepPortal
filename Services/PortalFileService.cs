using System.Data;

namespace RepPortal.Services;

using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RepPortal.Data;
using RepPortal.Models;

public class PortalFileService
{
    

        private readonly string _connectionString;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly IDbConnectionFactory _dbConnectionFactory;
        private readonly ILogger<SalesService> _logger;
        private readonly IConfiguration _configuration;

    public PortalFileService(IConfiguration configuration, AuthenticationStateProvider authenticationStateProvider,
            IRepCodeContext repCodeContext, IDbConnectionFactory dbConnectionFactory, ILogger<SalesService> logger)
        {
            _connectionString = configuration.GetConnectionString("RepPortalConnection");
            _authenticationStateProvider = authenticationStateProvider;
            _dbConnectionFactory = dbConnectionFactory;
            _logger = logger;
            _configuration =   configuration;


    }

        public async Task<List<PortalFolder>> GetFoldersWithFilesAsync(string pageType)
        {
            using var conn = _dbConnectionFactory.CreateRepConnection();
            var reader = await conn.QueryMultipleAsync("sp_GetPortalFoldersWithFiles", new { PageType = pageType }, commandType: CommandType.StoredProcedure);
            var folders = (await reader.ReadAsync<PortalFolder>()).ToList();
            var files = (await reader.ReadAsync<PortalFile>()).ToList();
            folders.ForEach(f => f.Files = files.Where(x => x.FolderId == f.Id).ToList());
            return folders;
        }

        public async Task AddFolderAsync(PortalFolder folder)
        {
        using var conn = _dbConnectionFactory.CreateRepConnection();
        await conn.ExecuteAsync("sp_AddPortalFolder", folder, commandType: CommandType.StoredProcedure);
        }

        public async Task AddFileAsync(PortalFile file)
        {
        using var conn = _dbConnectionFactory.CreateRepConnection();
        await conn.ExecuteAsync("sp_AddPortalFile", file, commandType: CommandType.StoredProcedure);
        }

        public async Task<List<PortalFile>> GetFilesByFolderIdAsync(int folderId)
        {
        using var conn = _dbConnectionFactory.CreateRepConnection();
        return (await conn.QueryAsync<PortalFile>(
                "sp_GetFilesByFolderId", new { FolderId = folderId },
                commandType: CommandType.StoredProcedure)).ToList();
        }

        public async Task DeleteFileAsync(int fileId)
        {
        using var conn = _dbConnectionFactory.CreateRepConnection();
        await conn.ExecuteAsync("sp_DeletePortalFile", new { Id = fileId }, commandType: CommandType.StoredProcedure);
        }
    // Additional methods for update/delete can be similarly implemented.
}
