// Services/UserContextResolver.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace RepPortal.Services;

public sealed record UserContextResult(string RepCode, IReadOnlyList<string>? AllowedRegions);
public interface IUserContextResolver
{
    /// <summary>
    /// Resolve repCode and allowed regions for the user identified by email.
    /// Returns null if the user can't be resolved.
    /// </summary>
    
    Task<UserContextResult?> ResolveByEmailAsync(string email);
}
public  class UserContextResolver : IUserContextResolver
{
        private readonly string _connectionString;
        private readonly ILogger<UserContextResolver>? _logger;
        private readonly Configuration _cfg;

    

    public UserContextResolver(string? connectionString,  ILogger<UserContextResolver>? logger = null)
        {
            _connectionString = connectionString ?? "Data Source=ciisql10;Database=RepPortal;User Id=sa;Password='*id10t*';TrustServerCertificate=True;";
            _logger = logger;
        
        }

        public async Task<UserContextResult?> ResolveByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // 1) Find the Identity user id
            var userId = await conn.ExecuteScalarAsync<string>(
                @"SELECT TOP(1) [Id]
                  FROM [dbo].[AspNetUsers]
                  WHERE [NormalizedEmail] = UPPER(@Email);",
                new { Email = email });

            if (userId is null)
            {
                _logger?.LogWarning("UserContextResolver: No AspNetUser for email {Email}", email);
                return null;
            }

            // 2) Try to get repCode )
            var repCode = await conn.ExecuteScalarAsync<string>(
                @"SELECT TOP(1) [RepCode]
                  FROM [dbo].[AspNetUsers]
                  WHERE [Id] = @UserId;",
                new { UserId = userId });

          

            if (string.IsNullOrWhiteSpace(repCode))
            {
                _logger?.LogWarning("UserContextResolver: repCode not found for user {Email} ({UserId})", email, userId);
                return null;
            }

            // 3) Regions:
            // If your existing rule is:
            // - LAWxxx -> allowed regions from Region claims
            // - LAW    -> allowed regions from _repCodeContext.CurrentRegions (likely table-driven)
            IReadOnlyList<string>? allowedRegions = null;

           
            if (repCode == "LAW")
            {
            // Replace this with the same source your _repCodeContext.CurrentRegions uses.
            var region = await conn.ExecuteScalarAsync<string>(
                @"SELECT TOP(1) [Region]
                  FROM [dbo].[AspNetUsers]
                  WHERE [Id] = @UserId;",
                new { UserId = userId });

            if (!string.IsNullOrWhiteSpace(region))
            {
                // Support single value or comma/semicolon-separated values.
                var parts = region
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToList();

                allowedRegions = parts.Count > 0 ? parts : new List<string> { region.Trim() };
            }
        }

            return new UserContextResult(repCode, allowedRegions);
        }
    }

