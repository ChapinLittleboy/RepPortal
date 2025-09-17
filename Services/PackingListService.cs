using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Data.SqlClient;
using RepPortal.Models;

namespace RepPortal.Services;

public class PackingListService
{
    private readonly string _connString;

    // Adjust to your actual proc names
    private const string SpGetPackingListByShipment = "dbo.Rep_Rpt_PackingSlipByBOLSp";
    private const string SpGetShipmentsByOrder = "dbo.Rep_GetShipmentsByOrderNum_sp";

    public PackingListService(IConfiguration cfg)
    {
        _connString = cfg.GetConnectionString("BatAppConnection")
            ?? throw new InvalidOperationException("Missing connection string 'BatAppConnection'.");
    }

    /// <summary>
    /// Get a single PackingList by shipment (pack_num).
    /// Uses ExecuteReaderAsync + DataTable.Load.
    /// </summary>
    public async Task<PackingList> GetPackingListByShipmentAsync(string packNum, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(packNum)) return new();

        using var conn = new SqlConnection(_connString);
        await conn.OpenAsync(ct);

        using var cmd = new SqlCommand(SpGetPackingListByShipment, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add(new SqlParameter("@MinShipNum", SqlDbType.VarChar, 20) { Value = packNum });

        using var reader = await cmd.ExecuteReaderAsync(ct); // async execution of stored proc
        var table = new DataTable();
        table.Load(reader); // hydrate DataTable from IDataReader

        return FromDataTable(table);
    }

    /// <summary>
    /// Given a Customer Order (co_num), call SpGetShipmentsByOrder to get all shipments
    /// (expects at least a 'pack_num' column), then build a PackingList for each shipment.
    /// Returns a list of fully-hydrated PackingLists.
    /// </summary>
    /// <param name="coNum">Customer order number</param>
    /// <param name="maxConcurrency">Throttle parallel DB calls (default 6)</param>
    public async Task<List<PackingList>> GetShipmentsByOrderAsync(
        string coNum,
        int maxConcurrency = 6,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(coNum)) return new();

        // 1) Query for shipments by order
        var packNums = new List<string>();
        using (var conn = new SqlConnection(_connString))
        {
            await conn.OpenAsync(ct);

            using var cmd = new SqlCommand(SpGetShipmentsByOrder, conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add(new SqlParameter("@CoNum", SqlDbType.VarChar, 30) { Value = coNum });

            using var reader = await cmd.ExecuteReaderAsync(ct); // async SP call
            var hasPackNum = false;
            int ordPackNum = -1;

            // Try to resolve ordinal once for speed
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), "pack_num", StringComparison.OrdinalIgnoreCase))
                {
                    hasPackNum = true;
                    ordPackNum = i;
                    break;
                }
            }

            if (!hasPackNum)
                return new(); // Nothing we can do without pack numbers

            while (await reader.ReadAsync(ct)) // async iteration
            {
                string? pn = reader.IsDBNull(ordPackNum) ? null : Convert.ToString(reader.GetValue(ordPackNum), CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(pn))
                    packNums.Add(pn.Trim());
            }
        }

        var distinctPackNums = packNums
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctPackNums.Count == 0) return new();

        // 2) Build packing lists for each shipment, with throttled parallelism
        var semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrency));
        var tasks = distinctPackNums.Select(async pn =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                return await GetPackingListByShipmentAsync(pn, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(pl => pl is not null).ToList();
    }

    // --- Mapping helpers (same columns as your proc output) ---

    private static PackingList FromDataTable(DataTable table)
    {
        var list = new PackingList();
        if (table.Rows.Count == 0) return list;

        var r0 = table.Rows[0];
        list.Header = new PackingListHeader
        {
            PackNum = S(r0, "pack_num"),
            PackDate = DT(r0, "pack_date"),
            Whse = S(r0, "whse"),
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
            list.Items.Add(new PackingListItem
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
