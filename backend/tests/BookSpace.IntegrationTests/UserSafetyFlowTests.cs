using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.IntegrationTests;

public sealed class UserSafetyFlowTests
{
    [Fact]
    public async Task Block_is_two_way_revokes_follows_and_can_be_managed_from_safety_list()
    {
        using var factory = new BookSpaceApiFactory();
        using var first = await RegisterAsync(factory, "block-first");
        using var second = await RegisterAsync(factory, "block-second");

        Assert.Equal(
            HttpStatusCode.OK,
            (await first.Client.PostAsync($"/api/users/{second.Id}/follow", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await second.Client.PostAsync($"/api/users/{first.Id}/follow", null)).StatusCode);

        var selfBlock = await first.Client.PostAsync($"/api/users/{first.Id}/block", null);
        await AssertFailureAsync(selfBlock, HttpStatusCode.BadRequest, "CANNOT_BLOCK_SELF");

        var blockResponse = await first.Client.PostAsync($"/api/users/{second.Id}/block", null);
        Assert.Equal(HttpStatusCode.OK, blockResponse.StatusCode);
        var block = await ReadDataAsync(blockResponse);
        Assert.True(block.GetProperty("isBlocked").GetBoolean());
        Assert.False(block.GetProperty("isMuted").GetBoolean());
        Assert.Equal(second.Id, block.GetProperty("user").GetProperty("id").GetGuid());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            Assert.False(await db.FollowSet.AnyAsync(x =>
                x.FollowerId == first.Id && x.FollowingId == second.Id ||
                x.FollowerId == second.Id && x.FollowingId == first.Id));
            Assert.True(await db.UserBlockSet.AnyAsync(x =>
                x.BlockerId == first.Id && x.BlockedUserId == second.Id));
        }

        var mine = await GetDataAsync(first.Client, "/api/users/me/safety?pageSize=100");
        var entry = Assert.Single(mine.GetProperty("items").EnumerateArray());
        Assert.Equal(second.Id, entry.GetProperty("user").GetProperty("id").GetGuid());
        Assert.True(entry.GetProperty("isBlocked").GetBoolean());

        await AssertFailureAsync(
            await first.Client.GetAsync($"/api/users/{second.Id}"),
            HttpStatusCode.NotFound,
            "USER_NOT_FOUND");
        await AssertFailureAsync(
            await second.Client.GetAsync($"/api/users/{first.Id}"),
            HttpStatusCode.NotFound,
            "USER_NOT_FOUND");
        await AssertFailureAsync(
            await first.Client.PostAsync($"/api/users/{second.Id}/follow", null),
            HttpStatusCode.Forbidden,
            "USER_RELATION_BLOCKED");

        var search = await GetDataAsync(
            first.Client,
            $"/api/users?search={Uri.EscapeDataString(second.DisplayName)}&pageSize=100");
        Assert.Empty(search.GetProperty("items").EnumerateArray());

        var unblock = await first.Client.DeleteAsync($"/api/users/{second.Id}/block");
        Assert.Equal(HttpStatusCode.OK, unblock.StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await first.Client.GetAsync($"/api/users/{second.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await second.Client.GetAsync($"/api/users/{first.Id}")).StatusCode);
        Assert.Empty(
            (await GetDataAsync(first.Client, "/api/users/me/safety?pageSize=100"))
            .GetProperty("items")
            .EnumerateArray());
    }

    [Fact]
    public async Task Mute_hides_aggregate_content_chat_and_new_notifications_without_hiding_profile()
    {
        using var factory = new BookSpaceApiFactory();
        using var viewer = await RegisterAsync(factory, "mute-viewer");
        using var actor = await RegisterAsync(factory, "mute-actor");
        Assert.Equal(
            HttpStatusCode.OK,
            (await viewer.Client.PostAsync($"/api/users/{actor.Id}/follow", null)).StatusCode);

        Guid bookId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            bookId = await db.BookSet.Select(x => x.Id).FirstAsync();
        }

        var reviewResponse = await actor.Client.PostAsJsonAsync(
            "/api/reviews",
            new
            {
                bookId,
                rating = 4,
                content = "Đánh giá dùng để kiểm thử chức năng ẩn nội dung.",
                containsSpoilers = false
            });
        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);
        var reviewId = (await ReadDataAsync(reviewResponse)).GetProperty("id").GetGuid();

        var clubResponse = await viewer.Client.PostAsJsonAsync(
            "/api/clubs",
            new
            {
                name = $"CLB mute {Guid.NewGuid():N}",
                description = "Kiểm thử ẩn hội thoại và thông báo.",
                coverImageUrl = (string?)null,
                isPrivate = false
            });
        Assert.Equal(HttpStatusCode.Created, clubResponse.StatusCode);
        var clubId = (await ReadDataAsync(clubResponse)).GetProperty("id").GetGuid();
        Assert.Equal(
            HttpStatusCode.OK,
            (await actor.Client.PostAsync($"/api/clubs/{clubId}/join", null)).StatusCode);
        _ = await SendMessageAsync(actor.Client, clubId, "Tin nhắn trước khi bị ẩn.");

        var feedBefore = await GetDataAsync(viewer.Client, "/api/feed?type=REVIEW&pageSize=100");
        Assert.Contains(
            feedBefore.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == reviewId);

        var muteResponse = await viewer.Client.PostAsync($"/api/users/{actor.Id}/mute", null);
        Assert.Equal(HttpStatusCode.OK, muteResponse.StatusCode);
        Assert.True((await ReadDataAsync(muteResponse)).GetProperty("isMuted").GetBoolean());

        var profile = await GetDataAsync(viewer.Client, $"/api/users/{actor.Id}");
        Assert.True(profile.GetProperty("isMuted").GetBoolean());
        Assert.Equal(
            HttpStatusCode.OK,
            (await actor.Client.GetAsync($"/api/users/{viewer.Id}")).StatusCode);

        var feedAfter = await GetDataAsync(viewer.Client, "/api/feed?type=REVIEW&pageSize=100");
        Assert.DoesNotContain(
            feedAfter.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == reviewId);
        var bookReviews = await GetDataAsync(
            viewer.Client,
            $"/api/books/{bookId}/reviews?pageSize=100");
        Assert.DoesNotContain(
            bookReviews.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == reviewId);
        var chatAfter = await GetDataAsync(
            viewer.Client,
            $"/api/clubs/{clubId}/chat/messages?pageSize=100");
        Assert.Empty(chatAfter.GetProperty("items").EnumerateArray());
        var unreadAfter = await GetDataAsync(
            viewer.Client,
            $"/api/clubs/{clubId}/chat/unread-count");
        Assert.Equal(0, unreadAfter.GetProperty("count").GetInt32());

        var chatNotificationCount = await CountChatNotificationsAsync(viewer.Client, clubId);
        _ = await SendMessageAsync(actor.Client, clubId, "Tin nhắn sau khi bị ẩn.");
        Assert.Equal(
            chatNotificationCount,
            await CountChatNotificationsAsync(viewer.Client, clubId));

        var safety = await GetDataAsync(viewer.Client, "/api/users/me/safety?pageSize=100");
        var muted = Assert.Single(safety.GetProperty("items").EnumerateArray());
        Assert.True(muted.GetProperty("isMuted").GetBoolean());
        Assert.False(muted.GetProperty("isBlocked").GetBoolean());

        Assert.Equal(
            HttpStatusCode.OK,
            (await viewer.Client.DeleteAsync($"/api/users/{actor.Id}/mute")).StatusCode);
        var visibleProfile = await GetDataAsync(viewer.Client, $"/api/users/{actor.Id}");
        Assert.False(visibleProfile.GetProperty("isMuted").GetBoolean());
        var restoredChat = await GetDataAsync(
            viewer.Client,
            $"/api/clubs/{clubId}/chat/messages?pageSize=100");
        Assert.Equal(2, restoredChat.GetProperty("items").GetArrayLength());
    }

    private static async Task<RegisteredUser> RegisterAsync(BookSpaceApiFactory factory, string prefix)
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var displayName = $"{prefix} {suffix[..8]}";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = $"{prefix}-{suffix}@bookspace.local",
                password = "Reader123!",
                displayName
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            data.GetProperty("accessToken").GetString());
        return new RegisteredUser(
            client,
            data.GetProperty("user").GetProperty("id").GetGuid(),
            displayName);
    }

    private static async Task<JsonElement> SendMessageAsync(
        HttpClient client,
        Guid clubId,
        string content)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/chat/messages",
            new { content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task<int> CountChatNotificationsAsync(HttpClient client, Guid clubId)
    {
        var data = await GetDataAsync(client, "/api/notifications?category=CLUB&pageSize=100");
        var link = $"/clubs/{clubId}?tab=chat";
        return data.GetProperty("items")
            .EnumerateArray()
            .Count(item => item.GetProperty("link").GetString() == link);
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

    private sealed record RegisteredUser(
        HttpClient Client,
        Guid Id,
        string DisplayName) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
