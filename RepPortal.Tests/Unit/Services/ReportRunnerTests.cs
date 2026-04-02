using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RepPortal.Models;
using RepPortal.Services;
using RepPortal.Services.ReportExport;
using RepPortal.Services.Reports;
using RepPortal.Tests.Support;

namespace RepPortal.Tests.Unit.Services;

public class ReportRunnerTests
{
    [Fact]
    public void Normalize_ShouldDropCustomerAndDateRange_ForUnscopedReportType()
    {
        object?[] args = [ReportType.OpenOrders, "C123", "CurrentMonth", null, null];

        ReflectionTestHelper.InvokeNonPublicStatic(typeof(ReportRunner), "Normalize", args);

        Assert.Null(args[3]);
        Assert.Null(args[4]);
    }

    [Fact]
    public void Normalize_ShouldKeepCustomerAndDateRange_ForScopedReportType()
    {
        object?[] args = [ReportType.InvoicedAccounts, " C123 ", "CurrentMonth", null, null];

        ReflectionTestHelper.InvokeNonPublicStatic(typeof(ReportRunner), "Normalize", args);

        Assert.Equal("C123", args[3]);
        Assert.Equal("CurrentMonth", args[4]);
    }

    [Fact]
    public void ToDateRange_ShouldReturnMinMax_ForAllDates()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        var result = ((DateTime start, DateTime end))ReflectionTestHelper.InvokeNonPublicStatic(
            typeof(ReportRunner),
            "ToDateRange",
            "AllDates",
            tz)!;

        Assert.Equal(DateTimeKind.Utc, result.start.Kind);
        Assert.True(result.start <= DateTime.MinValue.AddHours(12));
        Assert.Equal(DateTime.MaxValue, result.end);
    }

    [Fact]
    public void ClampToSqlDateTime_ShouldClampToBusinessMinimum()
    {
        var result = (DateTime)ReflectionTestHelper.InvokeNonPublicStatic(
            typeof(ReportRunner),
            "ClampToSqlDateTime",
            new DateTime(2000, 1, 1))!;

        Assert.Equal(new DateTime(2022, 9, 1), result);
    }

    [Fact]
    public async Task RunAsync_ShouldThrow_WhenReportTypeIsUnsupported()
    {
        var runner = CreateRunner();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            runner.RunAsync(new ReportRequest(ReportType.OpenOrders, "rep@chapinusa.com", null, null)));

        Assert.Contains("not implemented", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ShouldThrow_WhenUserContextCannotBeResolved()
    {
        var userContext = new Mock<IUserContextResolver>();
        userContext.Setup(x => x.ResolveByEmailAsync("rep@chapinusa.com"))
            .ReturnsAsync((UserContextResult?)null);
        var runner = CreateRunner(userContext: userContext.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(new ReportRequest(ReportType.InvoicedAccounts, "rep@chapinusa.com", null, null)));

        Assert.Contains("No user context", ex.Message);
    }

    [Fact]
    public async Task RunAsync_ShouldExecuteExpiringPcfJob_WhenReportTypeMatches()
    {
        var expiringJob = new Mock<IExpiringPcfNotificationsJob>();
        var runner = CreateRunner(expiringPcfNotificationsJob: expiringJob.Object);

        await runner.RunAsync(new ReportRequest(ReportType.ExpiringPCFNotications, "rep@chapinusa.com", null, null));

        expiringJob.Verify(x => x.RunAsync(), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ShouldSendInvoicedAccountsEmail_WhenReportTypeMatches()
    {
        var invoicedAccounts = new Mock<IInvoicedAccountsReport>();
        invoicedAccounts
            .Setup(x => x.GetAsync("REP1", It.IsAny<IReadOnlyList<string>?>(), "C123", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, null, null, null))
            .ReturnsAsync(new List<InvoiceRptDetail> { new() { InvNum = "INV1" } });
        var excel = new Mock<IExcelReportExporter>();
        excel.Setup(x => x.Export(It.IsAny<IReadOnlyList<InvoiceRptDetail>>(), It.IsAny<ExcelExportOptions>()))
            .Returns([1, 2, 3]);
        var email = new Mock<IAttachmentEmailSender>();
        var runner = CreateRunner(
            emailSender: email.Object,
            invoicedAccounts: invoicedAccounts.Object,
            excelExporter: excel.Object);

        await runner.RunAsync(new ReportRequest(ReportType.InvoicedAccounts, "rep@chapinusa.com", "C123", "CurrentMonth"));

        invoicedAccounts.VerifyAll();
        excel.Verify(x => x.Export(
            It.Is<IReadOnlyList<InvoiceRptDetail>>(r => r.Count == 1),
            It.Is<ExcelExportOptions>(o => o.WorksheetName == "InvoicedAccounts")), Times.Once);
        email.Verify(x => x.SendAsync(
            "rep@chapinusa.com",
            It.Is<string>(s => s.Contains("Invoiced Accounts")),
            It.Is<string>(b => b.Contains("Attached")),
            It.Is<IEnumerable<(string FileName, byte[] Bytes, string ContentType)>>(a =>
                a.Single().FileName.StartsWith("Invoiced_Accounts_") &&
                a.Single().ContentType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ShouldSendShipmentsEmail_WhenReportTypeMatches()
    {
        var shipments = new Mock<IShipmentsReport>();
        shipments
            .Setup(x => x.GetAsync("REP1", It.IsAny<IEnumerable<string>?>(), "C123", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<CustomerShipment> { new() { OrderNumber = "CO1" } });
        var excel = new Mock<IExcelReportExporter>();
        excel.Setup(x => x.Export(It.IsAny<IReadOnlyList<CustomerShipment>>(), It.IsAny<ExcelExportOptions>()))
            .Returns([4, 5, 6]);
        var email = new Mock<IAttachmentEmailSender>();
        var runner = CreateRunner(
            emailSender: email.Object,
            shipments: shipments.Object,
            excelExporter: excel.Object);

        await runner.RunAsync(new ReportRequest(ReportType.Shipments, "rep@chapinusa.com", "C123", "CurrentMonth"));

        shipments.VerifyAll();
        excel.Verify(x => x.Export(
            It.Is<IReadOnlyList<CustomerShipment>>(r => r.Count == 1),
            It.Is<ExcelExportOptions>(o => o.WorksheetName == "Shipments")), Times.Once);
        email.Verify(x => x.SendAsync(
            "rep@chapinusa.com",
            It.Is<string>(s => s.Contains("Shipments")),
            It.IsAny<string>(),
            It.Is<IEnumerable<(string FileName, byte[] Bytes, string ContentType)>>(a =>
                a.Single().FileName.StartsWith("Shipments_") &&
                a.Single().ContentType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")),
            Times.Once);
    }

    private static ReportRunner CreateRunner(
        IUserContextResolver? userContext = null,
        IAttachmentEmailSender? emailSender = null,
        IInvoicedAccountsReport? invoicedAccounts = null,
        IShipmentsReport? shipments = null,
        IExcelReportExporter? excelExporter = null,
        IExpiringPcfNotificationsJob? expiringPcfNotificationsJob = null)
    {
        userContext ??= Mock.Of<IUserContextResolver>(x =>
            x.ResolveByEmailAsync("rep@chapinusa.com") ==
            Task.FromResult<UserContextResult?>(new UserContextResult("REP1", new List<string> { "NE" })));

        emailSender ??= Mock.Of<IAttachmentEmailSender>();

        return new ReportRunner(
            userContext,
            emailSender,
            invoicedAccounts ?? Mock.Of<IInvoicedAccountsReport>(),
            shipments ?? Mock.Of<IShipmentsReport>(),
            excelExporter ?? Mock.Of<IExcelReportExporter>(),
            Mock.Of<ILogger<ReportRunner>>(),
            expiringPcfNotificationsJob ?? Mock.Of<IExpiringPcfNotificationsJob>());
    }
}
