using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Domain.Entities;
using BookSpace.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.IntegrationTests;

public sealed class ReadingInsightsFlowTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    private const int VietnamUtcOffsetMinutes = 420;
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromMinutes(VietnamUtcOffsetMinutes);
    private readonly BookSpaceApiFactory _factory = factory;

    [Fact]
    public async Task Insights_require_authentication_and_validate_all_ranges()
    {
        using var anonymous = _factory.CreateClient();
        var unauthorized = await anonymous.GetAsync("/api/insights/overview");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var reader = (await CreateAuthenticatedClientAsync()).Client;
        var localYear = DateTimeOffset.UtcNow.ToOffset(VietnamOffset).Year;
        var invalidRequests = new[]
        {
            ("/api/insights/overview?days=29", "INVALID_INSIGHTS_RANGE"),
            ("/api/insights/calendar?days=31", "INVALID_INSIGHTS_RANGE"),
            ("/api/insights/calendar?year=1899", "INVALID_INSIGHTS_YEAR"),
            ($"/api/insights/calendar?year={localYear + 1}&utcOffsetMinutes={VietnamUtcOffsetMinutes}",
                "INVALID_INSIGHTS_YEAR"),
            ("/api/insights/weekly?weeks=3", "INVALID_INSIGHTS_WEEKS"),
            ("/api/insights/weekly?weeks=53", "INVALID_INSIGHTS_WEEKS"),
            ("/api/insights/monthly?months=7", "INVALID_INSIGHTS_MONTHS"),
            ("/api/insights/overview?utcOffsetMinutes=841", "INVALID_UTC_OFFSET"),
            ("/api/insights/calendar?utcOffsetMinutes=-841", "INVALID_UTC_OFFSET")
        };

        foreach (var (endpoint, expectedCode) in invalidRequests)
        {
            var response = await reader.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var envelope = await ReadEnvelopeAsync(response);
            Assert.False(envelope.GetProperty("success").GetBoolean());
            Assert.Equal(expectedCode, envelope.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Authenticated_insights_cover_utc_plus_seven_calendar_streaks_reports_comparison_and_forecasts()
    {
        var authenticated = await CreateAuthenticatedClientAsync();
        using var reader = authenticated.Client;
        var books = await GetBooksAsync(reader);
        var readingBook = books[0];
        var finishedBook = books[1];
        var currentPage = Math.Min(100, readingBook.PageCount - 1);

        var addReading = await reader.PostAsJsonAsync("/api/library", new
        {
            bookId = readingBook.Id,
            shelf = "READING"
        });
        Assert.Equal(HttpStatusCode.Created, addReading.StatusCode);
        var readingItem = await ReadDataAsync(addReading);
        var libraryItemId = readingItem.GetProperty("id").GetGuid();

        var updateProgress = await reader.PatchAsJsonAsync(
            $"/api/library/{libraryItemId}",
            new { currentPage });
        Assert.Equal(HttpStatusCode.OK, updateProgress.StatusCode);

        var addFinished = await reader.PostAsJsonAsync("/api/library", new
        {
            bookId = finishedBook.Id,
            shelf = "READ"
        });
        Assert.Equal(HttpStatusCode.Created, addFinished.StatusCode);

        var localToday = DateOnly.FromDateTime(
            DateTimeOffset.UtcNow.ToOffset(VietnamOffset).DateTime);
        var sessions = new[]
        {
            new SessionSeed(localToday, 0, 1, 12, 20),
            new SessionSeed(localToday.AddDays(-1), 0, 30, 15, 30),
            new SessionSeed(localToday.AddDays(-2), 12, 0, 18, 25),
            new SessionSeed(localToday.AddDays(-10), 12, 0, 5, 10),
            new SessionSeed(localToday.AddDays(-9), 12, 0, 5, 10),
            new SessionSeed(localToday.AddDays(-8), 12, 0, 5, 10),
            new SessionSeed(localToday.AddDays(-7), 12, 0, 5, 10),
            new SessionSeed(localToday.AddDays(-35), 12, 0, 10, 15),
            new SessionSeed(localToday.AddDays(-365), 12, 0, 99, 99)
        };
        await SeedSessionsAsync(authenticated.UserId, readingBook.Id, sessions);

        var goalStart = AtLocal(localToday.AddDays(-15), 0, 0);
        var goalEnd = AtLocal(localToday.AddDays(60), 23, 59);
        var createGoal = await reader.PostAsJsonAsync("/api/reading-goals", new
        {
            metric = "PAGES",
            period = "CUSTOM",
            targetValue = 200,
            startDate = goalStart,
            endDate = goalEnd
        });
        Assert.Equal(HttpStatusCode.Created, createGoal.StatusCode);
        var goalId = (await ReadDataAsync(createGoal)).GetProperty("id").GetGuid();

        var overview = await GetDataAsync(
            reader,
            $"/api/insights/overview?days=30&utcOffsetMinutes={VietnamUtcOffsetMinutes}");
        Assert.Equal(VietnamUtcOffsetMinutes, overview.GetProperty("utcOffsetMinutes").GetInt32());
        Assert.Equal(30, overview.GetProperty("days").GetInt32());
        Assert.Equal(localToday.AddDays(-29).ToString("yyyy-MM-dd"),
            overview.GetProperty("fromDate").GetString());
        Assert.Equal(localToday.ToString("yyyy-MM-dd"),
            overview.GetProperty("toDate").GetString());
        Assert.Equal(7, overview.GetProperty("totalSessions").GetInt32());
        Assert.Equal(65, overview.GetProperty("totalPages").GetInt32());
        Assert.Equal(115, overview.GetProperty("totalMinutes").GetInt32());
        Assert.Equal(1, overview.GetProperty("booksFinished").GetInt32());
        Assert.Equal(7, overview.GetProperty("activeDays").GetInt32());
        Assert.Equal(3, overview.GetProperty("currentStreak").GetInt32());
        Assert.Equal(4, overview.GetProperty("longestStreak").GetInt32());

        var comparison = overview.GetProperty("comparison");
        Assert.Equal(7, comparison.GetProperty("sessions").GetProperty("current").GetInt32());
        Assert.Equal(1, comparison.GetProperty("sessions").GetProperty("previous").GetInt32());
        Assert.Equal(65, comparison.GetProperty("pages").GetProperty("current").GetInt32());
        Assert.Equal(10, comparison.GetProperty("pages").GetProperty("previous").GetInt32());
        Assert.Equal(
            550d,
            comparison.GetProperty("pages").GetProperty("changePercent").GetDouble(),
            precision: 2);
        Assert.Equal(
            JsonValueKind.Null,
            comparison.GetProperty("booksFinished").GetProperty("changePercent").ValueKind);

        var bookForecast = overview.GetProperty("forecasts")
            .EnumerateArray()
            .Single(item => item.GetProperty("libraryItemId").GetGuid() == libraryItemId);
        var remainingPages = readingBook.PageCount - currentPage;
        var expectedBookDays = (int)Math.Ceiling(remainingPages / 5.91d);
        Assert.Equal(readingBook.Id, bookForecast.GetProperty("bookId").GetGuid());
        Assert.Equal("5.91", bookForecast.GetProperty("averagePagesPerDay").GetRawText());
        Assert.Equal(remainingPages, bookForecast.GetProperty("remainingPages").GetInt32());
        Assert.Equal(expectedBookDays, bookForecast.GetProperty("estimatedDaysRemaining").GetInt32());
        Assert.Equal(
            localToday.AddDays(expectedBookDays).ToString("yyyy-MM-dd"),
            bookForecast.GetProperty("estimatedFinishDate").GetString());

        var goalForecast = overview.GetProperty("goalForecasts")
            .EnumerateArray()
            .Single(item => item.GetProperty("goalId").GetGuid() == goalId);
        Assert.Equal("PAGES", goalForecast.GetProperty("metric").GetString());
        Assert.Equal(65, goalForecast.GetProperty("currentValue").GetInt32());
        Assert.Equal(135, goalForecast.GetProperty("remainingValue").GetInt32());
        Assert.Equal("5.91", goalForecast.GetProperty("averagePerDay").GetRawText());
        Assert.True(goalForecast.GetProperty("isOnTrack").GetBoolean());

        var calendar = await GetDataAsync(
            reader,
            $"/api/insights/calendar?days=365&utcOffsetMinutes={VietnamUtcOffsetMinutes}");
        Assert.Equal(365, calendar.GetProperty("days").GetInt32());
        Assert.Equal(JsonValueKind.Null, calendar.GetProperty("year").ValueKind);
        Assert.Equal(localToday.AddDays(-364).ToString("yyyy-MM-dd"),
            calendar.GetProperty("fromDate").GetString());
        Assert.Equal(localToday.ToString("yyyy-MM-dd"),
            calendar.GetProperty("toDate").GetString());
        Assert.Equal(365, calendar.GetProperty("daysData").GetArrayLength());
        Assert.Equal(8, calendar.GetProperty("totalSessions").GetInt32());
        Assert.Equal(75, calendar.GetProperty("totalPages").GetInt32());
        var midnightBoundaryDay = calendar.GetProperty("daysData")
            .EnumerateArray()
            .Single(day =>
                day.GetProperty("date").GetString() ==
                localToday.AddDays(-1).ToString("yyyy-MM-dd"));
        Assert.Equal(15, midnightBoundaryDay.GetProperty("pagesRead").GetInt32());
        Assert.True(midnightBoundaryDay.GetProperty("isActive").GetBoolean());

        var weekly = await GetDataAsync(
            reader,
            $"/api/insights/weekly?weeks=12&utcOffsetMinutes={VietnamUtcOffsetMinutes}");
        Assert.Equal(12, weekly.GetProperty("items").GetArrayLength());
        Assert.Equal(
            8,
            weekly.GetProperty("items").EnumerateArray()
                .Sum(item => item.GetProperty("sessions").GetInt32()));

        var monthly = await GetDataAsync(
            reader,
            $"/api/insights/monthly?months=12&utcOffsetMinutes={VietnamUtcOffsetMinutes}");
        Assert.Equal(12, monthly.GetProperty("items").GetArrayLength());
        Assert.Equal(
            8,
            monthly.GetProperty("items").EnumerateArray()
                .Sum(item => item.GetProperty("sessions").GetInt32()));

        var other = await CreateAuthenticatedClientAsync();
        using var otherReader = other.Client;
        var isolatedOverview = await GetDataAsync(
            otherReader,
            $"/api/insights/overview?days=30&utcOffsetMinutes={VietnamUtcOffsetMinutes}");
        Assert.Equal(0, isolatedOverview.GetProperty("totalSessions").GetInt32());
        Assert.Equal(0, isolatedOverview.GetProperty("totalPages").GetInt32());
        Assert.Equal(0, isolatedOverview.GetProperty("activeDays").GetInt32());
        Assert.Empty(isolatedOverview.GetProperty("forecasts").EnumerateArray());
        Assert.Empty(isolatedOverview.GetProperty("goalForecasts").EnumerateArray());
    }

    [Fact]
    public async Task Overview_synchronizes_goal_completion_once_and_removes_it_from_active_forecasts()
    {
        var authenticated = await CreateAuthenticatedClientAsync();
        using var reader = authenticated.Client;
        var book = (await GetBooksAsync(reader))[0];
        var goalResponse = await reader.PostAsJsonAsync("/api/reading-goals", new
        {
            metric = "MINUTES",
            period = "WEEK",
            targetValue = 30,
            startDate = DateTimeOffset.UtcNow.AddHours(-2),
            endDate = DateTimeOffset.UtcNow.AddDays(7)
        });
        Assert.Equal(HttpStatusCode.Created, goalResponse.StatusCode);
        var goalId = (await ReadDataAsync(goalResponse)).GetProperty("id").GetGuid();

        var sessionResponse = await reader.PostAsJsonAsync("/api/reading-sessions", new
        {
            bookId = book.Id,
            startedAt = DateTimeOffset.UtcNow.AddMinutes(-45),
            durationMinutes = 30,
            pagesRead = 5,
            note = "Phiên đọc hoàn tất mục tiêu khi mở Insights."
        });
        Assert.Equal(HttpStatusCode.Created, sessionResponse.StatusCode);

        var endpoint =
            $"/api/insights/overview?days=30&utcOffsetMinutes={VietnamUtcOffsetMinutes}";
        var firstOverview = await GetDataAsync(reader, endpoint);
        var secondOverview = await GetDataAsync(reader, endpoint);

        Assert.Equal(1, firstOverview.GetProperty("goals").GetProperty("completed").GetInt32());
        Assert.Equal(0, firstOverview.GetProperty("goals").GetProperty("active").GetInt32());
        Assert.Equal(1, secondOverview.GetProperty("goals").GetProperty("completed").GetInt32());
        Assert.DoesNotContain(
            secondOverview.GetProperty("goalForecasts").EnumerateArray(),
            forecast => forecast.GetProperty("goalId").GetGuid() == goalId);

        var goal = await GetDataAsync(reader, $"/api/reading-goals/{goalId}");
        Assert.Equal("COMPLETED", goal.GetProperty("status").GetString());
        Assert.Equal(30, goal.GetProperty("currentValue").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, goal.GetProperty("completedAt").ValueKind);

        var notifications = await GetDataAsync(reader, "/api/notifications?pageSize=100");
        var completionNotifications = notifications.GetProperty("items")
            .EnumerateArray()
            .Where(notification =>
                notification.GetProperty("type").GetString() == "SYSTEM" &&
                notification.GetProperty("link").GetString() == "/goals")
            .ToList();
        Assert.Single(completionNotifications);
    }

    private async Task<AuthenticatedClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"insights-{suffix}@bookspace.local",
            password = "Reader123!",
            displayName = $"Độc giả Insights {suffix[..8]}"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                data.GetProperty("accessToken").GetString());
        return new AuthenticatedClient(
            client,
            data.GetProperty("user").GetProperty("id").GetGuid());
    }

    private static async Task<IReadOnlyList<BookSeed>> GetBooksAsync(HttpClient client)
    {
        var data = await GetDataAsync(client, "/api/books?page=1&pageSize=20");
        return data.GetProperty("items")
            .EnumerateArray()
            .Take(2)
            .Select(book => new BookSeed(
                book.GetProperty("id").GetGuid(),
                book.GetProperty("pageCount").GetInt32()))
            .ToList();
    }

    private async Task SeedSessionsAsync(
        Guid userId,
        Guid bookId,
        IEnumerable<SessionSeed> seeds)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var sessions = seeds
            .Select(seed =>
            {
                var startedAt = AtLocal(seed.Date, seed.Hour, seed.Minute);
                return new ReadingSession(
                    userId,
                    bookId,
                    startedAt,
                    startedAt.AddMinutes(seed.DurationMinutes),
                    seed.PagesRead,
                    seed.DurationMinutes,
                    null);
            })
            .ToList();
        db.ReadingSessionSet.AddRange(sessions);
        await db.SaveChangesAsync();
    }

    private static DateTimeOffset AtLocal(DateOnly date, int hour, int minute)
    {
        var local = DateTime.SpecifyKind(
            date.ToDateTime(new TimeOnly(hour, minute)),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(local, VietnamOffset).ToUniversalTime();
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

    private sealed record AuthenticatedClient(HttpClient Client, Guid UserId);
    private sealed record BookSeed(Guid Id, int PageCount);
    private sealed record SessionSeed(
        DateOnly Date,
        int Hour,
        int Minute,
        int PagesRead,
        int DurationMinutes);
}
