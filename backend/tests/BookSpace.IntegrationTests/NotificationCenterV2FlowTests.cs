using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class NotificationCenterV2FlowTests
{
    [Fact]
    public async Task Follow_preference_controls_future_delivery_and_server_unread_count()
    {
        using var factory = new BookSpaceApiFactory();
        using var target = factory.CreateClient();
        using var firstFollower = factory.CreateClient();
        using var secondFollower = factory.CreateClient();

        var targetId = await RegisterAsync(target, "notification-target@bookspace.local", "Người nhận");
        await RegisterAsync(firstFollower, "notification-follower-1@bookspace.local", "Độc giả Một");
        await RegisterAsync(secondFollower, "notification-follower-2@bookspace.local", "Độc giả Hai");

        var defaults = await ReadDataAsync(await target.GetAsync("/api/notifications/preferences"));
        Assert.True(defaults.GetProperty("isFollowNotificationEnabled").GetBoolean());
        Assert.True(defaults.GetProperty("isReviewNotificationEnabled").GetBoolean());
        Assert.True(defaults.GetProperty("isClubNotificationEnabled").GetBoolean());
        Assert.True(defaults.GetProperty("isChallengeNotificationEnabled").GetBoolean());
        Assert.True(defaults.GetProperty("isDirectMessageNotificationEnabled").GetBoolean());

        var disabled = await target.PatchAsJsonAsync(
            "/api/notifications/preferences",
            new
            {
                isFollowNotificationEnabled = false,
                isCatalogNotificationEnabled = true,
                isReviewNotificationEnabled = true,
                isClubNotificationEnabled = true,
                isChallengeNotificationEnabled = true,
                isDirectMessageNotificationEnabled = true
            });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.False((await ReadDataAsync(disabled))
            .GetProperty("isFollowNotificationEnabled")
            .GetBoolean());

        Assert.Equal(
            HttpStatusCode.OK,
            (await firstFollower.PostAsync($"/api/users/{targetId}/follow", null)).StatusCode);
        var noFollowNotifications = await ReadDataAsync(
            await target.GetAsync("/api/notifications?category=FOLLOW&page=1&pageSize=10"));
        Assert.Empty(noFollowNotifications.GetProperty("items").EnumerateArray());
        var invalidCategory = await target.GetAsync("/api/notifications?category=999");
        Assert.Equal(HttpStatusCode.BadRequest, invalidCategory.StatusCode);
        using (var invalidDocument = JsonDocument.Parse(
                   await invalidCategory.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                "INVALID_NOTIFICATION_CATEGORY",
                invalidDocument.RootElement.GetProperty("code").GetString());
        }

        var invalidUnreadCategory = await target.GetAsync(
            "/api/notifications/unread-count?category=UNKNOWN");
        Assert.Equal(HttpStatusCode.BadRequest, invalidUnreadCategory.StatusCode);
        using (var invalidUnreadDocument = JsonDocument.Parse(
                   await invalidUnreadCategory.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                "INVALID_NOTIFICATION_CATEGORY",
                invalidUnreadDocument.RootElement.GetProperty("code").GetString());
        }

        var enabled = await target.PatchAsJsonAsync(
            "/api/notifications/preferences",
            new
            {
                isFollowNotificationEnabled = true,
                isCatalogNotificationEnabled = true,
                isReviewNotificationEnabled = true,
                isClubNotificationEnabled = true,
                isChallengeNotificationEnabled = true,
                isDirectMessageNotificationEnabled = true
            });
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await secondFollower.PostAsync($"/api/users/{targetId}/follow", null)).StatusCode);

        var followPage = await ReadDataAsync(
            await target.GetAsync("/api/notifications?category=FOLLOW&page=1&pageSize=10"));
        var notification = Assert.Single(followPage.GetProperty("items").EnumerateArray());
        Assert.Equal("FOLLOW", notification.GetProperty("type").GetString());
        var notificationId = notification.GetProperty("id").GetGuid();

        var unread = await ReadDataAsync(
            await target.GetAsync("/api/notifications/unread-count?category=FOLLOW"));
        Assert.Equal(1, unread.GetProperty("count").GetInt32());

        var marked = await target.PatchAsync(
            $"/api/notifications/{notificationId}/read",
            null);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.True((await ReadDataAsync(marked)).GetProperty("isRead").GetBoolean());
        var afterRead = await ReadDataAsync(
            await target.GetAsync("/api/notifications/unread-count?category=FOLLOW"));
        Assert.Equal(0, afterRead.GetProperty("count").GetInt32());
    }

    private static async Task<Guid> RegisterAsync(
        HttpClient client,
        string email,
        string displayName)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, password = "Reader123!", displayName });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            data.GetProperty("accessToken").GetString());
        return data.GetProperty("user").GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
