using System.Data;
using Microsoft.Data.SqlClient;
using RepPortal.Services;

namespace ChapSureShipHistoryLoader;

internal sealed class SureShipSourceReader(string connectionString, string site)
{
    private const string HeaderSql = """
        ;WITH ReferencedShipments AS
        (
            SELECT shipment_id FROM dbo.AIT_SS_BOL_ref_charges_mst WITH (NOLOCK)
            WHERE ref_type = 'O' AND NULLIF(LTRIM(RTRIM(ref_num)), '') IS NOT NULL
            GROUP BY shipment_id
            UNION
            SELECT shipment_id FROM dbo.AIT_SS_BOL_Line_mst WITH (NOLOCK)
            WHERE ref_type = 'O' AND NULLIF(LTRIM(RTRIM(ref_num)), '') IS NOT NULL
            GROUP BY shipment_id
        ), ShipVia AS
        (
            SELECT ShipCode, MAX(ServiceType) AS ServiceType
            FROM dbo.AITShipViaMap_mst WITH (NOLOCK) GROUP BY ShipCode
        )
        SELECT TOP (@Take)
            b.shipment_id, b.ship_date, b.cust_num, b.cust_seq, b.whse,
            NULLIF(LTRIM(RTRIM(b.carrier_code)), '') AS carrier_code,
            NULLIF(LTRIM(RTRIM(b.ship_code)), '') AS ship_code,
            NULLIF(LTRIM(RTRIM(b.tracking_number)), '') AS tracking_number,
            NULLIF(LTRIM(RTRIM(b.pro_number)), '') AS pro_number,
            COALESCE(NULLIF(LTRIM(RTRIM(sc.carrier_service_type)), ''),
                     NULLIF(LTRIM(RTRIM(vm.ServiceType)), '')) AS service_type,
            b.BillTransportationTo, b.RecordDate AS SourceRecordDate
        FROM dbo.AIT_SS_BOL_mst b WITH (NOLOCK)
        JOIN ReferencedShipments r ON r.shipment_id = b.shipment_id
        LEFT JOIN dbo.shipcode_mst sc WITH (NOLOCK) ON sc.ship_code = b.ship_code
        LEFT JOIN ShipVia vm ON vm.ShipCode = b.ship_code
        WHERE b.shipment_id > @LastShipmentId
          AND (@StartDate IS NULL OR b.ship_date >= @StartDate)
          AND (@EndDateExclusive IS NULL OR b.ship_date < @EndDateExclusive)
        ORDER BY b.shipment_id;
        """;

    private const string OrderSql = """
        ;WITH ShipmentOrders AS
        (
            SELECT shipment_id, LTRIM(RTRIM(ref_num)) AS order_num
            FROM dbo.AIT_SS_BOL_Line_mst WITH (NOLOCK)
            WHERE ref_type = 'O' AND NULLIF(LTRIM(RTRIM(ref_num)), '') IS NOT NULL
            UNION
            SELECT shipment_id, LTRIM(RTRIM(ref_num))
            FROM dbo.AIT_SS_BOL_ref_charges_mst WITH (NOLOCK)
            WHERE ref_type = 'O' AND NULLIF(LTRIM(RTRIM(ref_num)), '') IS NOT NULL
        )
        SELECT TOP (@Take) o.shipment_id, o.order_num
        FROM ShipmentOrders o
        JOIN dbo.AIT_SS_BOL_mst b WITH (NOLOCK) ON b.shipment_id = o.shipment_id
        WHERE (o.shipment_id > @LastShipmentId
               OR (o.shipment_id = @LastShipmentId AND o.order_num > @LastOrderNum))
          AND (@StartDate IS NULL OR b.ship_date >= @StartDate)
          AND (@EndDateExclusive IS NULL OR b.ship_date < @EndDateExclusive)
        ORDER BY o.shipment_id, o.order_num;
        """;

    public async Task<List<ChapHistoryHeader>> ReadHeadersAsync(
        Cursor cursor, int take, DateTime? startDate, DateTime? endDateExclusive,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = Command(connection, HeaderSql, cursor, take, startDate, endDateExclusive);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        var rows = new List<ChapHistoryHeader>(take);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ChapHistoryHeader(site,
                Convert.ToInt32(reader["shipment_id"]), Date(reader, "ship_date"),
                Text(reader, "cust_num"), Number(reader, "cust_seq"), Text(reader, "whse"),
                Text(reader, "carrier_code"), Text(reader, "ship_code"),
                Text(reader, "tracking_number"), Text(reader, "pro_number"),
                Text(reader, "service_type"), Text(reader, "BillTransportationTo"),
                Date(reader, "SourceRecordDate")));
        }
        return rows;
    }

    public async Task<List<ChapHistoryOrder>> ReadOrdersAsync(
        Cursor cursor, int take, DateTime? startDate, DateTime? endDateExclusive,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = Command(connection, OrderSql, cursor, take, startDate, endDateExclusive);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        var rows = new List<ChapHistoryOrder>(take);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new ChapHistoryOrder(site, Convert.ToInt32(reader["shipment_id"]),
                Convert.ToString(reader["order_num"])!.Trim()));
        return rows;
    }

    private static SqlCommand Command(SqlConnection connection, string sql, Cursor cursor, int take,
        DateTime? startDate, DateTime? endDateExclusive)
    {
        var command = new SqlCommand(sql, connection) { CommandTimeout = 180 };
        command.Parameters.AddWithValue("@Take", take);
        command.Parameters.AddWithValue("@LastShipmentId", cursor.ShipmentId);
        command.Parameters.AddWithValue("@LastOrderNum", cursor.OrderNum);
        command.Parameters.Add("@StartDate", SqlDbType.DateTime).Value = startDate ?? (object)DBNull.Value;
        command.Parameters.Add("@EndDateExclusive", SqlDbType.DateTime).Value = endDateExclusive ?? (object)DBNull.Value;
        return command;
    }

    private static string? Text(SqlDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? null : Convert.ToString(value)?.Trim();
    }

    private static int? Number(SqlDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? null : Convert.ToInt32(value);
    }

    private static DateTime? Date(SqlDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? null : Convert.ToDateTime(value);
    }
}
