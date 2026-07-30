using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class ReadingGoalsAndNotesFlowTests(BookSpaceApiFactory factory) : IClassFixture<BookSpaceApiFactory>
{
    [Fact]
    public async Task Reading_notes_support_crud_filter_search_and_owner_isolation()
    {
        using var owner = await CreateAuthenticatedClientAsync();
        var bookId = await GetFirstBookIdAsync(owner);

        var createResponse = await owner.PostAsJsonAsync("/api/reading-notes", new
        {
            bookId,
            pageNumber = 12,
            quote = "Một trích dẫn để kiểm thử tìm kiếm ghi chú.",
            content = "Nội dung ghi chú có từ khóa riêng biệt.",
            tags = new[] { "  Kinh điển ", "kinh điển", " Nhật ký " }
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadDataAsync(createResponse);
        var noteId = created.GetProperty("id").GetGuid();
        Assert.Equal(bookId, created.GetProperty("bookId").GetGuid());
        Assert.Equal(12, created.GetProperty("pageNumber").GetInt32());
        Assert.Equal(
            new[] { "Kinh điển", "Nhật ký" },
            created.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ToArray());

        var secondCreateResponse = await owner.PostAsJsonAsync("/api/reading-notes", new
        {
            bookId,
            content = "Ghi chú thứ hai không mang thẻ cần lọc.",
            tags = new[] { "Đọc lại" }
        });
        Assert.Equal(HttpStatusCode.Created, secondCreateResponse.StatusCode);

        var tag = Uri.EscapeDataString("KINH ĐIỂN");
        var filtered = await GetDataAsync(owner, $"/api/reading-notes?bookId={bookId}&tag={tag}");
        Assert.Equal(1, filtered.GetProperty("totalItems").GetInt64());
        Assert.Equal(noteId, filtered.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid());

        var searched = await GetDataAsync(owner, "/api/reading-notes?search=từ%20khóa%20riêng%20biệt");
        Assert.Contains(
            searched.GetProperty("items").EnumerateArray(),
            note => note.GetProperty("id").GetGuid() == noteId);

        var updateResponse = await owner.PatchAsJsonAsync($"/api/reading-notes/{noteId}", new
        {
            pageNumber = 18,
            quote = "Trích dẫn đã cập nhật.",
            content = "Nội dung đã cập nhật sau khi đọc lại.",
            tags = new[] { "Đã đọc", "Kinh điển" }
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadDataAsync(updateResponse);
        Assert.Equal(18, updated.GetProperty("pageNumber").GetInt32());
        Assert.Equal("Nội dung đã cập nhật sau khi đọc lại.", updated.GetProperty("content").GetString());

        var loaded = await GetDataAsync(owner, $"/api/reading-notes/{noteId}");
        Assert.Equal("Trích dẫn đã cập nhật.", loaded.GetProperty("quote").GetString());

        using var otherUser = await CreateAuthenticatedClientAsync();
        var inaccessible = await otherUser.GetAsync($"/api/reading-notes/{noteId}");
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);
        Assert.Equal(
            "READING_NOTE_NOT_FOUND",
            (await ReadEnvelopeAsync(inaccessible)).GetProperty("code").GetString());
        var otherUserNotes = await GetDataAsync(otherUser, "/api/reading-notes");
        Assert.Equal(0, otherUserNotes.GetProperty("totalItems").GetInt64());

        var deleteResponse = await owner.DeleteAsync($"/api/reading-notes/{noteId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/reading-notes/{noteId}")).StatusCode);
    }

    [Fact]
    public async Task Reading_goals_support_crud_overlap_conflict_and_owner_isolation()
    {
        using var owner = await CreateAuthenticatedClientAsync();
        var startDate = DateTimeOffset.UtcNow.AddMinutes(-5);
        var endDate = DateTimeOffset.UtcNow.AddDays(3);

        var invalidMetricResponse = await owner.PostAsJsonAsync("/api/reading-goals", new
        {
            metric = 999,
            period = "CUSTOM",
            targetValue = 120,
            startDate,
            endDate
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidMetricResponse.StatusCode);
        Assert.Equal(
            "INVALID_READING_GOAL_METRIC",
            (await ReadEnvelopeAsync(invalidMetricResponse)).GetProperty("code").GetString());

        var invalidPeriodResponse = await owner.PostAsJsonAsync("/api/reading-goals", new
        {
            metric = "PAGES",
            period = 999,
            targetValue = 120,
            startDate,
            endDate
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPeriodResponse.StatusCode);
        Assert.Equal(
            "INVALID_READING_GOAL_PERIOD",
            (await ReadEnvelopeAsync(invalidPeriodResponse)).GetProperty("code").GetString());

        var initialRequest = new
        {
            metric = "PAGES",
            period = "CUSTOM",
            targetValue = 120,
            startDate,
            endDate
        };

        var createResponse = await owner.PostAsJsonAsync("/api/reading-goals", initialRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadDataAsync(createResponse);
        var goalId = created.GetProperty("id").GetGuid();
        Assert.Equal("ACTIVE", created.GetProperty("status").GetString());
        Assert.Equal(120, created.GetProperty("targetValue").GetInt32());

        var overlapResponse = await owner.PostAsJsonAsync("/api/reading-goals", new
        {
            metric = "PAGES",
            period = "CUSTOM",
            targetValue = 200,
            startDate = startDate.AddHours(1),
            endDate = endDate.AddDays(1)
        });
        Assert.Equal(HttpStatusCode.Conflict, overlapResponse.StatusCode);
        Assert.Equal(
            "READING_GOAL_OVERLAPS",
            (await ReadEnvelopeAsync(overlapResponse)).GetProperty("code").GetString());

        var updateResponse = await owner.PatchAsJsonAsync($"/api/reading-goals/{goalId}", new
        {
            metric = "PAGES",
            period = "CUSTOM",
            targetValue = 180,
            startDate,
            endDate
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(180, (await ReadDataAsync(updateResponse)).GetProperty("targetValue").GetInt32());

        var activeGoals = await GetDataAsync(owner, "/api/reading-goals?status=ACTIVE");
        Assert.Contains(
            activeGoals.GetProperty("items").EnumerateArray(),
            goal => goal.GetProperty("id").GetGuid() == goalId);

        using var otherUser = await CreateAuthenticatedClientAsync();
        var inaccessible = await otherUser.GetAsync($"/api/reading-goals/{goalId}");
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);
        Assert.Equal(
            "READING_GOAL_NOT_FOUND",
            (await ReadEnvelopeAsync(inaccessible)).GetProperty("code").GetString());

        var deleteResponse = await owner.DeleteAsync($"/api/reading-goals/{goalId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/reading-goals/{goalId}")).StatusCode);
    }

    [Fact]
    public async Task Reading_goal_progress_is_calculated_from_a_reading_session()
    {
        using var reader = await CreateAuthenticatedClientAsync();
        var bookId = await GetFirstBookIdAsync(reader);
        var startDate = DateTimeOffset.UtcNow.AddHours(-1);
        var endDate = DateTimeOffset.UtcNow.AddDays(2);

        var goalResponse = await reader.PostAsJsonAsync("/api/reading-goals", new
        {
            metric = "PAGES",
            period = "CUSTOM",
            targetValue = 12,
            startDate,
            endDate
        });
        Assert.Equal(HttpStatusCode.Created, goalResponse.StatusCode);
        var goalId = (await ReadDataAsync(goalResponse)).GetProperty("id").GetGuid();

        var sessionResponse = await reader.PostAsJsonAsync("/api/reading-sessions", new
        {
            bookId,
            startedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            durationMinutes = 30,
            pagesRead = 12,
            note = "Phiên đọc dùng để cập nhật tiến độ mục tiêu."
        });
        Assert.Equal(HttpStatusCode.Created, sessionResponse.StatusCode);

        var completedGoals = await GetDataAsync(reader, "/api/reading-goals?status=COMPLETED");
        Assert.Contains(
            completedGoals.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == goalId);

        var goal = await GetDataAsync(reader, $"/api/reading-goals/{goalId}");
        Assert.Equal(12, goal.GetProperty("currentValue").GetInt32());
        Assert.Equal(100, goal.GetProperty("progressPercent").GetInt32());
        Assert.Equal("COMPLETED", goal.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, goal.GetProperty("completedAt").ValueKind);

        var notifications = await GetDataAsync(reader, "/api/notifications");
        Assert.Contains(
            notifications.GetProperty("items").EnumerateArray(),
            notification => notification.GetProperty("type").GetString() == "SYSTEM" &&
                            notification.GetProperty("link").GetString() == "/goals");
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"goals-notes-{suffix}@bookspace.local",
            password = "Reader123!",
            displayName = $"Độc giả Goals Notes {suffix[..8]}"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<Guid> GetFirstBookIdAsync(HttpClient client)
    {
        var books = await GetDataAsync(client, "/api/books");
        return books.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
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

    private static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
