using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class AdminDashboardFlowTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    [Fact]
    public async Task Dashboard_is_admin_only_and_returns_operational_metrics()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/admin/dashboard")).StatusCode);

        using var reader = factory.CreateClient();
        await LoginAsync(reader, "reader@bookspace.local", "Reader123!");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await reader.GetAsync("/api/admin/dashboard")).StatusCode);

        using var admin = factory.CreateClient();
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");
        var response = await admin.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.True(data.GetProperty("totalUsers").GetInt32() >= 3);
        Assert.True(data.GetProperty("totalBooks").GetInt32() >= 12);
        Assert.True(data.GetProperty("totalAuthors").GetInt32() >= 6);
        Assert.True(data.GetProperty("totalCategories").GetInt32() >= 5);
        Assert.True(data.GetProperty("totalReviews").GetInt32() >= 2);
        Assert.True(data.GetProperty("totalClubs").GetInt32() >= 1);
        Assert.True(data.GetProperty("publishedChallenges").GetInt32() >= 1);
        Assert.True(data.GetProperty("readingSessionsLast30Days").GetInt32() >= 2);
        Assert.True(data.GetProperty("pagesReadLast30Days").GetInt32() > 0);
        Assert.True(data.GetProperty("readingMinutesLast30Days").GetInt32() > 0);
        Assert.Equal(7, data.GetProperty("activityLast7Days").GetArrayLength());

        var recentUser = Assert.Single(
            data.GetProperty("recentUsers").EnumerateArray(),
            user => user.GetProperty("displayName").GetString() == "Hà Linh");
        Assert.Equal("USER", recentUser.GetProperty("role").GetString());
        Assert.False(recentUser.TryGetProperty("email", out _));
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
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
