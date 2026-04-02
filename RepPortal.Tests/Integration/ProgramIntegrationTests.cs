using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RepPortal.Services;
using RepPortal.Services.ReportExport;

namespace RepPortal.Tests.Integration;

public class ProgramIntegrationTests : IClassFixture<RepPortalWebApplicationFactory>
{
    private readonly RepPortalWebApplicationFactory _factory;

    public ProgramIntegrationTests(RepPortalWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Services_ShouldResolveCoreRegistrations()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        Assert.NotNull(provider.GetService<ISalesService>());
        Assert.NotNull(provider.GetService<IIdoService>());
        Assert.NotNull(provider.GetService<PackingListService>());
        Assert.NotNull(provider.GetService<SubscriptionService>());
        Assert.NotNull(provider.GetService<IInsuranceRequestService>());
        Assert.NotNull(provider.GetService<IExcelReportExporter>());
        Assert.NotNull(provider.GetService<IRecurringJobScheduler>());
    }

    [Fact]
    public async Task InsuranceRequestEndpoint_ShouldRequireAuthorization()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("{}"), "data");
        var response = await client.PostAsync("/api/InsuranceRequest", form);

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Unexpected status code: {response.StatusCode}");
    }

    [Fact]
    public async Task ChapinrepPath_ShouldRedirectToRoot()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/chapinrep/test");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }
}
