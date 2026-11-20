using System.Net;
using System.Text.Json;
using Amanet.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace Amanet.Tests;

public sealed class ResendEmailSenderTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Sends_plain_text_and_reports_provider_rejection(HttpStatusCode statusCode)
    {
        const string apiKey = "test-key";
        const string from = "Amanet <demo@example.test>";
        const string recipient = "reviewer@example.test";
        const string subject = "Document request";
        const string body = "Please upload the requested PDF.\n\nhttps://demo.example.test/external/upload/1#token=sample";
        using var client = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.resend.com/emails", request.RequestUri?.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(apiKey, request.Headers.Authorization?.Parameter);
            Assert.NotNull(request.Content);
            Assert.Equal("application/json", request.Content.Headers.ContentType?.MediaType);
            using var json = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
            var payload = json.RootElement;
            Assert.Equal(from, payload.GetProperty("from").GetString());
            Assert.Equal(recipient, Assert.Single(payload.GetProperty("to").EnumerateArray()).GetString());
            Assert.Equal(subject, payload.GetProperty("subject").GetString());
            Assert.Equal(body, payload.GetProperty("text").GetString());

            return new HttpResponseMessage(statusCode) { Content = new StringContent(body) };
        }));
        var sender = new ResendEmailSender(client, Options.Create(new ResendOptions { ApiKey = apiKey, FromEmail = from }));
        var cancellationToken = TestContext.Current.CancellationToken;

        if (statusCode == HttpStatusCode.OK)
        {
            await sender.SendAsync(recipient, subject, body, cancellationToken);
            return;
        }

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync(recipient, subject, body, cancellationToken));
        Assert.Equal(statusCode, error.StatusCode);
        Assert.DoesNotContain("token=sample", error.Message);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
