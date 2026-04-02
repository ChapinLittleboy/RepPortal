using Microsoft.Extensions.Logging;
using Moq;
using RepPortal.Models;
using RepPortal.Services;
using RepPortal.Tests.Support;

namespace RepPortal.Tests.Unit.Services;

public class SubscriptionServiceTests
{
    [Fact]
    public void Normalize_ShouldKeepScopedValues_ForScopedReportType()
    {
        object?[] args = [ReportType.InvoicedAccounts, "  C123  ", "CurrentMonth", null, null];

        ReflectionTestHelper.InvokeNonPublicStatic(typeof(SubscriptionService), "Normalize", args);

        Assert.Equal("C123", args[3]);
        Assert.Equal("CurrentMonth", args[4]);
    }

    [Fact]
    public void Normalize_ShouldDropScopeValues_ForUnscopedReportType()
    {
        object?[] args = [ReportType.OpenOrders, "C123", "CurrentMonth", null, null];

        ReflectionTestHelper.InvokeNonPublicStatic(typeof(SubscriptionService), "Normalize", args);

        Assert.Null(args[3]);
        Assert.Null(args[4]);
    }

    [Fact]
    public void Normalize_ShouldDropInvalidDateRangeCode()
    {
        object?[] args = [ReportType.Shipments, "C123", "Nope", null, null];

        ReflectionTestHelper.InvokeNonPublicStatic(typeof(SubscriptionService), "Normalize", args);

        Assert.Equal("C123", args[3]);
        Assert.Null(args[4]);
    }

    [Fact]
    public void BuildJobId_ShouldUseDefaults_WhenScopeValuesAreMissing()
    {
        var jobId = (string)ReflectionTestHelper.InvokeNonPublicStatic(
            typeof(SubscriptionService),
            "BuildJobId",
            ReportType.OpenOrders,
            "rep@chapinusa.com",
            null,
            null)!;

        Assert.Equal("subs:OpenOrders:rep@chapinusa.com:ALL:DEFAULT", jobId);
    }

    [Fact]
    public void SafeFindTimeZone_ShouldReturnFallback_WhenZoneIsInvalid()
    {
        var tz = (TimeZoneInfo)ReflectionTestHelper.InvokeNonPublicStatic(
            typeof(SubscriptionService),
            "SafeFindTimeZone",
            "Definitely/NotAZone")!;

        Assert.True(tz.Id is "Eastern Standard Time" or "UTC");
    }

    [Fact]
    public void RegisterOrUpdateJob_ShouldSendNormalizedValues_ToScheduler()
    {
        var scheduler = new Mock<IRecurringJobScheduler>();
        var sut = new SubscriptionService(scheduler.Object, Mock.Of<ILogger<SubscriptionService>>());

        sut.RegisterOrUpdateJob(
            ReportType.InvoicedAccounts,
            "rep@chapinusa.com",
            " C123 ",
            "CurrentMonth",
            "0 8 * * *",
            "Eastern Standard Time");

        scheduler.Verify(x => x.AddOrUpdate<ReportRunner>(
            "subs:InvoicedAccounts:rep@chapinusa.com:C123:CurrentMonth",
            "reports",
            It.IsAny<System.Linq.Expressions.Expression<Func<ReportRunner, Task>>>(),
            "0 8 * * *",
            It.Is<Hangfire.RecurringJobOptions>(o => o.TimeZone.Id == "Eastern Standard Time" || o.TimeZone.Id == "UTC")),
            Times.Once);
    }

    [Fact]
    public void RemoveJob_ShouldUseNormalizedJobId()
    {
        var scheduler = new Mock<IRecurringJobScheduler>();
        var sut = new SubscriptionService(scheduler.Object, Mock.Of<ILogger<SubscriptionService>>());

        sut.RemoveJob(ReportType.OpenOrders, "rep@chapinusa.com", "ignored", "ignored");

        scheduler.Verify(x => x.RemoveIfExists("subs:OpenOrders:rep@chapinusa.com:ALL:DEFAULT"), Times.Once);
    }
}
