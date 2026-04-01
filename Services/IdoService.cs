using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using RepPortal.Data;
using RepPortal.Models;

namespace RepPortal.Services;

public class IdoService : IIdoService
{
    private readonly ICsiRestClient _csiRestClient;
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly ILogger<IdoService> _logger;
    private readonly CsiOptions _csiOptions;

    public IdoService(
        ICsiRestClient csiRestClient,
        IDbConnectionFactory dbConnectionFactory,
        ILogger<IdoService> logger,
        IOptions<CsiOptions> csiOptions)
    {
        _csiRestClient = csiRestClient;
        _dbConnectionFactory = dbConnectionFactory;
        _logger = logger;
        _csiOptions = csiOptions.Value;
    }

    public async Task<List<Customer>> GetCustomersDetailsByRepCodeAsync(string repCode)
    {
        // Fetch exclusion codes from RepPortal DB to post-filter in memory
        List<string> excludedCodes;
        using (var repConn = _dbConnectionFactory.CreateRepConnection())
        {
            excludedCodes = (await repConn.QueryAsync<string>(
                "SELECT Code FROM dbo.CreditHoldReasonCodeExclusions")).ToList();
        }

        var excludedSet = new HashSet<string>(excludedCodes, StringComparer.OrdinalIgnoreCase);

        var query = new Dictionary<string, string>
        {
            ["props"] = "CustNum,Name,CorpCust,Addr_1,Addr_2,Addr_3,Addr_4,City,StateCode,Zip,Country," +
                        "Slsman,CreditHold,CreditHoldDate,CreditHoldReason,CreditHoldDescription," +
                        "Stat,CustType,CustTypeDescription",
            ["filter"] = BuildRepCodeFilter(repCode),
            ["rowcap"] = "0",
            ["loadtype"] = "FIRST",
            ["bookmark"] = "0",
            ["readonly"] = "1"
        };

        var json = await _csiRestClient.GetAsync("json/SLCustomers/adv", query);
        var response = Deserialize(json);

        if (response.MessageCode != 0)
            throw new InvalidOperationException(response.Message);

        _logger.LogInformation(
            "GetCustomersDetailsByRepCodeAsync: {Count} rows from IDO for rep {RepCode}",
            response.Items.Count, repCode);

        var customers = response.Items
            .Select(MapRow<Customer>)
            .Where(c => string.IsNullOrWhiteSpace(c.CreditHoldReason)
                        || !excludedSet.Contains(c.CreditHoldReason))
            .ToList();

        // Enrich with SalesManagerName via SQL lookup on Chap_SalesManagers
        var managerInitials = customers
            .Where(c => !string.IsNullOrWhiteSpace(c.SalesManager))
            .Select(c => c.SalesManager!)
            .Distinct()
            .ToList();

        if (managerInitials.Count > 0)
        {
            using var batConn = _dbConnectionFactory.CreateBatConnection();
            var managers = (await batConn.QueryAsync<(string Initials, string Name)>(
                "SELECT SalesManagerInitials AS Initials, SalesManagerName AS Name FROM Chap_SalesManagers WHERE SalesManagerInitials IN @Initials",
                new { Initials = managerInitials })).ToList();

            var managerLookup = managers.ToDictionary(
                m => m.Initials,
                m => m.Name,
                StringComparer.OrdinalIgnoreCase);

            foreach (var c in customers)
            {
                if (string.IsNullOrWhiteSpace(c.SalesManager))
                    continue;

                c.SalesManagerName = managerLookup.TryGetValue(c.SalesManager, out string? name)
                    ? name
                    : "To Be Assigned";
            }
        }

        return customers.OrderBy(c => c.Cust_Name).ToList();
    }

    private static string BuildRepCodeFilter(string repCode)
    {
        if (repCode.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return "CustSeq = 0";

        if (repCode.Equals("DAL", StringComparison.OrdinalIgnoreCase))
        {
            var specialCusts = In("CustNum", new[] { "  45424", "  45427", "  45424K", "45424", "45427", "45424K" });
            return $"CustSeq = 0 AND ({Eq("Slsman", "DAL")} OR {specialCusts})";
        }

        return $"CustSeq = 0 AND {Eq("Slsman", repCode)}";
    }

    private static MgRestAdvResponse Deserialize(string json) =>
        JsonSerializer.Deserialize<MgRestAdvResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static T MapRow<T>(List<MgNameValue> row)
        where T : new()
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
            try
            {
                prop.SetValue(obj, ConvertTo(cell.Value, targetType));
            }
            catch (FormatException)
            {
                // Leave property at its default value if conversion fails
            }
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
            string[] formats = { "yyyyMMdd HH:mm:ss.fff", "yyyyMMdd", "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "o" };
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
            // SyteLine may return boolean strings for bit fields
            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase)) return 1;
            if (raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return 0;
            throw new FormatException($"Cannot convert '{raw}' to int.");
        }

        return Convert.ChangeType(raw, effectiveType, CultureInfo.InvariantCulture);
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
