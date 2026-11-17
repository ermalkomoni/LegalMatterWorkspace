using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Amanet.Tests;

public sealed class ApiIntegrationTests(AmanetApiFactory factory) : IClassFixture<AmanetApiFactory>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task OpenApi_is_available()
    {
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);

        Assert.Equal("v1", document.GetProperty("info").GetProperty("version").GetString());
        Assert.True(document.GetProperty("paths").TryGetProperty("/health", out _));
    }
}
