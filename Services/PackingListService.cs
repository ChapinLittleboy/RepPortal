using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace RepPortal.Services;

// key for a shipment we need to hydrate into a packing list
public sealed class ShipmentKey
{
    public string PackNum { get; set; } = "";
    public string Site { get; set; } = "";     // e.g., "BAT" or "KENT"
    public DateTime? PackDate { get; set; }    // optional: handy for UI
    public string? ShipCode { get; set; }      // optional: handy for UI
    public string? Carrier { get; set; }       // optional: handy for UI
}

internal sealed class SiteProcConfig
{
    public string ConnectionString { get; set; } = "";
    public string PackingListProc { get; set; } = "dbo.Rep_Rpt_PackingSlipByBOLSp";
}

public class PackingListService
{
    private readonly string _appDb;
    private readonly string _spShipmentsByOrder;
    private readonly Dictionary<string, SiteProcConfig> _siteMap;

    public PackingListService(IConfiguration cfg)
    {
        _appDb = cfg.GetConnectionString("BatAppConnection")
                 ?? throw new InvalidOperationException("Missing connection string 'BAT'.");

        _spShipmentsByOrder = cfg["Procedures:ShipmentsByOrder"]
                 ?? "dbo.usp_Shipments_ByOrder";

        // Load site map
        _siteMap = new(StringComparer.OrdinalIgnoreCase);
        var sitesSection = cfg.GetSection("Sites");
        foreach (var siteSection in sitesSection.GetChildren())
        {
            var key = siteSection.Key; // "BAT", "KENT"
            var cs = siteSection["ConnectionString"];
            var sp = siteSection["PackingListProc"] ?? "dbo.Rep_Rpt_PackingSlipByBOLSp";
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(cs))
            {
                _siteMap[key] = new SiteProcConfig
                {
                    ConnectionString = cs,
                    PackingListProc = sp
                };
            }
        }
        if (_siteMap.Count == 0)
            throw new InvalidOperationException("No site configurations found under 'Sites'.");
    }

    /// <summary>Legacy single-site call (defaults to BAT). Prefer the overload with site.</summary>
    public Task<RepPortal.Models.PackingList> GetPackingListByShipmentAsync(string packNum, CancellationToken ct = default)
        => GetPackingListByShipmentAsync(packNum, site: "BAT", ct);

    /// <summary>Get a packing list for a given pack number at a given site (BAT/KENT).</summary>
    public async Task<RepPortal.Models.PackingList> GetPackingListByShipmentAsync(
        string packNum, string site, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(packNum)) return new RepPortal.Models.PackingList();
        if (!_siteMap.TryGetValue(site ?? "", out var siteCfg))
            throw new ArgumentException($"Unknown site '{site}'. Configure it under Sites:* in appsettings.json.");

        using var conn = new SqlConnection(siteCfg.ConnectionString);
        await conn.OpenAsync(ct);

        using var cmd = new SqlCommand(siteCfg.PackingListProc, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add(new SqlParameter("@MinShipNum", SqlDbType.VarChar, 20) { Value = packNum });

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var table = new DataTable();
        table.Load(reader);

        return FromDataTable(table);
    }

    /// <summary>
    /// Return shipment keys (pack_num + site) for a customer order (co_num).
    /// Requires the proc to return at least: pack_num, site.
    /// </summary>
    public async Task<List<ShipmentKey>> GetShipmentKeysByOrderAsync(string coNum, CancellationToken ct = default)
    {
        var keys = new List<ShipmentKey>();
        if (string.IsNullOrWhiteSpace(coNum)) return keys;

        using var conn = new SqlConnection(_appDb);
        await conn.OpenAsync(ct);

        using var cmd = new SqlCommand(_spShipmentsByOrder, conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@CoNum", SqlDbType.VarChar, 30) { Value = coNum });

        using var reader = await cmd.ExecuteReaderAsync(ct);

        int ordPack = -1, ordSite = -1, ordDate = -1, ordShip = -1, ordCarrier = -1;
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            if (ordPack < 0 && name.Equals("pack_num", StringComparison.OrdinalIgnoreCase)) ordPack = i;
            else if (ordSite < 0 && name.Equals("site", StringComparison.OrdinalIgnoreCase)) ordSite = i;
            else if (ordDate < 0 && name.Equals("pack_date", StringComparison.OrdinalIgnoreCase)) ordDate = i;
            else if (ordShip < 0 && name.Equals("ship_code", StringComparison.OrdinalIgnoreCase)) ordShip = i;
            else if (ordCarrier < 0 && name.Equals("carrier", StringComparison.OrdinalIgnoreCase)) ordCarrier = i;
        }

        if (ordPack < 0 || ordSite < 0)
            throw new InvalidOperationException("ShipmentsByOrder must return at least 'pack_num' and 'site'.");

        while (await reader.ReadAsync(ct))
        {
            var k = new ShipmentKey
            {
                PackNum = reader.IsDBNull(ordPack) ? "" : Convert.ToString(reader.GetValue(ordPack), CultureInfo.InvariantCulture) ?? "",
                Site = reader.IsDBNull(ordSite) ? "" : Convert.ToString(reader.GetValue(ordSite), CultureInfo.InvariantCulture) ?? "",
                PackDate = (ordDate >= 0 && !reader.IsDBNull(ordDate)) ? Convert.ToDateTime(reader.GetValue(ordDate), CultureInfo.InvariantCulture) : (DateTime?)null,
                ShipCode = (ordShip >= 0 && !reader.IsDBNull(ordShip)) ? Convert.ToString(reader.GetValue(ordShip), CultureInfo.InvariantCulture) : null,
                Carrier = (ordCarrier >= 0 && !reader.IsDBNull(ordCarrier)) ? Convert.ToString(reader.GetValue(ordCarrier), CultureInfo.InvariantCulture) : null
            };
            if (!string.IsNullOrWhiteSpace(k.PackNum) && !string.IsNullOrWhiteSpace(k.Site))
                keys.Add(k);
        }

        // de-dupe across sites just in case
        return keys
            .GroupBy(k => (k.Site.ToUpperInvariant(), k.PackNum.ToUpperInvariant()))
            .Select(g => g.First())
            .OrderBy(k => k.PackDate ?? DateTime.MinValue)
            .ToList();
    }

    /// <summary>
    /// Given a co_num, get all (site, pack_num) keys then hydrate each into a full PackingList
    /// using the proper site's connection/procedure. Parallel with throttling.
    /// </summary>
    public async Task<List<RepPortal.Models.PackingList>> GetShipmentsByOrderAsync(
        string coNum, int maxConcurrency = 6, CancellationToken ct = default)
    {
        var keys = await GetShipmentKeysByOrderAsync(coNum);
        if (keys.Count == 0) return new();

        var sem = new SemaphoreSlim(Math.Max(1, maxConcurrency));
        var tasks = keys.Select(async k =>
        {
            await sem.WaitAsync(ct);
            try
            {
                return await GetPackingListByShipmentAsync(k.PackNum, k.Site, ct);
            }
            finally
            {
                sem.Release();
            }
        });

        var lists = await Task.WhenAll(tasks);
        return lists.Where(pl => pl is not null).ToList()!;
    }

    // ------------------- mapping helpers (same as before) -------------------

    private static RepPortal.Models.PackingList FromDataTable(DataTable table)
    {
        var list = new RepPortal.Models.PackingList();
        if (table.Rows.Count == 0) return list;

        var r0 = table.Rows[0];
        list.Header = new RepPortal.Models.PackingListHeader
        {
            PackNum = S(r0, "pack_num"),
            PackDate = DT(r0, "pack_date"),
            Whse = S(r0, "whse"),         // typically 'BAT' or 'KENT'
            CoNum = S(r0, "co_num"),
            CustNum = S(r0, "cust_num"),
            ShipCode = S(r0, "ship_code"),
            Carrier = S(r0, "carrier"),
            ShipAddr = S(r0, "ship_addr"),
            ShipAddr2 = S(r0, "ship_addr2"),
            ShipAddr3 = S(r0, "ship_addr3"),
            ShipAddr4 = S(r0, "ship_addr4"),
            ShipCity = S(r0, "ship_city"),
            ShipState = S(r0, "ship_state"),
            ShipZip = S(r0, "ship_zip"),
            CustPo = S(r0, "cust_po"),
        };

        foreach (DataRow r in table.Rows)
        {
            list.Items.Add(new RepPortal.Models.PackingListItem
            {
                CoLine = I(r, "co_line"),
                Item = S(r, "item"),
                ItemDesc = S(r, "item_desc"),
                UM = S(r, "u_m"),
                ShipmentId = S(r, "shipment_id"),
                QtyPicked = D(r, "qty_picked"),
                QtyShipped = D(r, "qty_shipped"),
            });
        }
        return list;
    }

    private static string S(DataRow r, string c) =>
        r.Table.Columns.Contains(c) && r[c] != DBNull.Value ? Convert.ToString(r[c])?.Trim() ?? "" : "";

    private static DateTime? DT(DataRow r, string c)
    {
        if (!r.Table.Columns.Contains(c) || r[c] == DBNull.Value) return null;
        if (r[c] is DateTime dt) return dt;
        return DateTime.TryParse(Convert.ToString(r[c]), out var parsed) ? parsed : null;
    }

    private static int I(DataRow r, string c)
    {
        if (!r.Table.Columns.Contains(c) || r[c] == DBNull.Value) return 0;
        if (r[c] is int i) return i;
        return int.TryParse(Convert.ToString(r[c]), out var parsed) ? parsed : 0;
    }

    private static decimal D(DataRow r, string c)
    {
        if (!r.Table.Columns.Contains(c) || r[c] == DBNull.Value) return 0m;
        if (r[c] is decimal d) return d;
        return decimal.TryParse(Convert.ToString(r[c]), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
    }
}
