using RepPortal.Models;
using Dapper;
using System.Data;
using RepPortal.Data;



namespace RepPortal.Services;

public interface IItemService
{
    Task<List<ItemInfo>> GetItemsAsync();
    Task<ItemDetail> GetItemDetailAsync(string item);
}



public class ItemService : IItemService
{
    private readonly DbConnectionFactory _dbConnectionFactory;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IRepCodeContext _repCodeContext;
    private readonly IConfiguration _configuration;
    private readonly CustomerService _customerService;
    private readonly ILogger<PcfService> _logger;

    private List<ItemInfo> _itemsCache;
    private Dictionary<string, ItemDetail> _detailsCache;

    public ItemService(IConfiguration configuration, AuthenticationStateProvider authenticationStateProvider,
        IRepCodeContext repCodeContext, DbConnectionFactory dbConnectionFactory, CustomerService customerService, ILogger<PcfService> logger)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _repCodeContext = repCodeContext;
        _dbConnectionFactory = dbConnectionFactory;
        _configuration = configuration;
        _customerService = customerService;
        _logger = logger;

    }

    public async Task<List<ItemInfo>> GetItemsAsync()
    {
        if (_itemsCache != null)
            return _itemsCache;

        using var connection = _dbConnectionFactory.CreateBatConnection();
        var sql = @"SELECT Item, Description
                        FROM item_mst
                        ORDER BY Item";
        var list = await connection.QueryAsync<ItemInfo>(sql);
        return list.ToList();
    }

    public async Task<ItemDetail> GetItemDetailAsync(string item)
    {
        _detailsCache ??= new();
        if (_detailsCache.TryGetValue(item, out var cached))
            return cached;

        using var connection = _dbConnectionFactory.CreateBatConnection();
        var sql = @"
                SELECT TOP 1
                  i.Item,
                  i.Description,
                  ip.unit_price1 AS Price1,
                  ip.unit_price2 AS Price2,
                  ip.unit_price3 AS Price3
                FROM item_mst i
                JOIN itemprice_mst ip
                  ON ip.item = i.item
                WHERE i.Item = @Item
                ORDER BY ip.effect_date DESC";
        return await connection.QueryFirstOrDefaultAsync<ItemDetail>(sql, new { Item = item });
    }
}
