

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using global::RepPortal.Data;
using global::RepPortal.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RepPortal.Services;

public interface IPriceBookService
{
    Task<List<PriceBookItem>> GetPriceBookItemsAsync(int priceBookId);
}

public class PriceBookService : IPriceBookService
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly AuthenticationStateProvider _authProvider;
    private readonly IRepCodeContext _repCodeContext;
    private readonly IConfiguration _config;
    private readonly ILogger<PriceBookService> _logger;

    public PriceBookService(
        IDbConnectionFactory dbConnectionFactory,
        AuthenticationStateProvider authProvider,
        IRepCodeContext repCodeContext,
        IConfiguration config,
        ILogger<PriceBookService> logger)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _authProvider = authProvider;
        _repCodeContext = repCodeContext;
        _config = config;
        _logger = logger;
    }

    public async Task<List<PriceBookItem>> GetPriceBookItemsAsync(int priceBookId)
    {
        try
        {
            using var connection = _dbConnectionFactory.CreateRepConnection();

            const string sql = @"
                    SELECT 
                        PriceBookId,
                        Item,
                        ListPrice,
                        PP1Price,
                        PP2Price,
                        BM1Price,
                        BM2Price,
                        FobPrice,
                        MSRPPrice,
                        MAPPrice,
                        EffectiveDate,
                        LastUpdated,
                        ItemStatus
                    FROM dbo.PriceBookItems
                    WHERE PriceBookId = @PriceBookId";

            var items = await connection.QueryAsync<PriceBookItem>(sql, new { PriceBookId = priceBookId });
            return items.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PriceBookItems for PriceBookId={PriceBookId}", priceBookId);
            return new List<PriceBookItem>();
        }
    }
}


