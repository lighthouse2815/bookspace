using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Domain.Entities;
using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.IntegrationTests;

public sealed class ContentModerationFlowTests
{
    [Fact]
    public async Task Reader_can_report_once_and_admin_removal_soft_deletes_content_and_closes_the_queue()
    {
        using var factory = new BookSpaceApiFactory();
        using var reader = factory.CreateClient();
        using var admin = factory.CreateClient();
        await LoginAsync(reader, "reader@bookspace.local", "Reader123!");
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");

        Guid reviewId;
        Guid bookId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var target = new User(
                $"moderation-target-{Guid.NewGuid():N}@bookspace.local",
                "test-password-hash",
                "Tác giả cần kiểm duyệt");
            var book = await db.BookSet.FirstAsync();
            var review = new Review(
                target.Id,
                book.Id,
                1,
                "Nội dung đánh giá dùng để kiểm thử hàng đợi báo cáo và soft delete.",
                false);
            db.AddRange(target, review);
            await db.SaveChangesAsync();
            reviewId = review.Id;
            bookId = book.Id;
        }

        var reportResponse = await reader.PostAsJsonAsync(
            "/api/reports",
            new
            {
                targetType = "REVIEW",
                targetId = reviewId,
                reason = "HARASSMENT",
                details = "Nội dung công kích người đọc khác."
            });
        Assert.Equal(HttpStatusCode.Created, reportResponse.StatusCode);
        var created = await ReadDataAsync(reportResponse);
        var reportId = created.GetProperty("id").GetGuid();
        Assert.Equal("PENDING", created.GetProperty("status").GetString());
        Assert.Equal("Tác giả cần kiểm duyệt", created.GetProperty("targetOwner").GetProperty("displayName").GetString());
        Assert.False(created.GetProperty("targetOwner").TryGetProperty("email", out _));

        var duplicate = await reader.PostAsJsonAsync(
            "/api/reports",
            new
            {
                targetType = "REVIEW",
                targetId = reviewId,
                reason = "SPAM"
            });
        await AssertFailureAsync(
            duplicate,
            HttpStatusCode.Conflict,
            "CONTENT_REPORT_ALREADY_PENDING");

        var readerQueue = await reader.GetAsync("/api/admin/reports?status=PENDING");
        Assert.Equal(HttpStatusCode.Forbidden, readerQueue.StatusCode);
        var queue = await GetDataAsync(admin, "/api/admin/reports?status=PENDING&targetType=REVIEW");
        var queueItem = Assert.Single(queue.GetProperty("items").EnumerateArray());
        Assert.Equal(reportId, queueItem.GetProperty("id").GetGuid());

        var resolution = await admin.PatchAsJsonAsync(
            $"/api/admin/reports/{reportId}/resolution",
            new
            {
                status = "RESOLVED",
                action = "CONTENT_REMOVED",
                resolutionNote = "Đã xác minh nội dung vi phạm quy tắc cộng đồng."
            });
        Assert.Equal(HttpStatusCode.OK, resolution.StatusCode);
        var resolved = await ReadDataAsync(resolution);
        Assert.Equal("RESOLVED", resolved.GetProperty("status").GetString());
        Assert.Equal("CONTENT_REMOVED", resolved.GetProperty("action").GetString());
        Assert.Equal("Quản trị BookSpace", resolved.GetProperty("moderator").GetProperty("displayName").GetString());

        var reviews = await GetDataAsync(reader, $"/api/books/{bookId}/reviews?pageSize=100");
        Assert.DoesNotContain(
            reviews.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == reviewId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var stored = await db.ReviewSet.IgnoreQueryFilters().SingleAsync(x => x.Id == reviewId);
            Assert.NotNull(stored.DeletedAt);
        }
    }

    [Fact]
    public async Task Private_chat_reports_are_cloaked_and_locking_a_profile_revokes_existing_access()
    {
        using var factory = new BookSpaceApiFactory();
        using var owner = await RegisterAsync(factory, "safety-owner");
        using var outsider = await RegisterAsync(factory, "safety-outsider");
        using var reporter = await RegisterAsync(factory, "safety-reporter");
        using var target = await RegisterAsync(factory, "safety-target");
        using var admin = factory.CreateClient();
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");

        var clubResponse = await owner.Client.PostAsJsonAsync(
            "/api/clubs",
            new
            {
                name = $"CLB riêng tư {Guid.NewGuid():N}",
                description = "Kiểm thử quyền riêng tư của báo cáo chat.",
                coverImageUrl = (string?)null,
                isPrivate = true
            });
        Assert.Equal(HttpStatusCode.Created, clubResponse.StatusCode);
        var clubId = (await ReadDataAsync(clubResponse)).GetProperty("id").GetGuid();
        var messageResponse = await owner.Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/chat/messages",
            new { content = "Tin nhắn chỉ thành viên được nhìn thấy." });
        Assert.Equal(HttpStatusCode.Created, messageResponse.StatusCode);
        var messageId = (await ReadDataAsync(messageResponse)).GetProperty("id").GetGuid();

        var outsiderReport = await outsider.Client.PostAsJsonAsync(
            "/api/reports",
            new
            {
                targetType = "CLUB_CHAT_MESSAGE",
                targetId = messageId,
                reason = "INAPPROPRIATE_CONTENT"
            });
        await AssertFailureAsync(outsiderReport, HttpStatusCode.NotFound, "REPORT_TARGET_NOT_FOUND");

        var ownReport = await owner.Client.PostAsJsonAsync(
            "/api/reports",
            new
            {
                targetType = "CLUB_CHAT_MESSAGE",
                targetId = messageId,
                reason = "OTHER"
            });
        await AssertFailureAsync(ownReport, HttpStatusCode.BadRequest, "CANNOT_REPORT_OWN_CONTENT");

        var profileReport = await reporter.Client.PostAsJsonAsync(
            "/api/reports",
            new
            {
                targetType = "USER",
                targetId = target.Id,
                reason = "HARASSMENT",
                details = "Hồ sơ dùng để kiểm thử thao tác khóa tài khoản."
            });
        Assert.Equal(HttpStatusCode.Created, profileReport.StatusCode);
        var reportId = (await ReadDataAsync(profileReport)).GetProperty("id").GetGuid();

        var lockResponse = await admin.PatchAsJsonAsync(
            $"/api/admin/reports/{reportId}/resolution",
            new
            {
                status = "RESOLVED",
                action = "USER_LOCKED",
                resolutionNote = "Khóa sau khi xác minh."
            });
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);

        var oldTokenRequest = await target.Client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenRequest.StatusCode);
        var hiddenProfile = await reporter.Client.GetAsync($"/api/users/{target.Id}");
        await AssertFailureAsync(hiddenProfile, HttpStatusCode.NotFound, "USER_NOT_FOUND");
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await ReadDataAsync(response)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<RegisteredUser> RegisterAsync(BookSpaceApiFactory factory, string prefix)
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = $"{prefix}-{suffix}@bookspace.local",
                password = "Reader123!",
                displayName = $"{prefix} {suffix[..8]}"
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            data.GetProperty("accessToken").GetString());
        return new RegisteredUser(
            client,
            data.GetProperty("user").GetProperty("id").GetGuid());
    }

    private static async Task<JsonElement> GetDataAsync(HttpClient client, string endpoint)
    {
        var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task AssertFailureAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }

    private sealed record RegisteredUser(HttpClient Client, Guid Id) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
