using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Enums;

namespace BookSpace.UnitTests;

public sealed class ReadingInsightsTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BookId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Overview_calculates_timezone_streak_comparison_and_forecasts()
    {
        var repository = new FakeReadingInsightsRepository
        {
            Sessions =
            [
                Session(BookId, "2026-07-29T17:30:00Z", 20, 30),
                Session(BookId, "2026-07-30T00:30:00Z", 10, 15),
                Session(BookId, "2026-07-28T18:00:00Z", 15, 20),
                Session(BookId, "2026-07-19T18:00:00Z", 15, 20),
                Session(BookId, "2026-07-20T18:00:00Z", 15, 20),
                Session(BookId, "2026-07-21T18:00:00Z", 15, 20),
                Session(BookId, "2026-06-29T18:00:00Z", 10, 10)
            ],
            FinishedBooks =
            [
                Finished("2026-07-10T10:00:00Z"),
                Finished("2026-06-10T10:00:00Z")
            ],
            GoalCounts = new ReadingGoalCountData(4, 2, 1, 1),
            ReadingBooks =
            [
                new ReadingBookInsightData(
                    Guid.NewGuid(),
                    BookId,
                    "Sách đang đọc",
                    "https://example.com/cover.jpg",
                    100,
                    300)
            ],
            ActiveGoals =
            [
                new ActiveReadingGoalInsightData(
                    Guid.NewGuid(),
                    ReadingGoalMetric.PAGES,
                    300,
                    100,
                    DateTimeOffset.Parse("2026-07-20T00:00:00Z"),
                    DateTimeOffset.Parse("2026-08-31T23:59:59Z")),
                new ActiveReadingGoalInsightData(
                    Guid.NewGuid(),
                    ReadingGoalMetric.MINUTES,
                    600,
                    0,
                    DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
                    DateTimeOffset.Parse("2026-08-31T23:59:59Z"))
            ]
        };
        var goalService = new FakeReadingGoalService();
        var service = CreateService(repository, goalService);

        var result = await service.GetOverviewAsync(
            UserId,
            30,
            420,
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 1), result.FromDate);
        Assert.Equal(new DateOnly(2026, 7, 30), result.ToDate);
        Assert.Equal(6, result.TotalSessions);
        Assert.Equal(90, result.TotalPages);
        Assert.Equal(125, result.TotalMinutes);
        Assert.Equal(1, result.BooksFinished);
        Assert.Equal(5, result.ActiveDays);
        Assert.Equal(18, result.AveragePagesPerActiveDay);
        Assert.Equal(25, result.AverageMinutesPerActiveDay);
        Assert.Equal(1.2, result.AverageSessionsPerActiveDay);
        Assert.Equal(2, result.CurrentStreak);
        Assert.Equal(3, result.LongestStreak);
        Assert.Equal(new ReadingGoalCountDto(4, 2, 1, 1), result.Goals);
        Assert.Equal(6, result.Comparison.Sessions.Current);
        Assert.Equal(1, result.Comparison.Sessions.Previous);
        Assert.Equal(500, result.Comparison.Sessions.ChangePercent);
        Assert.Equal(90, result.Comparison.Pages.Current);
        Assert.Equal(10, result.Comparison.Pages.Previous);
        Assert.Equal(800, result.Comparison.Pages.ChangePercent);
        Assert.Single(result.Forecasts);
        Assert.Equal("https://example.com/cover.jpg", result.Forecasts[0].CoverImageUrl);
        Assert.Equal(8.18, result.Forecasts[0].AveragePagesPerDay);
        Assert.Equal(25, result.Forecasts[0].EstimatedDaysRemaining);
        Assert.Equal(new DateOnly(2026, 8, 24), result.Forecasts[0].EstimatedFinishDate);
        Assert.Equal(2, result.GoalForecasts.Count);
        Assert.True(result.GoalForecasts[0].IsOnTrack);
        Assert.Null(result.GoalForecasts[1].EstimatedFinishDate);
        Assert.Null(result.GoalForecasts[1].IsOnTrack);
        Assert.Equal(1, goalService.SynchronizeCalls);
    }

    [Fact]
    public async Task Calendar_returns_every_day_and_groups_by_local_date()
    {
        var repository = new FakeReadingInsightsRepository
        {
            Sessions =
            [
                Session(BookId, "2024-02-28T18:00:00Z", 12, 20),
                Session(BookId, "2024-02-29T01:00:00Z", 8, 10)
            ]
        };
        var service = CreateService(
            repository,
            new FakeReadingGoalService(),
            new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero));

        var result = await service.GetCalendarAsync(
            UserId,
            2024,
            365,
            420,
            CancellationToken.None);

        Assert.Equal(2024, result.Year);
        Assert.Equal(366, result.Days);
        Assert.Equal(new DateOnly(2024, 1, 1), result.FromDate);
        Assert.Equal(new DateOnly(2024, 12, 31), result.ToDate);
        Assert.Equal(366, result.DaysData.Count);
        var leapDay = Assert.Single(
            result.DaysData,
            x => x.Date == new DateOnly(2024, 2, 29));
        Assert.Equal(2, leapDay.SessionCount);
        Assert.Equal(20, leapDay.PagesRead);
        Assert.True(leapDay.IsActive);
    }

    [Fact]
    public async Task Rolling_calendar_defaults_to_requested_number_of_days()
    {
        var service = CreateService(
            new FakeReadingInsightsRepository(),
            new FakeReadingGoalService());

        var result = await service.GetCalendarAsync(
            UserId,
            null,
            365,
            0,
            CancellationToken.None);

        Assert.Null(result.Year);
        Assert.Equal(365, result.Days);
        Assert.Equal(new DateOnly(2025, 7, 31), result.FromDate);
        Assert.Equal(new DateOnly(2026, 7, 30), result.ToDate);
        Assert.Equal(365, result.DaysData.Count);
    }

    [Fact]
    public async Task Weekly_buckets_start_on_monday_in_selected_timezone()
    {
        var repository = new FakeReadingInsightsRepository
        {
            Sessions =
            [
                Session(BookId, "2026-07-26T18:00:00Z", 25, 35)
            ],
            FinishedBooks =
            [
                Finished("2026-07-27T01:00:00Z")
            ]
        };
        var service = CreateService(repository, new FakeReadingGoalService());

        var result = await service.GetWeeklyAsync(
            UserId,
            4,
            420,
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 6), result.FromDate);
        Assert.Equal(new DateOnly(2026, 8, 2), result.ToDate);
        Assert.Equal(4, result.Items.Count);
        var current = result.Items[^1];
        Assert.Equal(DayOfWeek.Monday, current.WeekStart.DayOfWeek);
        Assert.Equal(new DateOnly(2026, 7, 27), current.WeekStart);
        Assert.Equal(1, current.Sessions);
        Assert.Equal(25, current.Pages);
        Assert.Equal(1, current.BooksFinished);
    }

    [Fact]
    public async Task Monthly_returns_ordered_calendar_month_buckets()
    {
        var repository = new FakeReadingInsightsRepository
        {
            Sessions =
            [
                Session(BookId, "2026-06-30T18:00:00Z", 20, 25),
                Session(BookId, "2026-05-15T10:00:00Z", 10, 15)
            ],
            FinishedBooks =
            [
                Finished("2026-06-30T18:30:00Z")
            ]
        };
        var service = CreateService(repository, new FakeReadingGoalService());

        var result = await service.GetMonthlyAsync(
            UserId,
            6,
            420,
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 2, 1), result.FromDate);
        Assert.Equal(new DateOnly(2026, 7, 31), result.ToDate);
        Assert.Equal(6, result.Items.Count);
        Assert.Equal(new DateOnly(2026, 7, 1), result.Items[^1].MonthStart);
        Assert.Equal(1, result.Items[^1].Sessions);
        Assert.Equal(20, result.Items[^1].Pages);
        Assert.Equal(1, result.Items[^1].BooksFinished);
    }

    [Theory]
    [InlineData(29, 0, "INVALID_INSIGHTS_RANGE")]
    [InlineData(30, -841, "INVALID_UTC_OFFSET")]
    [InlineData(30, 841, "INVALID_UTC_OFFSET")]
    public async Task Overview_rejects_invalid_query_values(
        int days,
        int utcOffsetMinutes,
        string expectedCode)
    {
        var service = CreateService(
            new FakeReadingInsightsRepository(),
            new FakeReadingGoalService());

        var error = await Assert.ThrowsAsync<UseCaseException>(() =>
            service.GetOverviewAsync(
                UserId,
                days,
                utcOffsetMinutes,
                CancellationToken.None));

        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Calendar_rejects_future_year()
    {
        var service = CreateService(
            new FakeReadingInsightsRepository(),
            new FakeReadingGoalService());

        var error = await Assert.ThrowsAsync<UseCaseException>(() =>
            service.GetCalendarAsync(
                UserId,
                2027,
                365,
                0,
                CancellationToken.None));

        Assert.Equal("INVALID_INSIGHTS_YEAR", error.Code);
    }

    [Theory]
    [InlineData(3, "INVALID_INSIGHTS_WEEKS")]
    [InlineData(53, "INVALID_INSIGHTS_WEEKS")]
    public async Task Weekly_rejects_out_of_range_weeks(int weeks, string expectedCode)
    {
        var service = CreateService(
            new FakeReadingInsightsRepository(),
            new FakeReadingGoalService());

        var error = await Assert.ThrowsAsync<UseCaseException>(() =>
            service.GetWeeklyAsync(
                UserId,
                weeks,
                0,
                CancellationToken.None));

        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public async Task Monthly_rejects_unsupported_month_count()
    {
        var service = CreateService(
            new FakeReadingInsightsRepository(),
            new FakeReadingGoalService());

        var error = await Assert.ThrowsAsync<UseCaseException>(() =>
            service.GetMonthlyAsync(
                UserId,
                7,
                0,
                CancellationToken.None));

        Assert.Equal("INVALID_INSIGHTS_MONTHS", error.Code);
    }

    private static ReadingInsightsService CreateService(
        FakeReadingInsightsRepository repository,
        FakeReadingGoalService goalService,
        DateTimeOffset? now = null) =>
        new(repository, goalService, new FixedTimeProvider(now ?? Now));

    private static ReadingSessionInsightData Session(
        Guid bookId,
        string startedAt,
        int pages,
        int minutes) =>
        new(bookId, DateTimeOffset.Parse(startedAt), pages, minutes);

    private static FinishedBookInsightData Finished(string finishedAt) =>
        new(Guid.NewGuid(), DateTimeOffset.Parse(finishedAt));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeReadingInsightsRepository : IReadingInsightsRepository
    {
        public IReadOnlyList<ReadingSessionInsightData> Sessions { get; init; } = [];
        public IReadOnlyList<FinishedBookInsightData> FinishedBooks { get; init; } = [];
        public ReadingGoalCountData GoalCounts { get; init; } = new(0, 0, 0, 0);
        public IReadOnlyList<ReadingBookInsightData> ReadingBooks { get; init; } = [];
        public IReadOnlyList<ActiveReadingGoalInsightData> ActiveGoals { get; init; } = [];

        public Task<IReadOnlyList<ReadingSessionInsightData>> GetSessionsAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Sessions);

        public Task<IReadOnlyList<FinishedBookInsightData>> GetFinishedBooksAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(FinishedBooks);

        public Task<ReadingGoalCountData> GetGoalCountsAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(GoalCounts);

        public Task<IReadOnlyList<ReadingBookInsightData>> GetReadingBooksAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(ReadingBooks);

        public Task<IReadOnlyList<ActiveReadingGoalInsightData>> GetActiveGoalsAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(ActiveGoals);
    }

    private sealed class FakeReadingGoalService : IReadingGoalService
    {
        public int SynchronizeCalls { get; private set; }

        public Task<PageResult<ReadingGoalDto>> GetGoalsAsync(
            Guid userId,
            ReadingGoalStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            SynchronizeCalls++;
            return Task.FromResult(PageResult<ReadingGoalDto>.Create([], page, pageSize, 0));
        }

        public Task<ReadingGoalDto> GetGoalAsync(
            Guid userId,
            Guid goalId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadingGoalDto> CreateAsync(
            Guid userId,
            CreateReadingGoalRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadingGoalDto> UpdateAsync(
            Guid userId,
            Guid goalId,
            UpdateReadingGoalRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(
            Guid userId,
            Guid goalId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
