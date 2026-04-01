using System.Globalization;
using System.Reflection;
using System.Text.Json;
using RepPortal.Models;

namespace RepPortal.Services;

/// <summary>
/// Implements IDO API calls against the CSI REST endpoint.
/// All methods in this class are API-only; SQL fallback lives in the calling service.
/// </summary>
public class IdoService : IIdoService
{
    private readonly ICsiRestClient _csiRestClient;
    private readonly ILogger<IdoService> _logger;

    public IdoService(ICsiRestClient csiRestClient, ILogger<IdoService> logger)
    {
        _csiRestClient = csiRestClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ItemDetail?> GetItemDetailAsync(string item)
    {
        // ── 1. SLItems — item master (description, status) ──
        var itemQuery = new Dictionary<string, string>
        {
            ["props"]    = "Item,Description,Stat",
            ["filter"]   = Eq("Item", item),
            ["rowcap"]   = "1",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var itemJson = await _csiRestClient.GetAsync("json/SLItems/adv", itemQuery);
        var itemResponse = Deserialize(itemJson);

        if (itemResponse.MessageCode != 0 || itemResponse.Items.Count == 0)
        {
            _logger.LogWarning(
                "SLItems returned no result for item {Item}: [{Code}] {Msg}",
                item, itemResponse.MessageCode, itemResponse.Message);
            return null;
        }

        var itemRow = MapRow<SLItemRow>(itemResponse.Items[0]);

        // ── 2. SLItemPrices — most recent effective price break ──
        var priceQuery = new Dictionary<string, string>
        {
            ["props"]    = "Item,EffectDate,UnitPrice1,UnitPrice2,UnitPrice3",
            ["filter"]   = Eq("Item", item),
            ["rowcap"]   = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var priceJson = await _csiRestClient.GetAsync("json/SLItemPrices/adv", priceQuery);
        var priceResponse = Deserialize(priceJson);

        SLItemPriceRow? priceRow = null;
        if (priceResponse.MessageCode == 0 && priceResponse.Items.Count > 0)
        {
            // IDO does not guarantee order; sort client-side for the most recent row.
            priceRow = priceResponse.Items
                .Select(r => MapRow<SLItemPriceRow>(r))
                .OrderByDescending(r => r.EffectDate)
                .FirstOrDefault();
        }
        else
        {
            _logger.LogWarning(
                "SLItemPrices returned no result for item {Item}: [{Code}] {Msg}",
                item, priceResponse.MessageCode, priceResponse.Message);
        }

        return new ItemDetail
        {
            Item        = itemRow.Item ?? item,
            Description = itemRow.Description ?? "",
            ItemStatus  = itemRow.Stat ?? "A",
            Price1      = priceRow?.UnitPrice1 ?? 0m,
            Price2      = priceRow?.UnitPrice2 ?? 0m,
            Price3      = priceRow?.UnitPrice3 ?? 0m,
        };
    }

    // ── IDO row types ──

    private sealed class SLItemRow
    {
        [CsiField("Item")]        public string? Item        { get; set; }
        [CsiField("Description")] public string? Description { get; set; }
        [CsiField("Stat")]        public string? Stat        { get; set; }
    }

    private sealed class SLItemPriceRow
    {
        [CsiField("EffectDate")]  public DateTime? EffectDate { get; set; }
        [CsiField("UnitPrice1")]  public decimal   UnitPrice1 { get; set; }
        [CsiField("UnitPrice2")]  public decimal   UnitPrice2 { get; set; }
        [CsiField("UnitPrice3")]  public decimal   UnitPrice3 { get; set; }
    }

    // ── Private helpers — same pattern as RepPortal IdoService ──

    private static MgRestAdvResponse Deserialize(string json) =>
        JsonSerializer.Deserialize<MgRestAdvResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static T MapRow<T>(List<MgNameValue> row) where T : new()
    {
        var obj = new T();
        foreach (var prop in typeof(T).GetProperties())
        {
            var attr = prop.GetCustomAttribute<CsiFieldAttribute>();
            if (attr == null)
                continue;

            var cell = row.FirstOrDefault(c =>
                string.Equals(c.Name, attr.FieldName, StringComparison.OrdinalIgnoreCase));

            if (cell?.Value == null)
                continue;

            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            prop.SetValue(obj, ConvertTo(cell.Value, targetType));
        }
        return obj;
    }

    private static object? ConvertTo(string? raw, Type targetType)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        var isNullable = underlyingType != null;
        var effectiveType = underlyingType ?? targetType;

        if (string.IsNullOrWhiteSpace(raw))
        {
            if (isNullable || !effectiveType.IsValueType)
                return null;
            throw new FormatException($"Cannot convert null/empty to non-nullable '{effectiveType.Name}'.");
        }

        if (effectiveType == typeof(string))
            return raw;

        if (effectiveType == typeof(DateTime))
        {
            string[] formats =
            {
                "yyyyMMdd HH:mm:ss.fff",
                "yyyyMMdd",
                "yyyy-MM-dd",
                "yyyy-MM-dd HH:mm:ss",
                "o"
            };
            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt))
                return dt;
            throw new FormatException($"Cannot convert '{raw}' to DateTime.");
        }

        if (effectiveType == typeof(decimal))
        {
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
                return dec;
            throw new FormatException($"Cannot convert '{raw}' to decimal.");
        }

        if (effectiveType == typeof(int))
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                return i;
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return (int)d;
            throw new FormatException($"Cannot convert '{raw}' to int.");
        }

        return Convert.ChangeType(raw, effectiveType, CultureInfo.InvariantCulture);
    }

    private static string Eq(string field, string value) =>
        $"{field} = '{value.Replace("'", "''")}'";
}
