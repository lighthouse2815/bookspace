using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class CatalogFollowingFlowTests
{
    [Fact]
    public async Task Reader_can_follow_catalog_metadata_and_receive_one_alert_per_new_book()
    {
        using var factory = new BookSpaceApiFactory();
        using var reader = factory.CreateClient();
        using var admin = factory.CreateClient();
        await RegisterAsync(reader);
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync("/api/catalog-follows")).StatusCode);

        var suffix = Guid.NewGuid().ToString("N");
        var authorResponse = await admin.PostAsJsonAsync(
            "/api/admin/authors",
            new { name = $"Tác giả theo dõi {suffix}" });
        var authorId = (await ReadDataAsync(authorResponse)).GetProperty("id").GetGuid();
        var categoryResponse = await admin.PostAsJsonAsync(
            "/api/admin/categories",
            new { name = $"Thể loại theo dõi {suffix}" });
        var categoryId = (await ReadDataAsync(categoryResponse)).GetProperty("id").GetGuid();

        Assert.Equal(
            HttpStatusCode.OK,
            (await reader.PutAsync($"/api/catalog-follows/authors/{authorId}", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await reader.PutAsync($"/api/catalog-follows/categories/{categoryId}", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await reader.PutAsync($"/api/catalog-follows/authors/{authorId}", null)).StatusCode);

        var following = await ReadDataAsync(await reader.GetAsync("/api/catalog-follows"));
        Assert.Single(following.GetProperty("authors").EnumerateArray());
        Assert.Single(following.GetProperty("categories").EnumerateArray());

        Assert.Equal(
            HttpStatusCode.OK,
            (await reader.DeleteAsync($"/api/catalog-follows/authors/{authorId}")).StatusCode);
        Assert.Empty((await ReadDataAsync(await reader.GetAsync("/api/catalog-follows")))
            .GetProperty("authors")
            .EnumerateArray());
        Assert.Equal(
            HttpStatusCode.OK,
            (await reader.PutAsync($"/api/catalog-follows/authors/{authorId}", null)).StatusCode);

        var bookResponse = await admin.PostAsJsonAsync(
            "/api/admin/books",
            new
            {
                title = $"Sách mới theo dõi {suffix}",
                authorId,
                categoryIds = new[] { categoryId },
                pageCount = 160,
                publishedYear = 2026
            });
        Assert.Equal(HttpStatusCode.Created, bookResponse.StatusCode);
        var bookId = (await ReadDataAsync(bookResponse)).GetProperty("id").GetGuid();

        var recommendations = await ReadDataAsync(
            await reader.GetAsync("/api/books/recommendations?page=1&pageSize=12"));
        var firstRecommendation = recommendations.GetProperty("items").EnumerateArray().First();
        Assert.Equal(bookId, firstRecommendation.GetProperty("book").GetProperty("id").GetGuid());
        Assert.Equal("MATCHED_AUTHOR", firstRecommendation.GetProperty("reasonCode").GetString());

        var alerts = await ReadDataAsync(
            await reader.GetAsync("/api/notifications?category=CATALOG&page=1&pageSize=10"));
        var alert = Assert.Single(alerts.GetProperty("items").EnumerateArray());
        Assert.Equal("CATALOG", alert.GetProperty("type").GetString());
        Assert.Equal($"/books/{bookId}", alert.GetProperty("link").GetString());

        var preferences = await ReadDataAsync(await reader.GetAsync("/api/notifications/preferences"));
        Assert.True(preferences.GetProperty("isCatalogNotificationEnabled").GetBoolean());
        var disabled = await reader.PatchAsJsonAsync(
            "/api/notifications/preferences",
            new
            {
                isFollowNotificationEnabled = true,
                isCatalogNotificationEnabled = false,
                isReviewNotificationEnabled = true,
                isClubNotificationEnabled = true,
                isChallengeNotificationEnabled = true,
                isDirectMessageNotificationEnabled = true
            });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);

        var secondBook = await admin.PostAsJsonAsync(
            "/api/admin/books",
            new
            {
                title = $"Sách mới không báo {suffix}",
                authorId,
                categoryIds = new[] { categoryId },
                pageCount = 120,
                publishedYear = 2026
            });
        Assert.Equal(HttpStatusCode.Created, secondBook.StatusCode);
        var alertsAfterDisable = await ReadDataAsync(
            await reader.GetAsync("/api/notifications?category=CATALOG&page=1&pageSize=10"));
        Assert.Single(alertsAfterDisable.GetProperty("items").EnumerateArray());
    }

    private static async Task RegisterAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = $"catalog-follow-{suffix}@bookspace.local",
                password = "Reader123!",
                displayName = "Độc giả theo dõi catalog"
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            data.GetProperty("accessToken").GetString());
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            data.GetProperty("accessToken").GetString());
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
