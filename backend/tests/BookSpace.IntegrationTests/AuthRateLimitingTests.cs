using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    public async Task Forwarded_client_addresses_use_independent_login_partitions()
    {
        using var factory = CreateFactoryWithLimit("Login", permitLimit: 1);
        using var client = factory.CreateClient();

        var firstClient = await SendLoginAsync(client, "198.51.100.10");
        Assert.Equal(HttpStatusCode.Unauthorized, firstClient.StatusCode);

        var repeatedClient = await SendLoginAsync(client, "198.51.100.10");
        await AssertRateLimitResponseAsync(repeatedClient);

        var differentClient = await SendLoginAsync(client, "198.51.100.11");
        Assert.Equal(HttpStatusCode.Unauthorized, differentClient.StatusCode);
    }

    [Fact]
    public async Task Register_login_and_refresh_have_independent_limits_and_rejections_skip_auth_service()
    {
        var authService = new CountingAuthService();
        using var factory = new BookSpaceApiFactory(
            new Dictionary<string, string?>
            {
                ["RateLimiting:Authentication:Register:PermitLimit"] = "1",
                ["RateLimiting:Authentication:Register:WindowSeconds"] = "60",
                ["RateLimiting:Authentication:Register:SegmentsPerWindow"] = "6",
                ["RateLimiting:Authentication:Login:PermitLimit"] = "1",
                ["RateLimiting:Authentication:Login:WindowSeconds"] = "60",
                ["RateLimiting:Authentication:Login:SegmentsPerWindow"] = "6",
                ["RateLimiting:Authentication:Refresh:PermitLimit"] = "1",
                ["RateLimiting:Authentication:Refresh:WindowSeconds"] = "60",
                ["RateLimiting:Authentication:Refresh:SegmentsPerWindow"] = "6",
                ["ForwardedHeaders:ForwardLimit"] = "1"
            },
            services =>
            {
                services.RemoveAll<IAuthService>();
                services.AddSingleton<IAuthService>(authService);
            });
        using var client = factory.CreateClient();

        var register = await SendRegisterAsync(client, "198.51.100.20");
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await SendLoginAsync(client, "198.51.100.20");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var refresh = await SendRefreshAsync(client, "198.51.100.20");
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        await AssertRateLimitResponseAsync(
            await SendRegisterAsync(client, "198.51.100.20"));
        await AssertRateLimitResponseAsync(
            await SendLoginAsync(client, "198.51.100.20"));
        await AssertRateLimitResponseAsync(
            await SendRefreshAsync(client, "198.51.100.20"));

        Assert.Equal(1, authService.RegisterCalls);
        Assert.Equal(1, authService.LoginCalls);
        Assert.Equal(1, authService.RefreshCalls);
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

    [Fact]
    public async Task Password_reset_request_is_rate_limited()
    {
        using var factory = CreateFactoryWithLimit("PasswordResetRequest", permitLimit: 2);
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/password-reset/request",
                new { email = "missing-user@bookspace.local" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync(
            "/api/auth/password-reset/request",
            new { email = "missing-user@bookspace.local" });

        await AssertRateLimitResponseAsync(rejected);
    }

    [Fact]
    public async Task Password_reset_confirm_is_rate_limited()
    {
        using var factory = CreateFactoryWithLimit("PasswordResetConfirm", permitLimit: 2);
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/password-reset/confirm",
                new { token = "invalid-token", password = "Reader456!" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync(
            "/api/auth/password-reset/confirm",
            new { token = "invalid-token", password = "Reader456!" });

        await AssertRateLimitResponseAsync(rejected);
    }

    private static BookSpaceApiFactory CreateFactoryWithLimit(string endpoint, int permitLimit) =>
        new(new Dictionary<string, string?>
        {
            [$"RateLimiting:Authentication:{endpoint}:PermitLimit"] = permitLimit.ToString(),
            [$"RateLimiting:Authentication:{endpoint}:WindowSeconds"] = "60",
            [$"RateLimiting:Authentication:{endpoint}:SegmentsPerWindow"] = "6",
            ["ForwardedHeaders:ForwardLimit"] = "1"
        });

    private static async Task<HttpResponseMessage> SendRegisterAsync(
        HttpClient client,
        string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                email = "new-reader@bookspace.local",
                password = "Reader123!",
                displayName = "New Reader"
            })
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendLoginAsync(
        HttpClient client,
        string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = "missing-user@bookspace.local",
                password = "Incorrect123!"
            })
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendRefreshAsync(
        HttpClient client,
        string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { refreshToken = "test-refresh-token" })
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }

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

    private sealed class CountingAuthService : IAuthService
    {
        private static readonly AuthResponse Response = new(
            "access-token",
            "refresh-token",
            DateTimeOffset.UtcNow.AddMinutes(15),
            new UserSummary(Guid.NewGuid(), "reader@bookspace.local", "Reader", null, UserRole.USER));

        public int LoginCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public int RegisterCalls { get; private set; }

        public Task<AuthResponse> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken)
        {
            LoginCalls++;
            return Task.FromResult(Response);
        }

        public Task<AuthResponse> RefreshAsync(
            RefreshRequest request,
            CancellationToken cancellationToken)
        {
            RefreshCalls++;
            return Task.FromResult(Response);
        }

        public Task<AuthResponse> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken)
        {
            RegisterCalls++;
            return Task.FromResult(Response);
        }

        public Task RequestPasswordResetAsync(
            RequestPasswordResetRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ResetPasswordAsync(
            ResetPasswordRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LogoutAsync(
            LogoutRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public UserSummary GetMe(Guid userId) => throw new NotSupportedException();
    }
}
