using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanet.Application.Services;
using Microsoft.Extensions.Options;

namespace Amanet.Infrastructure.Email;

public sealed class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
}

public sealed class ResendEmailSender(HttpClient client, IOptions<ResendOptions> options) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string textBody, CancellationToken cancellationToken)
    {
        var resend = options.Value;
        if (string.IsNullOrWhiteSpace(resend.ApiKey) || string.IsNullOrWhiteSpace(resend.FromEmail))
            throw new InvalidOperationException("Configure Resend:ApiKey and Resend:FromEmail.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resend.ApiKey);
        request.Content = JsonContent.Create(new
        {
            from = resend.FromEmail,
            to = new[] { recipient },
            subject,
            text = textBody
        });

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Resend did not accept the email.", null, response.StatusCode);
    }
}
