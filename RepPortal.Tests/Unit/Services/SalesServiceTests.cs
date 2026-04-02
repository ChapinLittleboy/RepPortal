using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RepPortal.Data;
using RepPortal.Models;
using RepPortal.Services;
using RepPortal.Tests.Support;

namespace RepPortal.Tests.Unit.Services;

public class SalesServiceTests
{
    [Fact]
    public async Task GetRepCodeByRegistrationCodeAsync_ShouldReturnNull_WhenCodeIsBlank()
    {
        var service = CreateService();

        var result = await service.GetRepCodeByRegistrationCodeAsync("   ");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCustNumFromCoNum_ShouldReturnNull_WhenCoNumIsBlank()
    {
        var service = CreateService();

        var result = await service.GetCustNumFromCoNum(" ");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRepIDAsync_ShouldReturnNull_WhenClaimIsMissing()
    {
        var service = CreateService(authenticationStateProvider: new TestAuthenticationStateProvider());

        var result = await service.GetRepIDAsync();

        Assert.Null(result);
    }

    [Fact]
    public void GetCurrentRepCode_ShouldReturnCurrentRepCode_FromContext()
    {
        var repCodeContext = new Mock<IRepCodeContext>();
        repCodeContext.SetupGet(x => x.CurrentRepCode).Returns("TESTCODE");
        var service = CreateService(repCodeContext: repCodeContext.Object);

        var result = service.GetCurrentRepCode();

        Assert.Equal("TESTCODE", result);
    }

    [Fact]
    public async Task GetRepIDAsync_ShouldThrow_WhenAuthProviderIsUnavailable()
    {
        var service = new SalesService("Server=(local);Database=RepPortal;Trusted_Connection=True;");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetRepIDAsync());

        Assert.Contains("AuthenticationStateProvider", ex.Message);
    }

    [Fact]
    public void GetCurrentRepCode_ShouldThrow_WhenRepContextIsUnavailable()
    {
        var service = new SalesService("Server=(local);Database=RepPortal;Trusted_Connection=True;");

        var ex = Assert.Throws<InvalidOperationException>(() => service.GetCurrentRepCode());

        Assert.Contains("IRepCodeContext", ex.Message);
    }

    [Fact]
    public void GetDynamicQueryForItemsMonthlyWithQty_ShouldIncludeRegionFilter_WhenRegionsProvided()
    {
        var service = CreateService();

        var query = service.GetDynamicQueryForItemsMonthlyWithQty(new[] { "NE", "SE" });

        Assert.Contains("cu.Uf_SalesRegion IN @AllowedRegions", query);
    }

    [Fact]
    public void GetDynamicQueryForItemsMonthlyWithQty_ShouldNotIncludeRegionFilter_WhenRegionsMissing()
    {
        var service = CreateService();

        var query = service.GetDynamicQueryForItemsMonthlyWithQty();

        Assert.DoesNotContain("cu.Uf_SalesRegion IN @AllowedRegions", query);
    }

    [Fact]
    public void BuildSalesPivotQuery_ShouldReturnFiscalYearQuery()
    {
        var service = CreateService();

        var result = ((string query, int fiscalYear))ReflectionTestHelper.InvokeNonPublicInstanceWithArguments(
            service,
            "BuildSalesPivotQuery",
            new object?[] { null })!;

        Assert.False(string.IsNullOrWhiteSpace(result.query));
        Assert.Contains($"FY{result.fiscalYear}", result.query);
        Assert.Contains("PIVOT", result.query);
    }

    private static SalesService CreateService(
        IConfiguration? configuration = null,
        AuthenticationStateProvider? authenticationStateProvider = null,
        IRepCodeContext? repCodeContext = null)
    {
        configuration ??= TestConfigurationFactory.Create(("ConnectionStrings:BatAppConnection", "Server=(local);Database=BatApp;Trusted_Connection=True;"));
        authenticationStateProvider ??= new TestAuthenticationStateProvider();
        repCodeContext ??= Mock.Of<IRepCodeContext>(x => x.CurrentRepCode == "REP1" && x.CurrentRegions == new List<string>());

        return new SalesService(
            configuration,
            authenticationStateProvider,
            repCodeContext,
            Mock.Of<IDbConnectionFactory>(),
            Mock.Of<ILogger<SalesService>>(),
            Mock.Of<ISalesDataService>(),
            Mock.Of<IIdoService>(),
            Mock.Of<ICsiRestClient>(),
            Options.Create(new CsiOptions()));
    }
}
