using System.Net;
using System.Net.Mail;

namespace RepPortal.Services;


public interface IEmailService
{
    Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        bool isHtml = true,
        List<EmailAttachment> attachments = null
    );
}

public class EmailAttachment
{
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public byte[] Content { get; set; }
}



public class SmtpEmailService : IEmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _fromAddress;

    public SmtpEmailService(IConfiguration config)
    {
        // Use your Exchange server's SMTP settings
        _smtpHost = config["Smtp:Host"] ?? "CIIEXCH16";
        _smtpPort = int.TryParse(config["Smtp:Port"], out var port) ? port : 25;
        _fromAddress = config["Smtp:NoReplyEmail"] ?? "noreply@chapinmfg.com";
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        bool isHtml = true,
        List<EmailAttachment> attachments = null)
    {
        using var message = new MailMessage();
        message.From = new MailAddress(_fromAddress);
        message.To.Add(toEmail);
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = isHtml;

        if (attachments != null)
        {
            foreach (var att in attachments)
            {
                var stream = new MemoryStream(att.Content);
                message.Attachments.Add(new Attachment(stream, att.FileName, att.ContentType));
            }
        }

        using var smtp = new SmtpClient(_smtpHost, _smtpPort)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = true // Uses the server's credentials (no username/pw)
        };

        await smtp.SendMailAsync(message);
    }
}
