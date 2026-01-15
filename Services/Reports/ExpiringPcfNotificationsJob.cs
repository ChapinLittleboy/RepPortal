using System.Composition;
using RepPortal.Models;
using static RepPortal.Models.PcfExpirationNotice;

namespace RepPortal.Services.Reports;

public interface IExpiringPcfNotificationsJob
{
    Task RunAsync();
}

public sealed class ExpiringPcfNotificationsJob : IExpiringPcfNotificationsJob
{
    private readonly IPcfNotificationLogRepository _notificationLog;
    private readonly ExportService _pdfService;
    private readonly PcfService _pcfService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ExpiringPcfNotificationsJob> _logger;

    public ExpiringPcfNotificationsJob(
        IPcfNotificationLogRepository notificationLog,
        ExportService pdfService,
        PcfService pcfService,
        IEmailService emailService,
        ILogger<ExpiringPcfNotificationsJob> logger)
    {
        _notificationLog = notificationLog;
        _pdfService = pdfService;
        _pcfService = pcfService;
        _emailService = emailService;
        _logger = logger;
    }


    public async Task RunAsync()
    {
        await RunNoticeAsync(PcfNoticeType.Day30, 30);
        await RunNoticeAsync(PcfNoticeType.Day15, 15);
    }


    private async Task RunNoticeAsync(PcfNoticeType noticeType, int daysOut)
    {
        _logger.LogInformation(
            "Running PCF {NoticeType} expiration notices ({Days} days)",
            noticeType, daysOut);

        PCFHeader pcfHeader;

        var pcfs = await _pcfService.GetExpiringInDays(daysOut);

        foreach (var pcf in pcfs)
        {
            
           

            pcfHeader = await _pcfService.GetPCFHeaderWithItemsNoRepAsync(pcf);

            if (await _notificationLog.ExistsAsync(pcf, noticeType,pcfHeader.EndDate))
                continue;

            await SendNoticeAsync(pcfHeader, noticeType);
        }
    }


    private async Task SendNoticeAsync(PCFHeader pcfHeader, PcfNoticeType noticeType)
    {
        _logger.LogInformation(
            "Sending {NoticeType} PCF notice. PCF={PcfNumber}",
            noticeType, pcfHeader.PcfNumber);

        var pdfBytes = _pdfService.ExportPcfHeaderToPdf2(pcfHeader);

        var subject = noticeType switch
        {
            PcfNoticeType.Day30 => $"PCF {pcfHeader.PcfNumber} expires in 30 days",
            PcfNoticeType.Day15 => $"PCF {pcfHeader.PcfNumber} expires in 15 days",
            _ => throw new NotSupportedException()
        };

       // pcfHeader.RepEmail = "wlittleboy@chapinmfg.com;bill.littleboy@proton.me";
      //  pcfHeader.SalesMgrEmail = "bill.littleboy@gmail.com";

        var rawEmails = new[]
            {
                pcfHeader.RepEmail,          // or SalesRepEmailList
                pcfHeader.SalesMgrEmail,
                "wlittleboy@chapinmfg.com",
                "fnolan@chapinusa.com"
            }
            .Where(e => !string.IsNullOrWhiteSpace(e));

        var to = string.Join(",",
            rawEmails
                // Split on both ; and ,
                .SelectMany(e => e.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                // Trim whitespace
                .Select(e => e.Trim())
                // Remove duplicates
                .Distinct(StringComparer.OrdinalIgnoreCase)
        );
    


        await _emailService.SendEmailAsync(
            toEmail: to,   //pcfHeader.RepEmail,  // Need to add sales mgr mail
            subject: subject,
            body: BuildEmailBody(pcfHeader, noticeType),
            attachments: new List<EmailAttachment>()
            {
                new EmailAttachment
                {
                    FileName = $"PCF_{pcfHeader.PcfNumber}.pdf",
                    ContentType = "application/pdf",
                    Content = pdfBytes,
                }
            });
        
      await _notificationLog.InsertAsync(
          pcfHeader.PcfNum,
          noticeType,
          pcfHeader.EndDate,
          to);
      
    }

    private static string BuildEmailBody(PCFHeader pcfHeader, PcfNoticeType noticeType)
    {
        int days = noticeType switch
        {
            PcfNoticeType.Day30 => 30,
            PcfNoticeType.Day15 => 15,
            _ => throw new NotSupportedException($"Unsupported notice type: {noticeType}")
        };

        return $@"
This email is a notification that Price Contract Form (PCF) number {pcfHeader.PcfNumber}
for customer number {pcfHeader.CustomerNumber} ({pcfHeader.CustomerName})
is scheduled to expire in {days} days.
<p>
PCF Expiration Date:
{pcfHeader.EndDate:MMMM dd, yyyy}
<p>
Please take the necessary steps to ensure that new pricing is completed and fully
approved prior to the expiration date of this PCF in order to avoid any disruption
to customer orders.
<p>
A copy of the expiring PCF is attached to this email for your reference. If you have any questions or need assistance, please reach out to the Sales Operations
staff.
<p><p>
Thank you.
".Trim();
    }



}