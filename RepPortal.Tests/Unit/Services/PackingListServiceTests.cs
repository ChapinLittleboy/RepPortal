using System.Data;
using Moq;
using RepPortal.Models;
using RepPortal.Services;
using RepPortal.Tests.Support;

namespace RepPortal.Tests.Unit.Services;

public class PackingListServiceTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenSqlModeHasNoSitesConfigured()
    {
        var config = TestConfigurationFactory.Create(("ConnectionStrings:BatAppConnection", "Server=(local);Database=BatApp;"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new PackingListService(config, Mock.Of<IIdoService>(), TestConfigurationFactory.CreateCsiOptions(x => x.UseApi = false)));

        Assert.Contains("No site configurations found", ex.Message);
    }

    [Fact]
    public async Task GetPackingListByShipmentAsync_ShouldUseIdoService_WhenApiModeIsEnabled()
    {
        var expected = new PackingList { Header = new PackingListHeader { PackNum = "P100" } };
        var ido = new Mock<IIdoService>();
        ido.Setup(x => x.GetPackingListByShipmentAsync("P100")).ReturnsAsync(expected);
        var sut = CreateService(ido: ido.Object, useApi: true);

        var result = await sut.GetPackingListByShipmentAsync("P100");

        Assert.Same(expected, result);
        ido.Verify(x => x.GetPackingListByShipmentAsync("P100"), Times.Once);
    }

    [Fact]
    public async Task GetPackingListByShipmentAsync_ShouldReturnEmpty_WhenPackNumIsBlankInSqlMode()
    {
        var sut = CreateService(useApi: false);

        var result = await sut.GetPackingListByShipmentAsync(" ", "BAT");

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Header.PackNum);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetShipmentKeysByOrderAsync_ShouldReturnEmpty_WhenCoNumIsBlank()
    {
        var sut = CreateService(useApi: true);

        var result = await sut.GetShipmentKeysByOrderAsync(" ");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetShipmentsByOrderAsync_ShouldReturnEmpty_WhenApiModeReturnsNoShipments()
    {
        var ido = new Mock<IIdoService>();
        ido.Setup(x => x.GetPackingListsByOrderAsync("CO100")).ReturnsAsync(new List<PackingList>());
        var sut = CreateService(ido: ido.Object, useApi: true);

        var result = await sut.GetShipmentsByOrderAsync("CO100");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPackingListByShipmentAsync_ShouldThrowForUnknownSite_WhenSqlMode()
    {
        var sut = CreateService(useApi: false);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.GetPackingListByShipmentAsync("P100", "NOPE"));

        Assert.Contains("Unknown site", ex.Message);
    }

    [Fact]
    public void FromDataTable_ShouldMapHeaderAndItems()
    {
        var table = new DataTable();
        table.Columns.Add("pack_num", typeof(string));
        table.Columns.Add("pack_date", typeof(DateTime));
        table.Columns.Add("whse", typeof(string));
        table.Columns.Add("co_num", typeof(string));
        table.Columns.Add("cust_num", typeof(string));
        table.Columns.Add("ship_code", typeof(string));
        table.Columns.Add("carrier", typeof(string));
        table.Columns.Add("ship_addr", typeof(string));
        table.Columns.Add("ship_city", typeof(string));
        table.Columns.Add("ship_state", typeof(string));
        table.Columns.Add("ship_zip", typeof(string));
        table.Columns.Add("cust_po", typeof(string));
        table.Columns.Add("co_line", typeof(int));
        table.Columns.Add("item", typeof(string));
        table.Columns.Add("item_desc", typeof(string));
        table.Columns.Add("u_m", typeof(string));
        table.Columns.Add("shipment_id", typeof(string));
        table.Columns.Add("qty_picked", typeof(decimal));
        table.Columns.Add("qty_shipped", typeof(decimal));
        table.Rows.Add("P100", new DateTime(2026, 4, 2), "BAT", "CO1", "C1", "UPS", "Carrier", "Acme", "Akron", "OH", "44301", "PO1", 1, "ITEM1", "Widget", "EA", "P100", 2m, 2m);

        var result = (PackingList)ReflectionTestHelper.InvokeNonPublicStatic(typeof(PackingListService), "FromDataTable", table)!;

        Assert.Equal("P100", result.Header!.PackNum);
        Assert.Equal("CO1", result.Header.CoNum);
        Assert.Single(result.Items);
        Assert.Equal("ITEM1", result.Items[0].Item);
        Assert.Equal(2m, result.Items[0].QtyShipped);
    }

    private static PackingListService CreateService(IIdoService? ido = null, bool useApi = false)
    {
        var config = TestConfigurationFactory.Create(
            ("ConnectionStrings:BatAppConnection", "Server=(local);Database=BatApp;"),
            ("Sites:BAT:ConnectionString", "Server=(local);Database=BatSite;"),
            ("Sites:BAT:PackingListProc", "dbo.Rep_Rpt_PackingSlipByBOLSp"));

        return new PackingListService(
            config,
            ido ?? Mock.Of<IIdoService>(),
            TestConfigurationFactory.CreateCsiOptions(x => x.UseApi = useApi));
    }
}
