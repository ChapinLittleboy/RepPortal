namespace RepPortal.Services;

using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;

    public SmtpEmailSender(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Chapin Rep Portal", _config["Smtp:SenderEmail"]));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlMessage };

        using var client = new SmtpClient();

        // Use ConnectAsync without SSL, for port 25 or startTLS on 587
        await client.ConnectAsync(_config["Smtp:Host"], int.Parse(_config["Smtp:Port"]), MailKit.Security.SecureSocketOptions.None);

        // var username = _config["Smtp:Username"];
        // var password = _config["Smtp:Password"];

        // if (!string.IsNullOrEmpty(username))
        //  await client.AuthenticateAsync(username, password);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
