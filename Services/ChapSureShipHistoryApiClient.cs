using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RepPortal.Services;

public interface IChapSureShipHistoryApiClient
{
    Task<IReadOnlyList<ChapHistoryHeader>> GetHeadersAsync(
        string site, IEnumerable<int> shipmentIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetShipmentIdsByOrderAsync(
        string site, string orderNum,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChapHistoryOrder>> GetOrderMappingsAsync(
        string site, IEnumerable<string> orderNums,
        CancellationToken cancellationToken = default);

    Task<HashSet<int>> GetExistingHeaderKeysAsync(
        string site, int firstShipmentId, int lastShipmentId,
        CancellationToken cancellationToken = default);

    Task<HashSet<(int ShipmentId, string OrderNum)>> GetExistingOrderKeysAsync(
        string site, int firstShipmentId, int lastShipmentId,
        CancellationToken cancellationToken = default);

    Task InsertHeadersAsync(string site, IReadOnlyCollection<ChapHistoryHeader> rows,
        CancellationToken cancellationToken = default);

    Task InsertOrdersAsync(string site, IReadOnlyCollection<ChapHistoryOrder> rows,
        CancellationToken cancellationToken = default);
}

public sealed class ChapSureShipHistoryApiClient(ICsiRestClient restClient)
    : IChapSureShipHistoryApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<IReadOnlyList<ChapHistoryHeader>> GetHeadersAsync(
        string site, IEnumerable<int> shipmentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = shipmentIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var results = new List<ChapHistoryHeader>();
        foreach (var batch in ids.Chunk(100))
        {
            var query = Query(
                "SiteRef,ShipmentId,ShipDate,CustNum,CustSeq,Whse,CarrierCode,ShipCode," +
                "TrackingNumber,ProNumber,ServiceType,BillTransportationTo,SourceRecordDate",
                $"SiteRef = '{Escape(site)}' AND ShipmentId IN ({string.Join(',', batch)})");
            var response = await GetAsync("ue_Chap_SS_History_BOLs", query, site, cancellationToken);
            results.AddRange(response.Items.Select(row => new ChapHistoryHeader(
                Value(row, "SiteRef") ?? site,
                Integer(row, "ShipmentId") ?? 0,
                Date(row, "ShipDate"),
                Value(row, "CustNum"),
                Integer(row, "CustSeq"),
                Value(row, "Whse"),
                Value(row, "CarrierCode"),
                Value(row, "ShipCode"),
                Value(row, "TrackingNumber"),
                Value(row, "ProNumber"),
                Value(row, "ServiceType"),
                Value(row, "BillTransportationTo"),
                Date(row, "SourceRecordDate")))
                .Where(row => row.ShipmentId > 0));
        }

        return results;
    }

    public async Task<IReadOnlyList<int>> GetShipmentIdsByOrderAsync(
        string site, string orderNum,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNum))
            return [];

        return (await GetOrderMappingsAsync(site, [orderNum], cancellationToken))
            .Select(mapping => mapping.ShipmentId)
            .Distinct()
            .ToArray();
    }

    public async Task<IReadOnlyList<ChapHistoryOrder>> GetOrderMappingsAsync(
        string site, IEnumerable<string> orderNums,
        CancellationToken cancellationToken = default)
    {
        var orders = orderNums
            .Where(order => !string.IsNullOrWhiteSpace(order))
            .Select(order => order.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (orders.Length == 0)
            return [];

        var results = new List<ChapHistoryOrder>();
        foreach (var batch in orders.Chunk(30))
        {
            var orderFilter = string.Join(',', batch.Select(order => $"'{Escape(order)}'"));
            var query = Query("SiteRef,ShipmentId,OrderNum",
                $"SiteRef = '{Escape(site)}' AND OrderNum IN ({orderFilter})");
            var response = await GetAsync(
                "ue_Chap_SS_History_BOLOrders", query, site, cancellationToken);
            results.AddRange(response.Items.Select(row => new ChapHistoryOrder(
                    Value(row, "SiteRef") ?? site,
                    Integer(row, "ShipmentId") ?? 0,
                    Value(row, "OrderNum")?.Trim() ?? ""))
                .Where(mapping => mapping.ShipmentId > 0
                                  && !string.IsNullOrWhiteSpace(mapping.OrderNum)));
        }

        return results;
    }

    public async Task<HashSet<int>> GetExistingHeaderKeysAsync(
        string site, int firstShipmentId, int lastShipmentId,
        CancellationToken cancellationToken = default)
    {
        var response = await LoadAsync(
            "ue_Chap_SS_History_BOLs", "ShipmentId", site,
            firstShipmentId, lastShipmentId, cancellationToken);
        return response.Items
            .Select(row => Value(row, "ShipmentId"))
            .Where(value => int.TryParse(value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out _))
            .Select(value => int.Parse(value!, CultureInfo.InvariantCulture))
            .ToHashSet();
    }

    public async Task<HashSet<(int ShipmentId, string OrderNum)>> GetExistingOrderKeysAsync(
        string site, int firstShipmentId, int lastShipmentId,
        CancellationToken cancellationToken = default)
    {
        var response = await LoadAsync(
            "ue_Chap_SS_History_BOLOrders", "ShipmentId,OrderNum", site,
            firstShipmentId, lastShipmentId, cancellationToken);
        return response.Items.Select(row =>
            {
                var shipment = Value(row, "ShipmentId");
                var order = Value(row, "OrderNum") ?? "";
                return int.TryParse(shipment, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var id)
                    ? (Valid: true, Key: (id, order.Trim()))
                    : (Valid: false, Key: default((int, string)));
            })
            .Where(item => item.Valid)
            .Select(item => item.Key)
            .ToHashSet();
    }

    public Task InsertHeadersAsync(string site, IReadOnlyCollection<ChapHistoryHeader> rows,
        CancellationToken cancellationToken = default) => InsertAsync(
        "ue_Chap_SS_History_BOLs", site,
        rows.Select(row => Item("ue_Chap_SS_History_BOLs",
        [
            Property("SiteRef", row.SiteRef),
            Property("ShipmentId", row.ShipmentId),
            Property("ShipDate", row.ShipDate),
            Property("CustNum", row.CustNum),
            Property("CustSeq", row.CustSeq),
            Property("Whse", row.Whse),
            Property("CarrierCode", row.CarrierCode),
            Property("ShipCode", row.ShipCode),
            Property("TrackingNumber", row.TrackingNumber),
            Property("ProNumber", row.ProNumber),
            Property("ServiceType", row.ServiceType),
            Property("BillTransportationTo", row.BillTransportationTo),
            Property("SourceRecordDate", row.SourceRecordDate)
        ])).ToArray(), cancellationToken);

    public Task InsertOrdersAsync(string site, IReadOnlyCollection<ChapHistoryOrder> rows,
        CancellationToken cancellationToken = default) => InsertAsync(
        "ue_Chap_SS_History_BOLOrders", site,
        rows.Select(row => Item("ue_Chap_SS_History_BOLOrders",
        [
            Property("SiteRef", row.SiteRef),
            Property("ShipmentId", row.ShipmentId),
            Property("OrderNum", row.OrderNum)
        ])).ToArray(), cancellationToken);

    private async Task<MgRestAdvResponse> LoadAsync(
        string ido, string props, string site, int firstShipmentId, int lastShipmentId,
        CancellationToken cancellationToken)
    {
        var query = Query(props,
            $"SiteRef = '{Escape(site)}' AND ShipmentId >= {firstShipmentId} AND ShipmentId <= {lastShipmentId}");
        return await GetAsync(ido, query, site, cancellationToken);
    }

    private async Task<MgRestAdvResponse> GetAsync(
        string ido, IDictionary<string, string> query, string site,
        CancellationToken cancellationToken)
    {
        var json = await restClient.GetAsync($"json/{ido}/adv", query, site, cancellationToken);
        var response = JsonSerializer.Deserialize<MgRestAdvResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"{ido} returned an empty response.");
        if (response.MessageCode != 0)
            throw new InvalidOperationException($"{ido}: {response.Message}");
        return response;
    }

    private static Dictionary<string, string> Query(string props, string filter) => new()
    {
        ["props"] = props,
        ["filter"] = filter,
        ["rowcap"] = "0",
        ["loadtype"] = "FIRST",
        ["bookmark"] = "0",
        ["readonly"] = "1"
    };

    private async Task InsertAsync(string ido, string site, MgUpdateItem[] items,
        CancellationToken cancellationToken)
    {
        if (items.Length == 0)
            return;

        var json = JsonSerializer.Serialize(items, JsonOptions);
        var responseJson = await restClient.PostJsonAsync(
            $"json/{ido}/additems", json, site: site,
            cancellationToken: cancellationToken);
        using var response = JsonDocument.Parse(responseJson);
        var root = response.RootElement;
        if (root.TryGetProperty("MessageCode", out var code) && !IsSuccess(code))
        {
            var message = root.TryGetProperty("Message", out var messageElement)
                ? messageElement.GetString()
                : "Unknown MGRest update error.";
            throw new InvalidOperationException($"{ido}: {message} (MessageCode {code})");
        }
    }

    private static bool IsSuccess(JsonElement code) => code.ValueKind switch
    {
        JsonValueKind.Number => code.TryGetInt32(out var value) && (value == 0 || value is >= 200 and < 300),
        JsonValueKind.String => int.TryParse(code.GetString(), out var value)
                                && (value == 0 || value is >= 200 and < 300),
        JsonValueKind.Null or JsonValueKind.Undefined => true,
        _ => false
    };

    private static MgUpdateItem Item(string ido, IEnumerable<MgUpdateProperty?> properties) => new()
    {
        Action = 1,
        ItemId = $"PBT=[{ido}]",
        Properties = properties.Where(property => property is not null).Cast<MgUpdateProperty>().ToArray()
    };

    private static MgUpdateProperty? Property(string name, object? value)
    {
        if (value is null)
            return null;
        var text = value switch
        {
            DateTime date => date.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
        return new MgUpdateProperty { Name = name, Value = text ?? "" };
    }

    private static string? Value(List<MgNameValue> row, string name) => row
        .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static int? Integer(List<MgNameValue> row, string name) =>
        int.TryParse(Value(row, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static DateTime? Date(List<MgNameValue> row, string name)
    {
        var text = Value(row, name);
        if (string.IsNullOrWhiteSpace(text))
            return null;
        string[] formats = ["yyyyMMdd", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fff"];
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var exact))
            return exact;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces, out var value) ? value : null;
    }

    private static string Escape(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}

public sealed record ChapHistoryHeader(
    string SiteRef, int ShipmentId, DateTime? ShipDate, string? CustNum, int? CustSeq,
    string? Whse, string? CarrierCode, string? ShipCode, string? TrackingNumber,
    string? ProNumber, string? ServiceType, string? BillTransportationTo,
    DateTime? SourceRecordDate);

public sealed record ChapHistoryOrder(string SiteRef, int ShipmentId, string OrderNum);

internal sealed class MgUpdateItem
{
    public int Action { get; init; }
    public string ItemId { get; init; } = "";
    public MgUpdateProperty[] Properties { get; init; } = [];
}

internal sealed class MgUpdateProperty
{
    public bool IsNull { get; init; }
    public bool Modified { get; init; } = true;
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
}
