namespace RepPortal.Services;

using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using MimeKit;

public interface IAttachmentEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        IEnumerable<(string FileName, byte[] Bytes, string ContentType)> attachments);
}

public class SmtpEmailSender : IEmailSender, IAttachmentEmailSender
{
    private readonly IConfiguration _config;

    public SmtpEmailSender(IConfiguration config) => _config = config;

    // Identity UI contract (no attachments)
    public Task SendEmailAsync(string email, string subject, string htmlMessage) =>
        SendAsync(email, subject, htmlMessage, attachments: null);

    // Attachment-aware version for reports
    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        IEnumerable<(string FileName, byte[] Bytes, string ContentType)>? attachments)
    {
        var fromName = _config["Smtp:SenderName"] ?? "Chapin Rep Portal";
        var fromEmail = _config["Smtp:SenderEmail"] ?? "noreply@yourco.com";
        var host = _config["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host missing");
        var portStr = _config["Smtp:Port"] ?? "25";
        var user = _config["Smtp:Username"];
        var pass = _config.GetSmtpPassword();
        var sslMode = (_config["Smtp:SecureSocketOptions"] ?? "StartTls").Trim(); // Auto|None|SslOnConnect|StartTls

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };

        if (attachments != null)
        {
            foreach (var a in attachments)
            {
                builder.Attachments.Add(a.FileName, a.Bytes, ContentType.Parse(a.ContentType));
            }
        }

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        var secure = sslMode switch
        {
            "Auto" => SecureSocketOptions.Auto,
            "None" => SecureSocketOptions.None,
            "SslOnConnect" => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.StartTls // default
        };

        if (!int.TryParse(portStr, out var port)) port = 25;

        await client.ConnectAsync(host, port, secure);

        if (ShouldAuthenticate(user, pass, client.Capabilities))
        {
            await client.AuthenticateAsync(user, pass);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    internal static bool ShouldAuthenticate(string? user, string? pass, SmtpCapabilities capabilities)
    {
        return !string.IsNullOrWhiteSpace(user)
               && !string.IsNullOrWhiteSpace(pass)
               && (capabilities & SmtpCapabilities.Authentication) != 0;
    }
}
