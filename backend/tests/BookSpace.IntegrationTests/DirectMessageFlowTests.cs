using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Application.Contracts;
using BookSpace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.IntegrationTests;

public sealed class DirectMessageFlowTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    [Fact]
    public async Task Mutual_followers_can_start_one_conversation_page_messages_and_track_unread()
    {
        using var alice = await RegisterAsync("dm-alice");
        using var bob = await RegisterAsync("dm-bob");
        using var outsider = await RegisterAsync("dm-outsider");
        await FollowAsync(alice.Client, bob.Id);

        var oneWayStart = await alice.Client.PostAsJsonAsync(
            "/api/conversations",
            new { targetUserId = bob.Id });
        await AssertFailureAsync(
            oneWayStart,
            HttpStatusCode.Forbidden,
            "DIRECT_MESSAGE_MUTUAL_FOLLOW_REQUIRED");

        await FollowAsync(bob.Client, alice.Id);
        var starts = await Task.WhenAll(
            alice.Client.PostAsJsonAsync("/api/conversations", new { targetUserId = bob.Id }),
            bob.Client.PostAsJsonAsync("/api/conversations", new { targetUserId = alice.Id }));
        Assert.All(starts, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var firstConversation = await ReadDataAsync(starts[0]);
        var secondConversation = await ReadDataAsync(starts[1]);
        var conversationId = firstConversation.GetProperty("id").GetGuid();
        Assert.Equal(conversationId, secondConversation.GetProperty("id").GetGuid());
        Assert.True(firstConversation.GetProperty("canSend").GetBoolean());

        var outsiderDetail = await outsider.Client.GetAsync($"/api/conversations/{conversationId}");
        await AssertFailureAsync(outsiderDetail, HttpStatusCode.NotFound, "CONVERSATION_NOT_FOUND");

        var first = await SendMessageAsync(alice.Client, conversationId, "Tin nhắn thứ nhất");
        var second = await SendMessageAsync(alice.Client, conversationId, "Tin nhắn thứ hai");
        var third = await SendMessageAsync(alice.Client, conversationId, "Tin nhắn thứ ba");

        var firstPage = await GetDataAsync(
            bob.Client,
            $"/api/conversations/{conversationId}/messages?pageSize=2");
        Assert.True(firstPage.GetProperty("hasMore").GetBoolean());
        var firstPageItems = firstPage.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, firstPageItems.Count);
        Assert.Equal(third.GetProperty("id").GetGuid(), firstPageItems[0].GetProperty("id").GetGuid());
        Assert.Equal(second.GetProperty("id").GetGuid(), firstPageItems[1].GetProperty("id").GetGuid());
        var cursor = firstPage.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));

        var secondPage = await GetDataAsync(
            bob.Client,
            $"/api/conversations/{conversationId}/messages?pageSize=2&cursor={Uri.EscapeDataString(cursor!)}");
        Assert.False(secondPage.GetProperty("hasMore").GetBoolean());
        Assert.Equal(
            first.GetProperty("id").GetGuid(),
            secondPage.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid());

        var invalidCursor = await bob.Client.GetAsync(
            $"/api/conversations/{conversationId}/messages?cursor=khong-hop-le");
        await AssertFailureAsync(
            invalidCursor,
            HttpStatusCode.BadRequest,
            "INVALID_DIRECT_MESSAGE_CURSOR");

        var inbox = await GetDataAsync(bob.Client, "/api/conversations");
        var inboxItem = Assert.Single(inbox.GetProperty("items").EnumerateArray());
        Assert.Equal(3, inboxItem.GetProperty("unreadCount").GetInt32());
        Assert.Equal(
            third.GetProperty("id").GetGuid(),
            inboxItem.GetProperty("lastMessage").GetProperty("id").GetGuid());
        var totalUnread = await GetDataAsync(bob.Client, "/api/conversations/unread-count");
        Assert.Equal(3, totalUnread.GetProperty("count").GetInt32());
        Assert.Equal(3, await CountMessageNotificationsAsync(bob.Client, conversationId));

        var markRead = await bob.Client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/read",
            new { lastReadMessageId = third.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.OK, markRead.StatusCode);
        Assert.Equal(0, (await ReadDataAsync(markRead)).GetProperty("count").GetInt32());

        var preference = await bob.Client.PatchAsJsonAsync(
            "/api/notifications/preferences",
            new
            {
                isFollowNotificationEnabled = true,
                isReviewNotificationEnabled = true,
                isClubNotificationEnabled = true,
                isChallengeNotificationEnabled = true,
                isDirectMessageNotificationEnabled = false
            });
        Assert.Equal(HttpStatusCode.OK, preference.StatusCode);
        _ = await SendMessageAsync(alice.Client, conversationId, "Vẫn tăng unread khi tắt thông báo");
        Assert.Equal(
            1,
            (await GetDataAsync(bob.Client, "/api/conversations/unread-count"))
            .GetProperty("count")
            .GetInt32());
        Assert.Equal(3, await CountMessageNotificationsAsync(bob.Client, conversationId));

        await UnfollowAsync(bob.Client, alice.Id);
        var cannotSend = await alice.Client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "Không còn mutual" });
        await AssertFailureAsync(
            cannotSend,
            HttpStatusCode.Forbidden,
            "DIRECT_MESSAGE_MUTUAL_FOLLOW_REQUIRED");
        var afterUnfollow = await GetDataAsync(alice.Client, $"/api/conversations/{conversationId}");
        Assert.False(afterUnfollow.GetProperty("canSend").GetBoolean());

        var blank = await alice.Client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = "   " });
        await AssertFailureAsync(blank, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        var tooLong = await alice.Client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content = new string('a', 2001) });
        await AssertFailureAsync(tooLong, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Mute_filters_private_messages_while_block_cloaks_the_conversation()
    {
        using var sender = await RegisterAsync("dm-safety-sender");
        using var recipient = await RegisterAsync("dm-safety-recipient");
        await MakeMutualAsync(sender, recipient);
        var conversationId = await StartConversationAsync(sender.Client, recipient.Id);
        _ = await SendMessageAsync(sender.Client, conversationId, "Tin trước khi ẩn");

        var mute = await recipient.Client.PostAsync($"/api/users/{sender.Id}/mute", null);
        Assert.Equal(HttpStatusCode.OK, mute.StatusCode);
        _ = await SendMessageAsync(sender.Client, conversationId, "Tin sau khi ẩn");

        var hiddenHistory = await GetDataAsync(
            recipient.Client,
            $"/api/conversations/{conversationId}/messages");
        Assert.Empty(hiddenHistory.GetProperty("items").EnumerateArray());
        Assert.Equal(
            0,
            (await GetDataAsync(recipient.Client, "/api/conversations/unread-count"))
            .GetProperty("count")
            .GetInt32());
        Assert.Equal(1, await CountMessageNotificationsAsync(recipient.Client, conversationId));

        var unmute = await recipient.Client.DeleteAsync($"/api/users/{sender.Id}/mute");
        Assert.Equal(HttpStatusCode.OK, unmute.StatusCode);
        var visibleHistory = await GetDataAsync(
            recipient.Client,
            $"/api/conversations/{conversationId}/messages");
        Assert.Equal(2, visibleHistory.GetProperty("items").GetArrayLength());
        Assert.Equal(
            2,
            (await GetDataAsync(recipient.Client, "/api/conversations/unread-count"))
            .GetProperty("count")
            .GetInt32());

        var block = await recipient.Client.PostAsync($"/api/users/{sender.Id}/block", null);
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);
        var recipientDetail = await recipient.Client.GetAsync($"/api/conversations/{conversationId}");
        await AssertFailureAsync(recipientDetail, HttpStatusCode.NotFound, "CONVERSATION_NOT_FOUND");
        var senderDetail = await sender.Client.GetAsync($"/api/conversations/{conversationId}");
        await AssertFailureAsync(senderDetail, HttpStatusCode.NotFound, "CONVERSATION_NOT_FOUND");
        var recipientInbox = await GetDataAsync(recipient.Client, "/api/conversations");
        Assert.Empty(recipientInbox.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Hub_is_authenticated_and_moderation_can_soft_delete_a_persisted_direct_message()
    {
        using var sender = await RegisterAsync("dm-hub-sender");
        using var recipient = await RegisterAsync("dm-hub-recipient");
        await MakeMutualAsync(sender, recipient);
        var conversationId = await StartConversationAsync(sender.Client, recipient.Id);

        using var anonymous = factory.CreateClient();
        var anonymousNegotiate = await anonymous.PostAsync(
            "/hubs/direct-messages/negotiate?negotiateVersion=1",
            null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousNegotiate.StatusCode);
        var queryTokenNegotiate = await anonymous.PostAsync(
            $"/hubs/direct-messages/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(recipient.Token)}",
            null);
        Assert.Equal(HttpStatusCode.OK, queryTokenNegotiate.StatusCode);

        await using var connection = CreateHubConnection(recipient.Token);
        var received = new TaskCompletionSource<DirectMessageDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = connection.On<DirectMessageDto>(
            "DirectMessageCreated",
            message => received.TrySetResult(message));
        await connection.StartAsync();

        var sent = await SendMessageAsync(sender.Client, conversationId, "Tin realtime cần kiểm duyệt");
        var delivered = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(sent.GetProperty("id").GetGuid(), delivered.Id);
        var persisted = await GetDataAsync(
            recipient.Client,
            $"/api/conversations/{conversationId}/messages");
        Assert.Contains(
            persisted.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == delivered.Id);

        var report = await recipient.Client.PostAsJsonAsync(
            "/api/reports",
            new
            {
                targetType = "DIRECT_MESSAGE",
                targetId = delivered.Id,
                reason = "HARASSMENT",
                details = "Tin nhắn riêng vi phạm quy tắc cộng đồng."
            });
        Assert.Equal(HttpStatusCode.Created, report.StatusCode);
        var reportId = (await ReadDataAsync(report)).GetProperty("id").GetGuid();

        using var admin = factory.CreateClient();
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");
        var resolution = await admin.PatchAsJsonAsync(
            $"/api/admin/reports/{reportId}/resolution",
            new
            {
                status = "RESOLVED",
                action = "CONTENT_REMOVED",
                resolutionNote = "Đã xác minh vi phạm trong tin nhắn riêng."
            });
        Assert.Equal(HttpStatusCode.OK, resolution.StatusCode);

        var afterModeration = await GetDataAsync(
            recipient.Client,
            $"/api/conversations/{conversationId}/messages");
        Assert.DoesNotContain(
            afterModeration.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == delivered.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var stored = await db.DirectMessageSet
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == delivered.Id);
        Assert.True(stored.IsDeleted);
    }

    private HubConnection CreateHubConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/direct-messages",
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                })
            .Build();

    private async Task<RegisteredUser> RegisterAsync(string prefix)
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
        var token = data.GetProperty("accessToken").GetString()
                    ?? throw new InvalidOperationException("Register không trả access token.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new RegisteredUser(
            client,
            data.GetProperty("user").GetProperty("id").GetGuid(),
            token);
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await ReadDataAsync(response)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task FollowAsync(HttpClient client, Guid targetUserId)
    {
        var response = await client.PostAsync($"/api/users/{targetUserId}/follow", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task UnfollowAsync(HttpClient client, Guid targetUserId)
    {
        var response = await client.DeleteAsync($"/api/users/{targetUserId}/follow");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task MakeMutualAsync(RegisteredUser first, RegisteredUser second)
    {
        await FollowAsync(first.Client, second.Id);
        await FollowAsync(second.Client, first.Id);
    }

    private static async Task<Guid> StartConversationAsync(HttpClient client, Guid targetUserId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/conversations",
            new { targetUserId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadDataAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> SendMessageAsync(
        HttpClient client,
        Guid conversationId,
        string content)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/conversations/{conversationId}/messages",
            new { content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task<int> CountMessageNotificationsAsync(
        HttpClient client,
        Guid conversationId)
    {
        var data = await GetDataAsync(
            client,
            "/api/notifications?category=DIRECT_MESSAGE&pageSize=100");
        var link = $"/messages/{conversationId}";
        return data
            .GetProperty("items")
            .EnumerateArray()
            .Count(item => item.GetProperty("link").GetString() == link);
    }

    private static async Task<JsonElement> GetDataAsync(HttpClient client, string endpoint)
    {
        var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task AssertFailureAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private sealed record RegisteredUser(
        HttpClient Client,
        Guid Id,
        string Token) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
