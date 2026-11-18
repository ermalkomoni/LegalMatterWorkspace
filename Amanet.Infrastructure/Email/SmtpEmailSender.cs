using Amanet.Application.Services;
using Amanet.Core.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Amanet.Infrastructure.Email;

public sealed class ExternalLinkOptions
{
    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";
    public int ExpiryDays { get; set; } = 7;

    public bool IsValid() => ExpiryDays is > 0 and <= 365 &&
        Uri.TryCreate(FrontendBaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback)) &&
        uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;
    public string FromEmail { get; set; } = "noreply@example.test";
    public string FromName { get; set; } = "Amanet";
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, IHostEnvironment environment) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string textBody, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        if (string.IsNullOrWhiteSpace(smtp.Host) || smtp.Port is < 1 or > 65535 ||
            smtp.Security is not (SecureSocketOptions.StartTls or SecureSocketOptions.SslOnConnect or SecureSocketOptions.None) ||
            (smtp.Security == SecureSocketOptions.None && !environment.IsDevelopment() && !environment.IsEnvironment("Testing")) ||
            string.IsNullOrEmpty(smtp.Username) != string.IsNullOrEmpty(smtp.Password))
            throw new InvalidOperationException("SMTP configuration is incomplete.");

        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromEmail));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = textBody };
        // No protocol logger; the server may echo message content in its responses/errors.
        using var client = new SmtpClient { Timeout = 30000 };
        await client.ConnectAsync(smtp.Host, smtp.Port, smtp.Security, cancellationToken);
        if (!string.IsNullOrEmpty(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password!, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        // Dispose closes the connection. A QUIT failure after acceptance must not report failed delivery.
    }
}

internal static class ExternalEmailTemplates
{
    public static (string Subject, string Body) Render(EmailTemplate template, string title, string url) => template switch
    {
        EmailTemplate.DocumentRequest => ("Document request", $"Please upload the requested PDF: {title}\n\n{url}"),
        EmailTemplate.SignatureInvitation => ("Signature invitation", $"Please review and sign: {title}\n\n{url}"),
        EmailTemplate.Correction => ("Document correction requested", $"Please review the request and provide the corrected PDF: {title}\n\n{url}"),
        EmailTemplate.Reminder => ("Request reminder", $"A reminder about your request: {title}\n\n{url}"),
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };
}
