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

public sealed class ClubChatFlowTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    [Fact]
    public async Task Members_can_send_page_and_mark_messages_while_unread_and_preferences_stay_independent()
    {
        using var owner = await RegisterAsync("chat-owner");
        using var member = await RegisterAsync("chat-member");
        using var outsider = await RegisterAsync("chat-outsider");
        var clubId = await CreateClubAsync(owner.Client, isPrivate: false);
        await JoinClubAsync(member.Client, clubId);

        var outsiderHistory = await outsider.Client.GetAsync($"/api/clubs/{clubId}/chat/messages");
        await AssertFailureAsync(
            outsiderHistory,
            HttpStatusCode.Forbidden,
            "CLUB_CHAT_MEMBERSHIP_REQUIRED");

        var first = await SendMessageAsync(owner.Client, clubId, "Tin nhắn đầu tiên");
        var second = await SendMessageAsync(owner.Client, clubId, "Tin nhắn thứ hai");
        var third = await SendMessageAsync(owner.Client, clubId, "Tin nhắn thứ ba");

        var firstPage = await GetDataAsync(
            member.Client,
            $"/api/clubs/{clubId}/chat/messages?pageSize=2");
        Assert.True(firstPage.GetProperty("hasMore").GetBoolean());
        var firstPageItems = firstPage.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, firstPageItems.Count);
        Assert.Equal(third.GetProperty("id").GetGuid(), firstPageItems[0].GetProperty("id").GetGuid());
        Assert.Equal(second.GetProperty("id").GetGuid(), firstPageItems[1].GetProperty("id").GetGuid());
        var cursor = firstPage.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));

        var secondPage = await GetDataAsync(
            member.Client,
            $"/api/clubs/{clubId}/chat/messages?pageSize=2&cursor={Uri.EscapeDataString(cursor!)}");
        Assert.False(secondPage.GetProperty("hasMore").GetBoolean());
        Assert.Equal(
            first.GetProperty("id").GetGuid(),
            secondPage.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, secondPage.GetProperty("nextCursor").ValueKind);

        var invalidCursor = await member.Client.GetAsync(
            $"/api/clubs/{clubId}/chat/messages?cursor=khong-hop-le");
        await AssertFailureAsync(
            invalidCursor,
            HttpStatusCode.BadRequest,
            "INVALID_CHAT_CURSOR");

        var memberUnread = await GetDataAsync(
            member.Client,
            $"/api/clubs/{clubId}/chat/unread-count");
        Assert.Equal(3, memberUnread.GetProperty("count").GetInt32());
        var ownerUnread = await GetDataAsync(
            owner.Client,
            $"/api/clubs/{clubId}/chat/unread-count");
        Assert.Equal(0, ownerUnread.GetProperty("count").GetInt32());
        Assert.Equal(3, await CountChatNotificationsAsync(member.Client, clubId));

        var markRead = await member.Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/chat/read",
            new { lastReadMessageId = third.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.OK, markRead.StatusCode);
        var readState = await ReadDataAsync(markRead);
        Assert.Equal(0, readState.GetProperty("count").GetInt32());
        Assert.Equal(
            third.GetProperty("id").GetGuid(),
            readState.GetProperty("lastReadMessageId").GetGuid());

        var preferenceResponse = await member.Client.PatchAsJsonAsync(
            "/api/notifications/preferences",
            new
            {
                isFollowNotificationEnabled = true,
                isReviewNotificationEnabled = true,
                isClubNotificationEnabled = false,
                isChallengeNotificationEnabled = true,
                isDirectMessageNotificationEnabled = true
            });
        Assert.Equal(HttpStatusCode.OK, preferenceResponse.StatusCode);
        _ = await SendMessageAsync(owner.Client, clubId, "Vẫn tăng unread khi tắt notification");

        var unreadAfterPreference = await GetDataAsync(
            member.Client,
            $"/api/clubs/{clubId}/chat/unread-count");
        Assert.Equal(1, unreadAfterPreference.GetProperty("count").GetInt32());
        Assert.Equal(3, await CountChatNotificationsAsync(member.Client, clubId));
    }

    [Fact]
    public async Task Private_chat_is_cloaked_and_all_chat_routes_require_an_active_membership()
    {
        using var owner = await RegisterAsync("chat-private-owner");
        using var outsider = await RegisterAsync("chat-private-outsider");
        using var anonymous = factory.CreateClient();
        var clubId = await CreateClubAsync(owner.Client, isPrivate: true);

        var anonymousHistory = await anonymous.GetAsync($"/api/clubs/{clubId}/chat/messages");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousHistory.StatusCode);

        var outsiderHistory = await outsider.Client.GetAsync($"/api/clubs/{clubId}/chat/messages");
        await AssertFailureAsync(outsiderHistory, HttpStatusCode.NotFound, "CLUB_NOT_FOUND");
        var outsiderSend = await outsider.Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/chat/messages",
            new { content = "Không được gửi" });
        await AssertFailureAsync(outsiderSend, HttpStatusCode.NotFound, "CLUB_NOT_FOUND");
        var outsiderUnread = await outsider.Client.GetAsync(
            $"/api/clubs/{clubId}/chat/unread-count");
        await AssertFailureAsync(outsiderUnread, HttpStatusCode.NotFound, "CLUB_NOT_FOUND");
        var outsiderRead = await outsider.Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/chat/read",
            new { lastReadMessageId = Guid.NewGuid() });
        await AssertFailureAsync(outsiderRead, HttpStatusCode.NotFound, "CLUB_NOT_FOUND");

        var blankMessage = await owner.Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/chat/messages",
            new { content = "   " });
        await AssertFailureAsync(blankMessage, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        var longMessage = await owner.Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/chat/messages",
            new { content = new string('a', 2001) });
        await AssertFailureAsync(longMessage, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Concurrent_mark_read_creates_one_state_and_keeps_the_newest_cursor()
    {
        using var owner = await RegisterAsync("chat-read-owner");
        using var member = await RegisterAsync("chat-read-member");
        var clubId = await CreateClubAsync(owner.Client, isPrivate: false);
        await JoinClubAsync(member.Client, clubId);
        _ = await SendMessageAsync(owner.Client, clubId, "Tin cũ");
        _ = await SendMessageAsync(owner.Client, clubId, "Tin mới");
        var history = await GetDataAsync(
            member.Client,
            $"/api/clubs/{clubId}/chat/messages?pageSize=10");
        var orderedIds = history
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();
        Assert.Equal(2, orderedIds.Count);
        var newestId = orderedIds[0];
        var olderId = orderedIds[1];

        var markOlder = member.Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/chat/read",
            new { lastReadMessageId = olderId });
        var markNewest = member.Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/chat/read",
            new { lastReadMessageId = newestId });
        var responses = await Task.WhenAll(markOlder, markNewest);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var finalState = await GetDataAsync(
            member.Client,
            $"/api/clubs/{clubId}/chat/unread-count");
        Assert.Equal(0, finalState.GetProperty("count").GetInt32());
        Assert.Equal(newestId, finalState.GetProperty("lastReadMessageId").GetGuid());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var membershipId = await db.BookClubMemberSet
            .Where(x => x.ClubId == clubId && x.UserId == member.Id)
            .Select(x => x.Id)
            .SingleAsync();
        var storedStates = await db.ClubChatReadStateSet
            .Where(x => x.MembershipId == membershipId)
            .ToListAsync();
        var storedState = Assert.Single(storedStates);
        Assert.Equal(newestId, storedState.LastReadMessageId);
    }

    [Fact]
    public async Task Hub_rejects_anonymous_and_delivers_only_to_current_members_after_persistence()
    {
        using var owner = await RegisterAsync("chat-hub-owner");
        using var member = await RegisterAsync("chat-hub-member");
        using var outsider = await RegisterAsync("chat-hub-outsider");
        var clubId = await CreateClubAsync(owner.Client, isPrivate: false);
        await JoinClubAsync(member.Client, clubId);

        using var anonymous = factory.CreateClient();
        var anonymousNegotiate = await anonymous.PostAsync(
            "/hubs/club-chat/negotiate?negotiateVersion=1",
            null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousNegotiate.StatusCode);
        var queryTokenNegotiate = await anonymous.PostAsync(
            $"/hubs/club-chat/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(member.Token)}",
            null);
        Assert.Equal(HttpStatusCode.OK, queryTokenNegotiate.StatusCode);
        var queryTokenOutsideHub = await anonymous.GetAsync(
            $"/api/notifications?access_token={Uri.EscapeDataString(member.Token)}");
        Assert.Equal(HttpStatusCode.Unauthorized, queryTokenOutsideHub.StatusCode);

        await using var memberConnection = CreateHubConnection(member.Token);
        await using var outsiderConnection = CreateHubConnection(outsider.Token);
        var memberReceived = NewMessageSource();
        var outsiderReceived = NewMessageSource();
        using var memberSubscription = memberConnection.On<ClubChatMessageDto>(
            "ClubChatMessageCreated",
            message => memberReceived.TrySetResult(message));
        using var outsiderSubscription = outsiderConnection.On<ClubChatMessageDto>(
            "ClubChatMessageCreated",
            message => outsiderReceived.TrySetResult(message));
        await Task.WhenAll(memberConnection.StartAsync(), outsiderConnection.StartAsync());

        var sent = await SendMessageAsync(owner.Client, clubId, "Tin realtime đã lưu");
        var delivered = await memberReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(sent.GetProperty("id").GetGuid(), delivered.Id);
        await AssertNoMessageAsync(outsiderReceived.Task);
        var persistedHistory = await GetDataAsync(
            owner.Client,
            $"/api/clubs/{clubId}/chat/messages?pageSize=10");
        Assert.Contains(
            persistedHistory.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == delivered.Id);

        memberSubscription.Dispose();
        var formerMemberReceived = NewMessageSource();
        using var formerMemberSubscription = memberConnection.On<ClubChatMessageDto>(
            "ClubChatMessageCreated",
            message => formerMemberReceived.TrySetResult(message));
        var leaveResponse = await member.Client.DeleteAsync($"/api/clubs/{clubId}/join");
        Assert.Equal(HttpStatusCode.OK, leaveResponse.StatusCode);
        _ = await SendMessageAsync(owner.Client, clubId, "Tin sau khi thành viên rời club");
        await AssertNoMessageAsync(formerMemberReceived.Task);

        var formerMemberHistory = await member.Client.GetAsync(
            $"/api/clubs/{clubId}/chat/messages");
        await AssertFailureAsync(
            formerMemberHistory,
            HttpStatusCode.Forbidden,
            "CLUB_CHAT_MEMBERSHIP_REQUIRED");
    }

    private HubConnection CreateHubConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/club-chat",
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                })
            .Build();

    private static TaskCompletionSource<ClubChatMessageDto> NewMessageSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task AssertNoMessageAsync(Task<ClubChatMessageDto> messageTask)
    {
        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await messageTask.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    private async Task<RegisteredUser> RegisterAsync(string prefix)
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"{prefix}-{suffix}@bookspace.local";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
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

    private static async Task<Guid> CreateClubAsync(HttpClient owner, bool isPrivate)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var response = await owner.PostAsJsonAsync(
            "/api/clubs",
            new
            {
                name = $"CLB Chat {suffix[..10]}",
                description = "Câu lạc bộ kiểm thử phòng trò chuyện thời gian thực.",
                coverImageUrl = (string?)null,
                isPrivate
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadDataAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task JoinClubAsync(HttpClient member, Guid clubId)
    {
        var response = await member.PostAsync($"/api/clubs/{clubId}/join", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<JsonElement> SendMessageAsync(
        HttpClient sender,
        Guid clubId,
        string content)
    {
        var response = await sender.PostAsJsonAsync(
            $"/api/clubs/{clubId}/chat/messages",
            new { content });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task<int> CountChatNotificationsAsync(HttpClient client, Guid clubId)
    {
        var data = await GetDataAsync(client, "/api/notifications?category=CLUB&pageSize=100");
        var link = $"/clubs/{clubId}?tab=chat";
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
