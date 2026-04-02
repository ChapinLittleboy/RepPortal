using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RepPortal.Models;
using RepPortal.Pages;
using RepPortal.Services;
using RepPortal.Tests.Support;
using Syncfusion.Blazor;

namespace RepPortal.Tests.Components;

public class SubscriptionsTests : TestContext
{
    [Fact]
    public void ShouldShowEmptyMessage_WhenNoSubscriptionsExist()
    {
        var store = new FakeReportSubscriptionStore();
        var customers = new FakeCustomerLookupService();
        SetupServices(store, customers);

        var cut = RenderComponent<Subscriptions>();

        Assert.Contains("No active subscriptions.", cut.Markup);
    }

    [Fact]
    public void ShouldRenderExistingSubscription_AndRemoveIt()
    {
        var store = new FakeReportSubscriptionStore();
        store.Entries.Add(new ReportSubscriptionEntry
        {
            Id = "subs:Shipments:rep@chapinusa.com:C123:CurrentMonth",
            ReportType = ReportType.Shipments,
            Email = "rep@chapinusa.com",
            CustomerId = "C123",
            DateRangeCode = "CurrentMonth",
            Cron = "0 8 * * 1-5",
            TimeZone = "Eastern Standard Time"
        });
        var customers = new FakeCustomerLookupService();
        SetupServices(store, customers);

        var cut = RenderComponent<Subscriptions>();

        Assert.Contains("Shipments", cut.Markup);
        Assert.Contains("C123", cut.Markup);
        cut.FindAll("button").First(x => x.TextContent.Contains("Remove")).Click();

        Assert.Equal("subs:Shipments:rep@chapinusa.com:C123:CurrentMonth", store.LastRemovedId);
        Assert.Empty(store.Entries);
        Assert.Contains("No active subscriptions.", cut.Markup);
    }

    [Fact]
    public void AddSubscription_ShouldUpsertDefaultSubscription()
    {
        var store = new FakeReportSubscriptionStore();
        var customers = new FakeCustomerLookupService();
        SetupServices(store, customers);

        var cut = RenderComponent<Subscriptions>();

        cut.Find("form").Submit();

        Assert.NotNull(store.LastUpserted);
        Assert.Equal("rep@chapinusa.com", store.LastUpserted!.Email);
        Assert.Equal(ReportType.MonthlyInvoicedSales, store.LastUpserted.ReportType);
        Assert.Equal("Eastern Standard Time", store.LastUpserted.TimeZone);
    }

    private void SetupServices(FakeReportSubscriptionStore store, FakeCustomerLookupService customers)
    {
        Services.AddSingleton<IReportSubscriptionStore>(store);
        Services.AddSingleton<ICustomerLookupService>(customers);
        Services.AddSingleton(new TestAuthenticationStateProvider(new System.Security.Claims.Claim("email", "rep@chapinusa.com")));
        Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<TestAuthenticationStateProvider>());
        Services.AddSyncfusionBlazor();
    }
}
