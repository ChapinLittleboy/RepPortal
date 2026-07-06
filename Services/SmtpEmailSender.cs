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

public class SmtpEmailSender : IEmailSender, IAttachmentEmailSender, IEmailService
{
    private readonly IConfiguration _config;

    public SmtpEmailSender(IConfiguration config) => _config = config;

    // Identity UI contract (no attachments)
    public Task SendEmailAsync(string email, string subject, string htmlMessage) =>
        SendAsync(email, subject, htmlMessage, attachments: null);

    public Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        bool isHtml = true,
        List<EmailAttachment>? attachments = null)
    {
        var attachmentTuples = attachments?
            .Where(a => a.Content is { Length: > 0 })
            .Select(a => (
                FileName: a.FileName ?? "attachment",
                Bytes: a.Content!,
                ContentType: a.ContentType ?? "application/octet-stream"));

        return SendAsync(toEmail, subject, body, attachmentTuples, isHtml);
    }

    // Attachment-aware version for reports
    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        IEnumerable<(string FileName, byte[] Bytes, string ContentType)>? attachments)
    {
        await SendAsync(toEmail, subject, htmlBody, attachments, isHtml: true);
    }

    private async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        IEnumerable<(string FileName, byte[] Bytes, string ContentType)>? attachments,
        bool isHtml)
    {
        var fromName = _config["Smtp:SenderName"] ?? "Chapin Rep Portal";
        var fromEmail = _config["Smtp:SenderEmail"]
                        ?? _config["Smtp:NoReplyEmail"]
                        ?? "noreply@yourco.com";
        var host = _config["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host missing");
        var portStr = _config["Smtp:Port"] ?? "25";
        var user = _config["Smtp:Username"];
        var pass = _config.GetSmtpPassword();
        var sslMode = (_config["Smtp:SecureSocketOptions"] ?? "StartTls").Trim(); // Auto|None|SslOnConnect|StartTls

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        AddRecipients(message.To, toEmail);
        message.Subject = subject;

        var builder = isHtml
            ? new BodyBuilder { HtmlBody = body }
            : new BodyBuilder { TextBody = body };

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
            // ShouldAuthenticate guarantees user and pass are non-empty here
            await client.AuthenticateAsync(user!, pass!);
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

    private static void AddRecipients(InternetAddressList recipients, string toEmail)
    {
        var normalizedRecipients = toEmail.Replace(';', ',');
        recipients.AddRange(InternetAddressList.Parse(normalizedRecipients));
    }
}
