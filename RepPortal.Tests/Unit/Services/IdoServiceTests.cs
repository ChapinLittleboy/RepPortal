using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RepPortal.Data;
using RepPortal.Models;
using RepPortal.Services;
using RepPortal.Tests.Support;

namespace RepPortal.Tests.Unit.Services;

public class IdoServiceTests
{
    [Fact]
    public async Task GetShipmentsDataAsync_ShouldMergeBatAndKentAndMapExtendedPrice()
    {
        var csi = new Mock<ICsiRestClient>();
        csi.Setup(client => client.GetAsync(
                "json/SLCoShips/adv",
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IDictionary<string, string> query, string site, CancellationToken _) =>
            {
                Assert.Contains("DerTotPrice", query["props"]);
                var order = site == "BAT" ? "BAT-ORDER" : "KENT-ORDER";
                var warehousePrice = site == "BAT" ? "10.50" : "20.25";
                return $$"""
                    {"Items":[[
                      {"Name":"CoCustNum","Value":"100"},
                      {"Name":"CadrName","Value":"Customer"},
                      {"Name":"CoCustPo","Value":"PO"},
                      {"Name":"CoNum","Value":"{{order}}"},
                      {"Name":"CoLine","Value":"1"},
                      {"Name":"CoiItem","Value":"ITEM"},
                      {"Name":"CoiDescription","Value":"Description"},
                      {"Name":"CoiDueDate","Value":"20251201"},
                      {"Name":"ShipDate","Value":"20251202"},
                      {"Name":"QtyShipped","Value":"2"},
                      {"Name":"CoiPrice","Value":"5"},
                      {"Name":"DerTotPrice","Value":"{{warehousePrice}}"},
                      {"Name":"ue_BolNumber","Value":null}
                    ]],"Message":"Success","MessageCode":0}
                    """;
            });

        var history = new Mock<IChapSureShipHistoryApiClient>();
        history.Setup(client => client.GetOrderMappingsAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        history.Setup(client => client.GetHeadersAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new IdoService(
            csi.Object,
            history.Object,
            Mock.Of<IDbConnectionFactory>(),
            Mock.Of<ILogger<IdoService>>(),
            Options.Create(new CsiOptions
            {
                Configurations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["BAT"] = "BAT_CONFIG",
                    ["KENT"] = "KENT_CONFIG"
                }
            }));

        var result = await service.GetShipmentsDataAsync(
            new SalesService.ShipmentsParameters
            {
                BeginShipDate = new DateTime(2025, 12, 1),
                EndShipDate = new DateTime(2025, 12, 15)
            },
            "GUL",
            null);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, row => row.OrderNumber == "BAT-ORDER" && row.ExtLinePrice == 10.50m);
        Assert.Contains(result, row => row.OrderNumber == "KENT-ORDER" && row.ExtLinePrice == 20.25m);
        csi.Verify(client => client.GetAsync(
            "json/SLCoShips/adv",
            It.IsAny<IDictionary<string, string>>(),
            "BAT",
            It.IsAny<CancellationToken>()), Times.Once);
        csi.Verify(client => client.GetAsync(
            "json/SLCoShips/adv",
            It.IsAny<IDictionary<string, string>>(),
            "KENT",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetShipmentsDataAsync_UsesUploadedHistoryWhenLiveBolIsMissing()
    {
        var csi = new Mock<ICsiRestClient>();
        csi.Setup(client => client.GetAsync(
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), "BAT",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string relativeUrl, IDictionary<string, string> _, string _, CancellationToken _) =>
                relativeUrl == "json/SLCoShips/adv"
                    ? """
                      {"Items":[[
                        {"Name":"CoCustNum","Value":"100"},
                        {"Name":"CoNum","Value":"S100"},
                        {"Name":"ShipDate","Value":"20260812"},
                        {"Name":"QtyShipped","Value":"1"},
                        {"Name":"ue_BolNumber","Value":"600001"}
                      ]],"Message":"Success","MessageCode":0}
                      """
                    : "{\"Items\":[],\"Message\":\"Success\",\"MessageCode\":0}");
        var history = new Mock<IChapSureShipHistoryApiClient>();
        history.Setup(client => client.GetHeadersAsync(
                "BAT", It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ChapHistoryHeader("BAT", 600001, new DateTime(2026, 8, 12),
                    "100", 0, "MAIN", "UPS", "GROUND", "1Z999", null,
                    "Ground", "P", null)
            ]);

        var service = CreateService(csi.Object, history.Object);
        var result = await service.GetShipmentsDataAsync(
            new SalesService.ShipmentsParameters
            {
                BeginShipDate = new DateTime(2026, 8, 1),
                EndShipDate = new DateTime(2026, 8, 31)
            }, "GUL", null);

        var row = Assert.Single(result);
        Assert.Equal(600001, row.ShipmentID);
        Assert.Equal("MAIN", row.Whse);
        Assert.Equal("UPS", row.CarrierCode);
        Assert.Equal("Ground", row.ServiceType);
        Assert.Equal("GROUND", row.ShipCode);
        Assert.Equal("1Z999", row.TrackingNumber);
    }

    [Fact]
    public async Task GetShipmentsDataAsync_ResolvesNullBolByOrderAndShipSecond()
    {
        var csi = new Mock<ICsiRestClient>();
        csi.Setup(client => client.GetAsync(
                "json/SLCoShips/adv", It.IsAny<IDictionary<string, string>>(), "BAT",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                {"Items":[[
                  {"Name":"CoCustNum","Value":"44640"},
                  {"Name":"CoNum","Value":"S001308787"},
                  {"Name":"ShipDate","Value":"20251113 04:53:45.000"},
                  {"Name":"QtyShipped","Value":"1"},
                  {"Name":"ue_BolNumber","Value":null}
                ]],"Message":"Success","MessageCode":0}
                """);
        var history = new Mock<IChapSureShipHistoryApiClient>();
        history.Setup(client => client.GetOrderMappingsAsync(
                "BAT",
                It.Is<IEnumerable<string>>(orders => orders.Contains("S001308787")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ChapHistoryOrder("BAT", 488735, "S001308787")
            ]);
        history.Setup(client => client.GetHeadersAsync(
                "BAT",
                It.Is<IEnumerable<int>>(ids => ids.Contains(488735)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ChapHistoryHeader("BAT", 488735,
                    new DateTime(2025, 11, 13, 4, 53, 45, 917),
                    "44640", 0, "BAT", "RHAWK", "EXLA", "126-0616514", null,
                    "LTL", "C", null)
            ]);

        var service = CreateService(csi.Object, history.Object);
        var result = await service.GetShipmentsDataAsync(
            new SalesService.ShipmentsParameters
            {
                BeginShipDate = new DateTime(2025, 11, 1),
                EndShipDate = new DateTime(2025, 11, 30)
            }, "GUL", null);

        var row = Assert.Single(result);
        Assert.Equal(488735, row.BolNumber);
        Assert.Equal(488735, row.ShipmentID);
        Assert.Equal("BAT", row.Whse);
        Assert.Equal("RHAWK", row.CarrierCode);
        Assert.Equal("LTL", row.ServiceType);
        Assert.Equal("EXLA", row.ShipCode);
        Assert.Equal("126-0616514", row.TrackingNumber);
        Assert.Equal("Collect", row.FreightTerms);
        csi.Verify(client => client.GetAsync(
            "json/ue_SS_Bols/adv",
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPackingListsByOrderAsync_UsesHistoryOrderMapAndHeader()
    {
        var csi = new Mock<ICsiRestClient>();
        csi.Setup(client => client.GetAsync(
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), "BAT",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string relativeUrl, IDictionary<string, string> query, string _, CancellationToken _) =>
            {
                if (relativeUrl == "json/ue_SS_Bols/adv")
                    return "{\"Items\":[],\"Message\":\"Success\",\"MessageCode\":0}";
                if (query["filter"].Contains("ue_BolNumber", StringComparison.Ordinal))
                {
                    return """
                        {"Items":[[
                          {"Name":"ue_BolNumber","Value":"600001"},
                          {"Name":"CoNum","Value":"S100"},
                          {"Name":"CoCustNum","Value":"100"},
                          {"Name":"CoCustPo","Value":"PO-1"},
                          {"Name":"CoLine","Value":"1"},
                          {"Name":"CoiItem","Value":"ITEM"},
                          {"Name":"QtyShipped","Value":"2"}
                        ]],"Message":"Success","MessageCode":0}
                        """;
                }
                return "{\"Items\":[],\"Message\":\"Success\",\"MessageCode\":0}";
            });
        var history = new Mock<IChapSureShipHistoryApiClient>();
        history.Setup(client => client.GetShipmentIdsByOrderAsync(
                "BAT", "S100", It.IsAny<CancellationToken>()))
            .ReturnsAsync([600001]);
        history.Setup(client => client.GetHeadersAsync(
                "BAT", It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ChapHistoryHeader("BAT", 600001, new DateTime(2026, 8, 12),
                    "100", 0, "MAIN", "UPS", "GROUND", null, null,
                    "Ground", "P", null)
            ]);

        var service = CreateService(csi.Object, history.Object);
        var results = await service.GetPackingListsByOrderAsync("S100");

        var packingList = Assert.Single(results);
        Assert.Equal("600001", packingList.Header.PackNum);
        Assert.Equal("S100", packingList.Header.CoNum);
        Assert.Equal("MAIN", packingList.Header.Whse);
        Assert.Equal("UPS", packingList.Header.Carrier);
        Assert.Equal("ITEM", Assert.Single(packingList.Items).Item);
    }

    [Fact]
    public void ConvertTo_ShouldReturnNull_ForNullableIntWhenInputIsBlank()
    {
        var result = InvokeConvertTo("", typeof(int?));

        Assert.Null(result);
    }

    [Fact]
    public void ConvertTo_ShouldParseDecimal_UsingInvariantCulture()
    {
        var result = InvokeConvertTo("123.45", typeof(decimal));

        Assert.Equal(123.45m, Assert.IsType<decimal>(result));
    }

    [Fact]
    public void ConvertTo_ShouldParseDateTime_FromSupportedFormat()
    {
        var result = InvokeConvertTo("20260402", typeof(DateTime));

        Assert.Equal(new DateTime(2026, 4, 2), Assert.IsType<DateTime>(result));
    }

    [Fact]
    public void ConvertTo_ShouldThrowFormatException_ForInvalidInt()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => InvokeConvertTo("not-an-int", typeof(int)));

        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public void MapRow_ShouldMapCsiFields_CaseInsensitively()
    {
        var row = new List<MgNameValue>
        {
            new() { Name = "invnum", Value = "INV-1" },
            new() { Name = "qtyinvoiced", Value = "2" },
            new() { Name = "price", Value = "7.5" },
            new() { Name = "item", Value = "ABC" }
        };

        var result = InvokeMapRow<InvoiceRptDetail>(row);

        Assert.Equal("INV-1", result.InvNum);
        Assert.Equal(2m, result.InvQty);
        Assert.Equal(7.5m, result.Price);
        Assert.Equal("ABC", result.Item);
    }

    [Fact]
    public void MapRow_ShouldMapCloudCustomerTypeDescription()
    {
        var row = new List<MgNameValue>
        {
            new() { Name = "CustType", Value = "RETAIL" },
            new() { Name = "ue_CustTypeDescription", Value = "Retail Customer" }
        };

        var result = InvokeMapRow<Customer>(row);

        Assert.Equal("RETAIL", result.BuyingGroup);
        Assert.Equal("Retail Customer", result.BuyingGroupDescription);
    }

    [Fact]
    public void MapRow_ShouldMapCloudBolProperties()
    {
        var result = InvokeMapRow(
            "BolInfo",
            new List<MgNameValue>
            {
                new() { Name = "ShipmentId", Value = "42" },
                new() { Name = "invoicee_state", Value = "NY" },
                new() { Name = "carrier_code", Value = "UPS" },
                new() { Name = "ship_date", Value = "20260812" },
                new() { Name = "tracking_number", Value = "TRACK-1" }
            });

        Assert.Equal(42, GetPropertyValue<int?>(result, "ShipmentId"));
        Assert.Equal("NY", GetPropertyValue<string>(result, "InvoiceeState"));
        Assert.Equal("UPS", GetPropertyValue<string>(result, "CarrierCode"));
        Assert.Equal(new DateTime(2026, 8, 12), GetPropertyValue<DateTime?>(result, "ShipDate"));
        Assert.Equal("TRACK-1", GetPropertyValue<string>(result, "TrackingNumber"));
    }

    [Fact]
    public void MapRow_ShouldMapCloudPackingListBolProperties()
    {
        var result = InvokeMapRow(
            "PackingListBolRow",
            new List<MgNameValue>
            {
                new() { Name = "ShipmentId", Value = "42" },
                new() { Name = "consignee_name", Value = "Customer" },
                new() { Name = "consignee_addr__1", Value = "123 Main St" },
                new() { Name = "consignee_city", Value = "Batavia" },
                new() { Name = "consignee_state", Value = "NY" }
            });

        Assert.Equal("42", GetPropertyValue<string>(result, "ShipmentId"));
        Assert.Equal("Customer", GetPropertyValue<string>(result, "ConsigneeName"));
        Assert.Equal("123 Main St", GetPropertyValue<string>(result, "ConsigneeAddr1"));
        Assert.Equal("Batavia", GetPropertyValue<string>(result, "ConsigneeCity"));
        Assert.Equal("NY", GetPropertyValue<string>(result, "ConsigneeState"));
    }

    [Fact]
    public void Eq_ShouldEscapeSingleQuotes()
    {
        var result = (string)ReflectionTestHelper.InvokeNonPublicStatic(typeof(IdoService), "Eq", "CustNum", "O'Brien")!;

        Assert.Equal("CustNum = 'O''Brien'", result);
    }

    [Fact]
    public void In_ShouldSkipBlankValues()
    {
        var result = (string)ReflectionTestHelper.InvokeNonPublicStatic(
            typeof(IdoService),
            "In",
            "SalesRegion",
            new[] { "NE", "", "SE" })!;

        Assert.Equal("SalesRegion IN ('NE','SE')", result);
    }

    [Fact]
    public void DateGt_ShouldFormatDateAsYyyyMMdd()
    {
        var result = (string)ReflectionTestHelper.InvokeNonPublicStatic(
            typeof(IdoService),
            "DateGt",
            "InvDate",
            new DateTime(2026, 4, 2))!;

        Assert.Equal("InvDate > '20260402'", result);
    }

    private static object? InvokeConvertTo(string? raw, Type targetType)
        => ReflectionTestHelper.InvokeNonPublicStatic(typeof(IdoService), "ConvertTo", raw, targetType);

    private static T InvokeMapRow<T>(List<MgNameValue> row)
        where T : new()
    {
        var method = typeof(IdoService)
            .GetMethod("MapRow", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(T));

        return (T)method.Invoke(null, new object[] { row })!;
    }

    private static object InvokeMapRow(string nestedTypeName, List<MgNameValue> row)
    {
        var nestedType = typeof(IdoService).GetNestedType(
            nestedTypeName,
            BindingFlags.NonPublic)!;
        var method = typeof(IdoService)
            .GetMethod("MapRow", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(nestedType);

        return method.Invoke(null, new object[] { row })!;
    }

    private static T? GetPropertyValue<T>(object instance, string propertyName)
        => (T?)instance.GetType().GetProperty(propertyName)!.GetValue(instance);

    private static IdoService CreateService(
        ICsiRestClient csiRestClient,
        IChapSureShipHistoryApiClient historyClient) => new(
        csiRestClient,
        historyClient,
        Mock.Of<IDbConnectionFactory>(),
        Mock.Of<ILogger<IdoService>>(),
        Options.Create(new CsiOptions
        {
            Configurations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["BAT"] = "BAT_CONFIG"
            }
        }));
}
