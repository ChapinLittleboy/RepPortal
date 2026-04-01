using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RepPortal.Models;

namespace RepPortal.Services;

public class IdoService : IIdoService
{
    private readonly ICsiRestClient _csiRestClient;
    private readonly ILogger<IdoService> _logger;
    private readonly CsiOptions _csiOptions;

    // DAL rep has visibility into these customers regardless of slsman on the order.
    private static readonly HashSet<string> DalSpecialCustNums =
        new(StringComparer.OrdinalIgnoreCase) { "45424", "45427", "45424K" };

    public IdoService(
        ICsiRestClient csiRestClient,
        ILogger<IdoService> logger,
        IOptions<CsiOptions> csiOptions)
    {
        _csiRestClient = csiRestClient;
        _logger = logger;
        _csiOptions = csiOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<(OrderLookupHeader? Header, List<OrderLookupLine> Lines)> GetOrderLookupAsync(
        string custNum, string normalizedPo, string repCode)
    {
        // ── 1. Query SLCos for the order header ──────────────────────────────
        // Filter by CustNum (and Slsman when applicable). Post-filter by PO/CoNum in C#
        // because IDO cannot normalize stored CustPo values the way the SQL path does.
        var hdrFilters = new List<string> { Eq("CustNum", custNum) };

        if (repCode != "Admin")
        {
            if (repCode == "DAL" && !DalSpecialCustNums.Contains(custNum.Trim()))
                hdrFilters.Add(Eq("Slsman", "DAL"));
            else if (repCode != "DAL")
                hdrFilters.Add(Eq("Slsman", repCode));
        }

        var hdrQuery = new Dictionary<string, string>
        {
            ["props"]    = "CustNum,CoNum,CustPo,CustSeq,OrderDate,Stat,CreditHold,BillToName,ShipToState",
            ["filter"]   = string.Join(" AND ", hdrFilters),
            ["rowcap"]   = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        _logger.LogDebug("GetOrderLookupAsync SLCos filter: {Filter}", hdrQuery["filter"]);

        var hdrResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCos/adv", hdrQuery));
        if (hdrResponse.MessageCode != 0)
            throw new InvalidOperationException($"SLCos query failed: {hdrResponse.Message}");

        // Match normalised CustPo OR exact CoNum (trimmed).
        List<MgNameValue>? matchedRow = null;
        foreach (var row in hdrResponse.Items)
        {
            var storedPo  = GetCell(row, "CustPo") ?? "";
            var storedNum = GetCell(row, "CoNum")?.Trim() ?? "";

            if (NormalizePo(storedPo).Equals(normalizedPo, StringComparison.OrdinalIgnoreCase)
                || storedNum.Equals(normalizedPo, StringComparison.OrdinalIgnoreCase))
            {
                matchedRow = row;
                break;
            }
        }

        if (matchedRow is null)
            return (null, new List<OrderLookupLine>());

        var header = new OrderLookupHeader
        {
            CustNum      = GetCell(matchedRow, "CustNum")?.Trim()    ?? "",
            CustomerName = GetCell(matchedRow, "BillToName")?.Trim() ?? "",
            CoNum        = GetCell(matchedRow, "CoNum")?.Trim()      ?? "",
            CustPo       = GetCell(matchedRow, "CustPo")?.Trim()     ?? "",
            CustSeq      = int.TryParse(GetCell(matchedRow, "CustSeq"), out int seq) ? seq : 0,
            OrderDate    = ParseDate(GetCell(matchedRow, "OrderDate")) ?? DateTime.MinValue,
            OrderStatus  = GetCell(matchedRow, "Stat")?.Trim()        ?? "",
            CreditHold   = ParseBool(GetCell(matchedRow, "CreditHold")),
            ShipToState  = GetCell(matchedRow, "ShipToState")?.Trim(),
        };

        // ── 2. Query SLCoitems for line items ────────────────────────────────
        // SLCoitems.ShipDate maps directly to coitem_mst.ship_date, which is the same
        // fallback value the SQL path uses (COALESCE(co_ship_mst.ship_date, coitem_mst.ship_date)).
        var lineQuery = new Dictionary<string, string>
        {
            ["props"]    = "CoLine,Item,Description,QtyOrdered,QtyShipped,QtyInvoiced,DueDate,Stat,ShipDate",
            ["filter"]   = Eq("CoNum", header.CoNum),
            ["rowcap"]   = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var lineResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCoitems/adv", lineQuery));
        if (lineResponse.MessageCode != 0)
            throw new InvalidOperationException($"SLCoitems query failed: {lineResponse.Message}");

        var lines = lineResponse.Items
            .Select(MapRow<CoItemRow>)
            .Select(r => new OrderLookupLine
            {
                CoLine      = r.CoLine,
                Item        = r.Item,
                Description = r.Description,
                QtyOrdered  = r.QtyOrdered,
                QtyShipped  = r.QtyShipped,
                QtyInvoiced = r.QtyInvoiced,
                DueDate     = r.DueDate,
                LineStatus  = r.Stat,
                ShipDate    = r.ShipDate,
            })
            .ToList();

        // ── 3. Fetch InvoiceDate from SLInvHdrs ──────────────────────────────
        if (!string.IsNullOrWhiteSpace(header.CustPo))
        {
            var invQuery = new Dictionary<string, string>
            {
                ["props"]    = "InvDate",
                ["filter"]   = $"{Eq("CustNum", header.CustNum)} AND {Eq("CustPo", header.CustPo)}",
                ["rowcap"]   = "1",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var invResponse = Deserialize(await _csiRestClient.GetAsync("json/SLInvHdrs/adv", invQuery));
            if (invResponse.MessageCode == 0 && invResponse.Items.Count > 0)
                header.InvoiceDate = ParseDate(GetCell(invResponse.Items[0], "InvDate"));
        }

        return (header, lines);
    }

    // ── Private IDO row DTOs ─────────────────────────────────────────────────

    private sealed class CoItemRow
    {
        [CsiField("CoLine")]      public int       CoLine      { get; set; }
        [CsiField("Item")]        public string    Item        { get; set; } = "";
        [CsiField("Description")] public string?   Description { get; set; }
        [CsiField("QtyOrdered")]  public int       QtyOrdered  { get; set; }
        [CsiField("QtyShipped")]  public int       QtyShipped  { get; set; }
        [CsiField("QtyInvoiced")] public int       QtyInvoiced { get; set; }
        [CsiField("DueDate")]     public DateTime? DueDate     { get; set; }
        [CsiField("Stat")]        public string    Stat        { get; set; } = "";
        [CsiField("ShipDate")]    public DateTime? ShipDate    { get; set; }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MgRestAdvResponse Deserialize(string json) =>
        JsonSerializer.Deserialize<MgRestAdvResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static string? GetCell(List<MgNameValue> row, string fieldName) =>
        row.FirstOrDefault(c =>
            string.Equals(c.Name, fieldName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static T MapRow<T>(List<MgNameValue> row) where T : new()
    {
        var obj   = new T();
        var props = typeof(T).GetProperties();

        foreach (var prop in props)
        {
            var attr = prop.GetCustomAttribute<CsiFieldAttribute>();
            if (attr is null) continue;

            var cell = row.FirstOrDefault(c =>
                string.Equals(c.Name, attr.FieldName, StringComparison.OrdinalIgnoreCase));

            if (cell?.Value is null) continue;

            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            prop.SetValue(obj, ConvertTo(cell.Value, targetType));
        }

        return obj;
    }

    private static object? ConvertTo(string? raw, Type targetType)
    {
        var underlying    = Nullable.GetUnderlyingType(targetType);
        var isNullable    = underlying is not null;
        var effectiveType = underlying ?? targetType;

        if (string.IsNullOrWhiteSpace(raw))
        {
            if (isNullable || !effectiveType.IsValueType) return null;
            throw new FormatException($"Cannot convert null/empty to non-nullable '{effectiveType.Name}'.");
        }

        if (effectiveType == typeof(string)) return raw;

        if (effectiveType == typeof(bool))
        {
            var t = raw.Trim();
            if (t == "1" || t.Equals("true",  StringComparison.OrdinalIgnoreCase)) return true;
            if (t == "0" || t.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            throw new FormatException($"Cannot convert '{raw}' to bool.");
        }

        if (effectiveType == typeof(DateTime))
            return ParseDate(raw) ?? throw new FormatException($"Cannot convert '{raw}' to DateTime.");

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

    private static DateTime? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string[] formats =
        {
            "yyyyMMdd HH:mm:ss.fff", "yyyyMMdd",
            "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "o"
        };

        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
            return dt;

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out dt))
            return dt;

        return null;
    }

    private static bool ParseBool(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Trim();
        return t == "1" || t.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePo(string po) =>
        Regex.Replace(po, "[^A-Za-z0-9]", "");

    private static string Eq(string field, string value) =>
        $"{field} = '{value.Replace("'", "''")}'";
}
