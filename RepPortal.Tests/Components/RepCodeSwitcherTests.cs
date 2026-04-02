using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RepPortal.Models;
using RepPortal.Services;
using RepPortal.Shared;
using RepPortal.Tests.Support;
using Syncfusion.Blazor;

namespace RepPortal.Tests.Components;

public class RepCodeSwitcherTests : TestContext
{
    [Fact]
    public void ShouldRenderRepOptions_AndUseCurrentRepCode()
    {
        var repCodeContext = new FakeRepCodeContext("REP1", new[] { "NE" });
        var salesService = new Mock<ISalesService>();
        salesService.Setup(x => x.GetAllRepCodesAsync()).ReturnsAsync(new List<string> { "REP1", "REP2", "LAW" });
        salesService.Setup(x => x.GetRegionInfoForRepCodeAsync("LAW")).ReturnsAsync(new List<RegionItem>
        {
            new() { Region = "NE", RegionName = "North East" }
        });

        Services.AddSingleton<IRepCodeContext>(repCodeContext);
        Services.AddSingleton(new TestAuthenticationStateProvider(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Administrator")));
        Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<TestAuthenticationStateProvider>());
        Services.AddSingleton(salesService.Object);
        Services.AddSyncfusionBlazor();

        var cut = RenderComponent<RepCodeSwitcher>();

        var select = cut.Find("select");
        Assert.Equal("REP1", select.GetAttribute("value"));
        Assert.Contains("REP2", cut.Markup);
        Assert.Contains("LAW", cut.Markup);
    }

    [Fact]
    public void SetButton_ShouldOverrideRepCode()
    {
        var repCodeContext = new FakeRepCodeContext("REP1");
        var salesService = new Mock<ISalesService>();
        salesService.Setup(x => x.GetAllRepCodesAsync()).ReturnsAsync(new List<string> { "REP1", "REP2" });
        salesService.Setup(x => x.GetRegionInfoForRepCodeAsync("LAW")).ReturnsAsync(new List<RegionItem>());

        Services.AddSingleton<IRepCodeContext>(repCodeContext);
        Services.AddSingleton(new TestAuthenticationStateProvider(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Administrator")));
        Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<TestAuthenticationStateProvider>());
        Services.AddSingleton(salesService.Object);
        Services.AddSyncfusionBlazor();

        var cut = RenderComponent<RepCodeSwitcher>();

        cut.Find("select").Change("REP2");
        cut.FindAll("button").First(x => x.TextContent.Contains("Set")).Click();

        Assert.Equal("REP2", repCodeContext.CurrentRepCode);
        Assert.Equal(1, repCodeContext.OverrideCalls);
    }

    [Fact]
    public void ResetButton_ShouldResetRepCode()
    {
        var repCodeContext = new FakeRepCodeContext("REP1");
        repCodeContext.OverrideRepCode("REP2");
        var salesService = new Mock<ISalesService>();
        salesService.Setup(x => x.GetAllRepCodesAsync()).ReturnsAsync(new List<string> { "REP1", "REP2" });
        salesService.Setup(x => x.GetRegionInfoForRepCodeAsync("LAW")).ReturnsAsync(new List<RegionItem>());

        Services.AddSingleton<IRepCodeContext>(repCodeContext);
        Services.AddSingleton(new TestAuthenticationStateProvider(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Administrator")));
        Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<TestAuthenticationStateProvider>());
        Services.AddSingleton(salesService.Object);
        Services.AddSyncfusionBlazor();

        var cut = RenderComponent<RepCodeSwitcher>();

        cut.FindAll("button").First(x => x.TextContent.Contains("Reset")).Click();

        Assert.Equal("REP1", repCodeContext.CurrentRepCode);
        Assert.Equal(1, repCodeContext.ResetCalls);
    }
}
