using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RepPortal.Models;

namespace RepPortal.Services;

/// <summary>
/// Implements <see cref="IIdoService"/> by calling the CSI REST API (MG-REST / IDO layer).
/// Field names are verified against Chap_InvoiceLines_Properties.csv.
/// </summary>
public class IdoService : IIdoService
{
    private readonly ICsiRestClient _csiRestClient;
    private readonly CsiOptions _csiOptions;
    private readonly ILogger<IdoService> _logger;

    public IdoService(
        ICsiRestClient csiRestClient,
        IOptions<CsiOptions> csiOptions,
        ILogger<IdoService> logger)
    {
        _csiRestClient = csiRestClient;
        _csiOptions = csiOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<Dictionary<string, object>>> GetItemSalesReportDataAsync(
        string repCode,
        List<string>? allowedRegions)
    {
        var today = DateTime.Today;
        int fiscalYear = today.Month >= 9 ? today.Year + 1 : today.Year;

        // Two fiscal years: prior (FY{fiscalYear-1}) and current (FY{fiscalYear})
        var fyPriorStart   = new DateTime(fiscalYear - 2, 9, 1);
        var fyPriorEnd     = new DateTime(fiscalYear - 1, 8, 31);
        var fyCurrentStart = new DateTime(fiscalYear - 1, 9, 1);
        var fyCurrentEnd   = new DateTime(fiscalYear,     8, 31);

        int currentFiscalMonth = today.Month >= 9 ? today.Month - 8 : today.Month + 4;

        // Monthly column keys for the current FY, e.g. ["Sep2025", "Oct2025", ...]
        var currentFYMonths = Enumerable.Range(0, currentFiscalMonth)
            .Select(i => fyCurrentStart.AddMonths(i))
            .Select(d => d.ToString("MMM") + d.Year)
            .ToList();

        // ── 1. Fetch invoice lines from Chap_InvoiceLines IDO ──
        var filters = new List<string>
        {
            Eq("Slsman", repCode),
            $"InvDate >= '{fyPriorStart:yyyyMMdd}'",
            $"InvDate <= '{fyCurrentEnd:yyyyMMdd}'"
        };

        if (allowedRegions is { Count: > 0 })
            filters.Add(In("SalesRegion", allowedRegions));

        var props = string.Join(",", new[]
        {
            "InvDate", "CustNum", "CustSeq",
            "ShipToCity", "ShipToState", "BillToState",
            "Slsman", "SalesRegion", "RegionName",
            "item", "qty_invoiced", "price", "disc", "Period"
        });

        var query = new Dictionary<string, string>
        {
            ["props"]    = props,
            ["filter"]   = string.Join(" AND ", filters),
            ["rowcap"]   = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        // Query BAT and (if configured) KENT — mirrors the SQL UNION ALL
        var siteAuths = new List<(string Site, string? AuthOverride)> { ("BAT", null) };
        if (!string.IsNullOrWhiteSpace(_csiOptions.KentAuthorization))
            siteAuths.Add(("KENT", _csiOptions.KentAuthorization));

        var lines = new List<InvLineRawRow>();

        foreach (var (site, authOverride) in siteAuths)
        {
            string siteJson = authOverride != null
                ? await _csiRestClient.GetAsync("json/Chap_InvoiceLines/adv", query, authOverride)
                : await _csiRestClient.GetAsync("json/Chap_InvoiceLines/adv", query);

            var siteResponse = Deserialize(siteJson);

            if (siteResponse.MessageCode == 0)
            {
                lines.AddRange(siteResponse.Items
                    .Select(MapRow<InvLineRawRow>)
                    .Where(l => l.InvDate.HasValue));
            }
            else if (authOverride != null)
            {
                // Chap_InvoiceLines is a custom IDO that may not be deployed on KENT.
                // Fall back to standard SLInvHdrs + SLInvItemAlls which exist on all instances.
                _logger.LogInformation(
                    "Chap_InvoiceLines not available on {Site} ({Msg}); falling back to SLInvHdrs + SLInvItemAlls",
                    site, siteResponse.Message);
                lines.AddRange(await FetchKentLinesViaStandardIdosAsync(
                    repCode, fyPriorStart, fyCurrentEnd, authOverride));
            }
            else
            {
                _logger.LogWarning("Chap_InvoiceLines failed for {Site}: {Msg}", site, siteResponse.Message);
            }
        }

        _logger.LogInformation(
            "GetItemSalesReportDataAsync: {Count} raw lines fetched for rep {RepCode} (BAT + KENT)",
            lines.Count, repCode);

        // ── 2. Customer names — SLCustomers, CustSeq=0 (billing/corporate address) ──
        var uniqueCustNums = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.CustNum))
            .Select(l => l.CustNum!)
            .Distinct()
            .ToList();

        var custNameLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Also used to backfill SalesRegion on KENT fallback lines
        var regionFromCustLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (uniqueCustNums.Count > 0)
        {
            var custQuery = new Dictionary<string, string>
            {
                ["props"]    = "CustNum,CustSeq,Name,Uf_SalesRegion",
                ["filter"]   = $"CustSeq = 0 AND {In("CustNum", uniqueCustNums)}",
                ["rowcap"]   = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var custResponse = Deserialize(await _csiRestClient.GetAsync("json/SLCustomers/adv", custQuery));

            if (custResponse.MessageCode == 0)
            {
                foreach (var custRow in custResponse.Items.Select(MapRow<CustNameInfo>))
                {
                    if (string.IsNullOrWhiteSpace(custRow.CustNum))
                        continue;

                    custNameLookup[custRow.CustNum] = custRow.Name ?? "";
                    if (!string.IsNullOrWhiteSpace(custRow.UfSalesRegion))
                        regionFromCustLookup[custRow.CustNum] = custRow.UfSalesRegion;
                }
            }
            else
            {
                _logger.LogWarning("SLCustomers lookup failed: {Msg}", custResponse.Message);
            }
        }

        // ── 3. Item descriptions — SLItems ──
        var uniqueItems = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Item))
            .Select(l => l.Item!)
            .Distinct()
            .ToList();

        var itemDescLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (uniqueItems.Count > 0)
        {
            var itemQuery = new Dictionary<string, string>
            {
                ["props"]    = "Item,Description",
                ["filter"]   = In("Item", uniqueItems),
                ["rowcap"]   = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var itemResponse = Deserialize(await _csiRestClient.GetAsync("json/SLItems/adv", itemQuery));

            if (itemResponse.MessageCode == 0)
            {
                foreach (var itemRow in itemResponse.Items.Select(MapRow<ItemDescInfo>))
                {
                    if (!string.IsNullOrWhiteSpace(itemRow.Item))
                        itemDescLookup[itemRow.Item] = itemRow.Description ?? "";
                }
            }
            else
            {
                _logger.LogWarning("SLItems lookup failed: {Msg}", itemResponse.Message);
            }
        }

        // ── 3b. Backfill SalesRegion on KENT fallback lines ──
        foreach (var line in lines.Where(l => string.IsNullOrEmpty(l.SalesRegion)
                                              && !string.IsNullOrWhiteSpace(l.CustNum)))
        {
            if (regionFromCustLookup.TryGetValue(line.CustNum!, out string? region))
                line.SalesRegion = region;
        }

        // Re-apply allowedRegions filter now that KENT lines have SalesRegion populated
        if (allowedRegions is { Count: > 0 })
        {
            lines = lines
                .Where(l => !string.IsNullOrEmpty(l.SalesRegion) && allowedRegions.Contains(l.SalesRegion))
                .ToList();
        }

        // ── 4. Group and pivot ──
        var grouped = lines
            .GroupBy(l => (CustNum: l.CustNum ?? "", CustSeq: l.CustSeq, Item: l.Item ?? ""))
            .ToList();

        var result = new List<Dictionary<string, object>>(grouped.Count);

        foreach (var group in grouped)
        {
            var first = group.First();
            custNameLookup.TryGetValue(first.CustNum ?? "", out string? custName);
            itemDescLookup.TryGetValue(first.Item ?? "", out string? itemDesc);

            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customer"]        = first.CustNum ?? "",
                ["Customer Name"]   = custName ?? "",
                ["Ship To Num"]     = first.CustSeq,
                ["Ship To City"]    = first.ShipToCity ?? "",
                ["Ship To State"]   = first.ShipToState ?? "",
                ["slsman"]          = first.Slsman ?? "",
                ["name"]            = custName ?? "",
                ["Bill To State"]   = first.BillToState ?? "",
                ["Uf_SalesRegion"]  = first.SalesRegion ?? "",
                ["RegionName"]      = first.RegionName ?? "",
                ["Item"]            = first.Item ?? "",
                ["ItemDescription"] = itemDesc ?? ""
            };

            // Revenue-only fiscal year totals (prior + current) — no _Qty suffix
            row[$"FY{fiscalYear - 1}"] = group
                .Where(l => l.InvDate >= fyPriorStart && l.InvDate <= fyPriorEnd)
                .Sum(l => l.NetRevenue);

            row[$"FY{fiscalYear}"] = group
                .Where(l => l.InvDate >= fyCurrentStart && l.InvDate <= fyCurrentEnd)
                .Sum(l => l.NetRevenue);

            // Revenue-only monthly columns for current FY — no _Rev suffix
            foreach (var monthKey in currentFYMonths)
            {
                row[monthKey] = group
                    .Where(l => string.Equals(l.Period, monthKey, StringComparison.OrdinalIgnoreCase))
                    .Sum(l => l.NetRevenue);
            }

            result.Add(row);
        }

        return result;
    }

    /// <summary>
    /// KENT fallback: Chap_InvoiceLines is a custom IDO that may not be deployed on KENT.
    /// Replicates the same data shape using standard SLInvHdrs + SLInvItemAlls.
    /// ShipToCity, BillToState, SalesRegion, and RegionName are backfilled by the caller.
    /// </summary>
    private async Task<List<InvLineRawRow>> FetchKentLinesViaStandardIdosAsync(
        string repCode,
        DateTime dateFrom,
        DateTime dateTo,
        string kentAuth)
    {
        var hdrQuery = new Dictionary<string, string>
        {
            ["props"]    = "InvNum,InvSeq,CustNum,CustSeq,InvDate,State,Disc,Slsman",
            ["filter"]   = $"{Eq("Slsman", repCode)} AND InvDate >= '{dateFrom:yyyyMMdd}' AND InvDate <= '{dateTo:yyyyMMdd}'",
            ["rowcap"]   = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var hdrResponse = Deserialize(await _csiRestClient.GetAsync("json/SLInvHdrs/adv", hdrQuery, kentAuth));

        if (hdrResponse.MessageCode != 0)
        {
            _logger.LogWarning("SLInvHdrs (KENT fallback) failed: {Msg}", hdrResponse.Message);
            return [];
        }

        var headers = hdrResponse.Items
            .Select(MapRow<KentInvHdrRaw>)
            .Where(h => !string.IsNullOrWhiteSpace(h.InvNum) && h.InvDate.HasValue)
            .ToList();

        if (headers.Count == 0)
            return [];

        var hdrLookup = headers
            .GroupBy(h => (h.InvNum!, h.InvSeq))
            .ToDictionary(g => g.Key, g => g.First());

        var invNums = headers.Select(h => h.InvNum!).Distinct().ToList();

        const int batchSize = 30;
        var rawItems = new List<KentInvItemRaw>();

        foreach (var batch in invNums.Chunk(batchSize))
        {
            var itemQuery = new Dictionary<string, string>
            {
                ["props"]    = "InvNum,InvSeq,Item,QtyInvoiced,Price",
                ["filter"]   = In("InvNum", batch),
                ["rowcap"]   = "0",
                ["loadtype"] = "FIRST",
                ["bookmark"] = "0",
                ["readonly"] = "1"
            };

            var itemResponse = Deserialize(await _csiRestClient.GetAsync("json/SLInvItemAlls/adv", itemQuery, kentAuth));

            if (itemResponse.MessageCode == 0)
                rawItems.AddRange(itemResponse.Items.Select(MapRow<KentInvItemRaw>));
            else
                _logger.LogWarning("SLInvItemAlls (KENT fallback) batch failed: {Msg}", itemResponse.Message);
        }

        var result = new List<InvLineRawRow>();

        foreach (var item in rawItems)
        {
            if (string.IsNullOrWhiteSpace(item.InvNum) || string.IsNullOrWhiteSpace(item.Item))
                continue;

            var key = (item.InvNum!, item.InvSeq);
            if (!hdrLookup.TryGetValue(key, out var hdr) || !hdr.InvDate.HasValue)
                continue;

            result.Add(new InvLineRawRow
            {
                InvDate     = hdr.InvDate,
                CustNum     = hdr.CustNum,
                CustSeq     = hdr.CustSeq,
                ShipToCity  = "",
                ShipToState = hdr.State ?? "",
                BillToState = "",
                Slsman      = hdr.Slsman ?? repCode,
                SalesRegion = "",           // backfilled by caller via SLCustomers
                RegionName  = "",
                Item        = item.Item,
                QtyInvoiced = item.QtyInvoiced,
                Price       = item.Price,
                Disc        = hdr.Disc,
                Period      = hdr.InvDate.Value.ToString("MMM") + hdr.InvDate.Value.Year
            });
        }

        _logger.LogInformation(
            "KENT fallback (SLInvHdrs + SLInvItemAlls): {Count} lines for rep {RepCode}",
            result.Count, repCode);

        return result;
    }

    // ── Private row types ──────────────────────────────────────────────────────

    // Maps to Chap_InvoiceLines IDO. ExtPrice on that IDO is qty*price with NO discount,
    // so we compute NetRevenue manually from price + disc.
    private sealed class InvLineRawRow
    {
        [CsiField("InvDate")]      public DateTime? InvDate { get; set; }
        [CsiField("CustNum")]      public string? CustNum { get; set; }
        [CsiField("CustSeq")]      public int CustSeq { get; set; }
        [CsiField("ShipToCity")]   public string? ShipToCity { get; set; }
        [CsiField("ShipToState")]  public string? ShipToState { get; set; }
        [CsiField("BillToState")]  public string? BillToState { get; set; }
        [CsiField("Slsman")]       public string? Slsman { get; set; }
        [CsiField("SalesRegion")]  public string? SalesRegion { get; set; }
        [CsiField("RegionName")]   public string? RegionName { get; set; }
        [CsiField("item")]         public string? Item { get; set; }
        [CsiField("qty_invoiced")] public decimal QtyInvoiced { get; set; }
        [CsiField("price")]        public decimal Price { get; set; }
        [CsiField("disc")]         public decimal Disc { get; set; }
        [CsiField("Period")]       public string? Period { get; set; }

        public decimal NetRevenue => QtyInvoiced * Price * (100m - Disc) / 100m;
    }

    private sealed class CustNameInfo
    {
        [CsiField("CustNum")]        public string? CustNum { get; set; }
        [CsiField("CustSeq")]        public int CustSeq { get; set; }
        [CsiField("Name")]           public string? Name { get; set; }
        [CsiField("Uf_SalesRegion")] public string? UfSalesRegion { get; set; }
    }

    private sealed class ItemDescInfo
    {
        [CsiField("Item")]        public string? Item { get; set; }
        [CsiField("Description")] public string? Description { get; set; }
    }

    private sealed class KentInvHdrRaw
    {
        [CsiField("InvNum")]  public string? InvNum { get; set; }
        [CsiField("InvSeq")]  public int InvSeq { get; set; }
        [CsiField("CustNum")] public string? CustNum { get; set; }
        [CsiField("CustSeq")] public int CustSeq { get; set; }
        [CsiField("InvDate")] public DateTime? InvDate { get; set; }
        [CsiField("State")]   public string? State { get; set; }
        [CsiField("Disc")]    public decimal Disc { get; set; }
        [CsiField("Slsman")]  public string? Slsman { get; set; }
    }

    private sealed class KentInvItemRaw
    {
        [CsiField("InvNum")]      public string? InvNum { get; set; }
        [CsiField("InvSeq")]      public int InvSeq { get; set; }
        [CsiField("Item")]        public string? Item { get; set; }
        [CsiField("QtyInvoiced")] public decimal QtyInvoiced { get; set; }
        [CsiField("Price")]       public decimal Price { get; set; }
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    private static MgRestAdvResponse Deserialize(string json) =>
        JsonSerializer.Deserialize<MgRestAdvResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static T MapRow<T>(List<MgNameValue> row) where T : new()
    {
        var obj = new T();
        foreach (var prop in typeof(T).GetProperties())
        {
            var attr = prop.GetCustomAttribute<CsiFieldAttribute>();
            if (attr == null) continue;

            var cell = row.FirstOrDefault(c =>
                string.Equals(c.Name, attr.FieldName, StringComparison.OrdinalIgnoreCase));

            if (cell?.Value == null) continue;

            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            prop.SetValue(obj, ConvertTo(cell.Value, targetType));
        }
        return obj;
    }

    private static object? ConvertTo(string? raw, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        bool isNullable = underlying != null;
        var effective = underlying ?? targetType;

        if (string.IsNullOrWhiteSpace(raw))
            return isNullable || !effective.IsValueType ? null
                : throw new FormatException($"Cannot convert null/empty to '{effective.Name}'.");

        try
        {
            if (effective == typeof(string)) return raw;

            if (effective == typeof(DateTime))
            {
                string[] fmts =
                {
                    "yyyyMMdd HH:mm:ss.fff", "yyyyMMdd",
                    "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "o"
                };
                if (DateTime.TryParseExact(raw, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt;
                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt))
                    return dt;
                throw new FormatException($"Cannot convert '{raw}' to DateTime.");
            }

            if (effective == typeof(decimal))
            {
                if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
                    return dec;
                throw new FormatException($"Cannot convert '{raw}' to decimal.");
            }

            if (effective == typeof(int))
            {
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    return i;
                if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return (int)d;
                throw new FormatException($"Cannot convert '{raw}' to int.");
            }

            if (effective == typeof(bool))
            {
                if (bool.TryParse(raw, out var b)) return b;
                if (raw == "0") return false;
                if (raw == "1") return true;
                throw new FormatException($"Cannot convert '{raw}' to bool.");
            }

            return Convert.ChangeType(raw, effective, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new FormatException($"Failed to convert '{raw}' to type '{effective.Name}'.", ex);
        }
    }

    private static string Eq(string field, string value) =>
        $"{field} = '{value.Replace("'", "''")}'";

    private static string In(string field, IEnumerable<string> values)
    {
        var safeValues = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => $"'{v.Replace("'", "''")}'");
        return $"{field} IN ({string.Join(",", safeValues)})";
    }
}
