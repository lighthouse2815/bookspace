using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class AuthRateLimitingTests
{
    private const string ExpectedMessage =
        "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.";

    [Fact]
    public async Task Login_returns_localized_api_envelope_after_client_exceeds_limit()
    {
        using var factory = CreateFactoryWithLimit("Login", permitLimit: 2);
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "missing-user@bookspace.local",
                password = "Incorrect123!"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "missing-user@bookspace.local",
            password = "Incorrect123!"
        });

        await AssertRateLimitResponseAsync(rejected);
    }

    [Fact]
    public async Task Refresh_returns_localized_api_envelope_after_client_exceeds_limit()
    {
        using var factory = CreateFactoryWithLimit("Refresh", permitLimit: 2);
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/refresh", new
            {
                refreshToken = "invalid-refresh-token"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = "invalid-refresh-token"
        });

        await AssertRateLimitResponseAsync(rejected);
    }

    private static BookSpaceApiFactory CreateFactoryWithLimit(string endpoint, int permitLimit) =>
        new(new Dictionary<string, string?>
        {
            [$"RateLimiting:Authentication:{endpoint}:PermitLimit"] = permitLimit.ToString(),
            [$"RateLimiting:Authentication:{endpoint}:WindowSeconds"] = "60",
            [$"RateLimiting:Authentication:{endpoint}:SegmentsPerWindow"] = "6"
        });

    private static async Task AssertRateLimitResponseAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter?.Delta);
        Assert.True(response.Headers.CacheControl?.NoStore);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var envelope = document.RootElement;
        Assert.False(envelope.GetProperty("success").GetBoolean());
        Assert.Equal(ExpectedMessage, envelope.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, envelope.GetProperty("data").ValueKind);
        Assert.Equal("RATE_LIMITED", envelope.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.String, envelope.GetProperty("timestamp").ValueKind);
    }
}
