using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Application.Abstractions;
using BookSpace.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookSpace.IntegrationTests;

public sealed class PasswordRecoveryFlowTests
{
    [Fact]
    public async Task Request_is_enumeration_safe_and_only_delivers_for_available_account()
    {
        var sender = new RecordingPasswordResetEmailSender();
        using var factory = CreateFactory(sender);
        using var client = factory.CreateClient();
        var email = $"recovery-{Guid.NewGuid():N}@bookspace.local";
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password = "Reader123!",
                displayName = "Độc giả khôi phục"
            })).StatusCode);

        var existing = await client.PostAsJsonAsync(
            "/api/auth/password-reset/request",
            new { email });
        var missing = await client.PostAsJsonAsync(
            "/api/auth/password-reset/request",
            new { email = $"missing-{Guid.NewGuid():N}@bookspace.local" });

        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        var existingEnvelope = await ReadEnvelopeAsync(existing);
        var missingEnvelope = await ReadEnvelopeAsync(missing);
        Assert.Equal(
            existingEnvelope.GetProperty("message").GetString(),
            missingEnvelope.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, existingEnvelope.GetProperty("data").ValueKind);
        Assert.Equal(JsonValueKind.Null, missingEnvelope.GetProperty("data").ValueKind);
        var delivered = Assert.Single(sender.Messages);
        Assert.Equal(email, delivered.RecipientEmail);
        Assert.DoesNotContain(delivered.Token, await existing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Confirm_changes_password_invalidates_sessions_and_consumes_hashed_token_once()
    {
        var sender = new RecordingPasswordResetEmailSender();
        using var factory = CreateFactory(sender);
        using var client = factory.CreateClient();
        var email = $"reset-{Guid.NewGuid():N}@bookspace.local";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Reader123!",
            displayName = "Độc giả đổi mật khẩu"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var auth = (await ReadEnvelopeAsync(register)).GetProperty("data");
        var accessToken = auth.GetProperty("accessToken").GetString();
        var refreshToken = auth.GetProperty("refreshToken").GetString();

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                "/api/auth/password-reset/request",
                new { email })).StatusCode);
        var resetMessage = Assert.Single(sender.Messages);

        var confirm = await client.PostAsJsonAsync(
            "/api/auth/password-reset/confirm",
            new { token = resetMessage.Token, password = "Reader456!" });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        using var oldAccessRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        oldAccessRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.SendAsync(oldAccessRequest)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync(
                "/api/auth/refresh",
                new { refreshToken })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email, password = "Reader123!" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email, password = "Reader456!" })).StatusCode);

        var reused = await client.PostAsJsonAsync(
            "/api/auth/password-reset/confirm",
            new { token = resetMessage.Token, password = "Reader789!" });
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal(
            "PASSWORD_RESET_TOKEN_INVALID",
            (await ReadEnvelopeAsync(reused)).GetProperty("code").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var user = db.UserSet.Single(item => item.Email == email);
        var storedToken = db.PasswordResetTokenSet.Single(item => item.UserId == user.Id);
        Assert.NotEqual(resetMessage.Token, storedToken.TokenHash);
        Assert.Equal(64, storedToken.TokenHash.Length);
        Assert.NotNull(storedToken.UsedAt);
        Assert.Equal(1, user.AuthVersion);
    }

    [Fact]
    public async Task Confirm_rejects_unknown_token_with_stable_error()
    {
        using var factory = CreateFactory(new RecordingPasswordResetEmailSender());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/password-reset/confirm",
            new { token = "unknown-reset-token", password = "Reader456!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.Equal("PASSWORD_RESET_TOKEN_INVALID", envelope.GetProperty("code").GetString());
        Assert.Equal(
            "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.",
            envelope.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Delivery_failure_keeps_generic_response_and_invalidates_generated_token()
    {
        var sender = new RecordingPasswordResetEmailSender(deliverSuccessfully: false);
        using var factory = CreateFactory(sender);
        using var client = factory.CreateClient();
        var email = $"delivery-failure-{Guid.NewGuid():N}@bookspace.local";
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password = "Reader123!",
                displayName = "Độc giả lỗi gửi mail"
            })).StatusCode);

        var response = await client.PostAsJsonAsync(
            "/api/auth/password-reset/request",
            new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadEnvelopeAsync(response)).GetProperty("data").ValueKind);
        Assert.Single(sender.Messages);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var user = db.UserSet.Single(item => item.Email == email);
        var storedToken = db.PasswordResetTokenSet.Single(item => item.UserId == user.Id);
        Assert.NotNull(storedToken.InvalidatedAt);
        Assert.False(storedToken.IsActiveAt(DateTimeOffset.UtcNow));
    }

    private static BookSpaceApiFactory CreateFactory(RecordingPasswordResetEmailSender sender) =>
        new(services =>
        {
            services.RemoveAll<IPasswordResetEmailSender>();
            services.AddSingleton<IPasswordResetEmailSender>(sender);
        });

    private static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed class RecordingPasswordResetEmailSender(bool deliverSuccessfully = true)
        : IPasswordResetEmailSender
    {
        public List<PasswordResetEmail> Messages { get; } = [];

        public Task<bool> SendAsync(
            PasswordResetEmail email,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(email);
            return Task.FromResult(deliverSuccessfully);
        }
    }
}
