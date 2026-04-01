using Dapper;
using Microsoft.Extensions.Options;
using RepPortal.Data;
using RepPortal.Models;



namespace RepPortal.Services;

public interface IItemService
{
    Task<List<ItemInfo>> GetItemsAsync();
    Task<ItemDetail> GetItemDetailAsync(string item);
}



public class ItemService : IItemService
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IRepCodeContext _repCodeContext;
    private readonly IConfiguration _configuration;
    private readonly CustomerService _customerService;
    private readonly ILogger<PcfService> _logger;
    private readonly IIdoService _idoService;
    private readonly CsiOptions _csiOptions;

    private List<ItemInfo> _itemsCache;
    private Dictionary<string, ItemDetail> _detailsCache;

    public ItemService(IConfiguration configuration, AuthenticationStateProvider authenticationStateProvider,
        IRepCodeContext repCodeContext, IDbConnectionFactory dbConnectionFactory, CustomerService customerService,
        ILogger<PcfService> logger, IIdoService idoService, IOptions<CsiOptions> csiOptions)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _repCodeContext = repCodeContext;
        _dbConnectionFactory = dbConnectionFactory;
        _configuration = configuration;
        _customerService = customerService;
        _logger = logger;
        _idoService = idoService;
        _csiOptions = csiOptions.Value;
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

        ItemDetail result;

        if (_csiOptions.UseApi)
        {
            result = await _idoService.GetItemDetailAsync(item);
        }
        else
        {
            using var connection = _dbConnectionFactory.CreateBatConnection();
            var sql = @"
                SELECT TOP 1
                  i.Item,
                  i.Description,
                  ip.unit_price1 AS Price1,
                  ip.unit_price2 AS Price2,
                  ip.unit_price3 AS Price3,
                    i.stat as ItemStatus
                FROM item_mst i
                JOIN itemprice_mst ip
                  ON ip.item = i.item
                WHERE i.Item = @Item
                ORDER BY ip.effect_date DESC";
            result = await connection.QueryFirstOrDefaultAsync<ItemDetail>(sql, new { Item = item });
        }

        if (result != null)
            _detailsCache[item] = result;

        return result;
    }
}
