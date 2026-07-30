using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ReadingInsightsService(
    IReadingInsightsRepository repository,
    IReadingGoalService readingGoalService,
    TimeProvider timeProvider) : IReadingInsightsService
{
    private static readonly int[] SupportedOverviewDays = [30, 90, 365];
    private static readonly int[] SupportedCalendarDays = [30, 90, 365];
    private static readonly int[] SupportedMonths = [6, 12, 24];

    public async Task<ReadingInsightsOverviewDto> GetOverviewAsync(
        Guid userId,
        int days,
        int utcOffsetMinutes,
        CancellationToken cancellationToken)
    {
        ValidateAllowed(days, SupportedOverviewDays, "INVALID_INSIGHTS_RANGE",
            "Khoảng thống kê chỉ hỗ trợ 30, 90 hoặc 365 ngày.");
        var offset = ValidateOffset(utcOffsetMinutes);
        var now = timeProvider.GetUtcNow();
        var today = LocalDate(now, offset);
        var currentFrom = today.AddDays(-(days - 1));
        var previousTo = currentFrom.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(days - 1));

        await readingGoalService.GetGoalsAsync(
            userId,
            null,
            1,
            1,
            cancellationToken);

        var sessions = (await repository.GetSessionsAsync(userId, cancellationToken))
            .Where(x => x.StartedAt <= now)
            .ToList();
        var finishedBooks = (await repository.GetFinishedBooksAsync(userId, cancellationToken))
            .Where(x => x.FinishedAt <= now)
            .ToList();
        var goals = await repository.GetGoalCountsAsync(userId, now, cancellationToken);
        var readingBooks = await repository.GetReadingBooksAsync(userId, cancellationToken);
        var activeGoals = await repository.GetActiveGoalsAsync(userId, now, cancellationToken);
        var currentSessions = SessionsInRange(sessions, currentFrom, today, offset);
        var previousSessions = SessionsInRange(sessions, previousFrom, previousTo, offset);
        var currentFinished = FinishedInRange(finishedBooks, currentFrom, today, offset);
        var previousFinished = FinishedInRange(finishedBooks, previousFrom, previousTo, offset);
        var currentActiveDays = ActiveDates(currentSessions, offset);
        var previousActiveDays = ActiveDates(previousSessions, offset);
        var allActiveDays = ActiveDates(sessions, offset);
        var recentFrom = today.AddDays(-29);
        var recentSessions = SessionsInRange(sessions, recentFrom, today, offset);
        var forecasts = BuildBookForecasts(
            readingBooks,
            recentSessions,
            today,
            offset);
        var goalForecasts = BuildGoalForecasts(
            activeGoals,
            sessions,
            finishedBooks,
            now,
            today,
            offset);

        return new ReadingInsightsOverviewDto(
            days,
            currentFrom,
            today,
            utcOffsetMinutes,
            currentSessions.Count,
            SumPages(currentSessions),
            SumMinutes(currentSessions),
            currentFinished.Count,
            currentActiveDays.Count,
            Average(SumPages(currentSessions), currentActiveDays.Count),
            Average(SumMinutes(currentSessions), currentActiveDays.Count),
            Average(currentSessions.Count, currentActiveDays.Count),
            CalculateCurrentStreak(allActiveDays, today),
            CalculateLongestStreak(allActiveDays),
            new ReadingGoalCountDto(
                goals.Total,
                goals.Active,
                goals.Completed,
                goals.Expired),
            BuildComparison(
                currentFrom,
                today,
                previousFrom,
                previousTo,
                currentSessions,
                previousSessions,
                currentActiveDays.Count,
                previousActiveDays.Count,
                currentFinished.Count,
                previousFinished.Count),
            forecasts,
            goalForecasts);
    }

    public async Task<ReadingCalendarDto> GetCalendarAsync(
        Guid userId,
        int? year,
        int days,
        int utcOffsetMinutes,
        CancellationToken cancellationToken)
    {
        var offset = ValidateOffset(utcOffsetMinutes);
        var now = timeProvider.GetUtcNow();
        var today = LocalDate(now, offset);
        DateOnly fromDate;
        DateOnly toDate;

        if (year.HasValue)
        {
            if (year.Value < 1900 || year.Value > today.Year)
            {
                throw ServiceErrors.BadRequest(
                    "INVALID_INSIGHTS_YEAR",
                    $"Năm thống kê phải từ 1900 đến {today.Year}.");
            }

            fromDate = new DateOnly(year.Value, 1, 1);
            toDate = new DateOnly(year.Value, 12, 31);
            days = toDate.DayNumber - fromDate.DayNumber + 1;
        }
        else
        {
            ValidateAllowed(days, SupportedCalendarDays, "INVALID_INSIGHTS_RANGE",
                "Lịch đọc chỉ hỗ trợ 30, 90 hoặc 365 ngày.");
            toDate = today;
            fromDate = today.AddDays(-(days - 1));
        }

        var sessions = (await repository.GetSessionsAsync(userId, cancellationToken))
            .Where(x => x.StartedAt <= now)
            .ToList();
        var selected = SessionsInRange(sessions, fromDate, toDate, offset);
        var byDate = selected
            .GroupBy(x => LocalDate(x.StartedAt, offset))
            .ToDictionary(x => x.Key, x => x.ToList());
        var daysData = Enumerable.Range(0, days)
            .Select(index => fromDate.AddDays(index))
            .Select(date =>
            {
                var values = byDate.GetValueOrDefault(date) ?? [];
                return new ReadingCalendarDayDto(
                    date,
                    values.Count,
                    SumPages(values),
                    SumMinutes(values),
                    values.Count > 0);
            })
            .ToList();

        return new ReadingCalendarDto(
            year,
            days,
            fromDate,
            toDate,
            utcOffsetMinutes,
            daysData.Count(x => x.IsActive),
            selected.Count,
            SumPages(selected),
            SumMinutes(selected),
            daysData);
    }

    public async Task<ReadingWeeklyInsightsDto> GetWeeklyAsync(
        Guid userId,
        int weeks,
        int utcOffsetMinutes,
        CancellationToken cancellationToken)
    {
        if (weeks is < 4 or > 52)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_INSIGHTS_WEEKS",
                "Số tuần thống kê phải từ 4 đến 52.");
        }

        var offset = ValidateOffset(utcOffsetMinutes);
        var now = timeProvider.GetUtcNow();
        var today = LocalDate(now, offset);
        var currentWeekStart = StartOfWeek(today);
        var fromDate = currentWeekStart.AddDays(-(weeks - 1) * 7);
        var toDate = currentWeekStart.AddDays(6);
        var sessions = (await repository.GetSessionsAsync(userId, cancellationToken))
            .Where(x => x.StartedAt <= now)
            .ToList();
        var finishedBooks = (await repository.GetFinishedBooksAsync(userId, cancellationToken))
            .Where(x => x.FinishedAt <= now)
            .ToList();

        var items = Enumerable.Range(0, weeks)
            .Select(index => fromDate.AddDays(index * 7))
            .Select(weekStart =>
            {
                var weekEnd = weekStart.AddDays(6);
                var weekSessions = SessionsInRange(sessions, weekStart, weekEnd, offset);
                var activeDays = ActiveDates(weekSessions, offset).Count;
                return new ReadingWeekDto(
                    weekStart,
                    weekEnd,
                    weekSessions.Count,
                    SumPages(weekSessions),
                    SumMinutes(weekSessions),
                    activeDays,
                    FinishedInRange(finishedBooks, weekStart, weekEnd, offset).Count,
                    Average(SumPages(weekSessions), activeDays),
                    Average(SumMinutes(weekSessions), activeDays));
            })
            .ToList();

        return new ReadingWeeklyInsightsDto(
            weeks,
            fromDate,
            toDate,
            utcOffsetMinutes,
            items);
    }

    public async Task<ReadingMonthlyInsightsDto> GetMonthlyAsync(
        Guid userId,
        int months,
        int utcOffsetMinutes,
        CancellationToken cancellationToken)
    {
        ValidateAllowed(months, SupportedMonths, "INVALID_INSIGHTS_MONTHS",
            "Số tháng thống kê chỉ hỗ trợ 6, 12 hoặc 24 tháng.");
        var offset = ValidateOffset(utcOffsetMinutes);
        var now = timeProvider.GetUtcNow();
        var today = LocalDate(now, offset);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var fromDate = currentMonthStart.AddMonths(-(months - 1));
        var toDate = currentMonthStart.AddMonths(1).AddDays(-1);
        var sessions = (await repository.GetSessionsAsync(userId, cancellationToken))
            .Where(x => x.StartedAt <= now)
            .ToList();
        var finishedBooks = (await repository.GetFinishedBooksAsync(userId, cancellationToken))
            .Where(x => x.FinishedAt <= now)
            .ToList();

        var items = Enumerable.Range(0, months)
            .Select(index => fromDate.AddMonths(index))
            .Select(monthStart =>
            {
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var monthSessions = SessionsInRange(sessions, monthStart, monthEnd, offset);
                var activeDays = ActiveDates(monthSessions, offset).Count;
                return new ReadingMonthDto(
                    monthStart,
                    monthEnd,
                    monthSessions.Count,
                    SumPages(monthSessions),
                    SumMinutes(monthSessions),
                    activeDays,
                    FinishedInRange(finishedBooks, monthStart, monthEnd, offset).Count,
                    Average(SumPages(monthSessions), activeDays),
                    Average(SumMinutes(monthSessions), activeDays));
            })
            .ToList();

        return new ReadingMonthlyInsightsDto(
            months,
            fromDate,
            toDate,
            utcOffsetMinutes,
            items);
    }

    private static ReadingPeriodComparisonDto BuildComparison(
        DateOnly currentFrom,
        DateOnly currentTo,
        DateOnly previousFrom,
        DateOnly previousTo,
        IReadOnlyCollection<ReadingSessionInsightData> currentSessions,
        IReadOnlyCollection<ReadingSessionInsightData> previousSessions,
        int currentActiveDays,
        int previousActiveDays,
        int currentFinishedBooks,
        int previousFinishedBooks) =>
        new(
            currentFrom,
            currentTo,
            previousFrom,
            previousTo,
            Compare(currentSessions.Count, previousSessions.Count),
            Compare(
                SumPages(currentSessions),
                SumPages(previousSessions)),
            Compare(
                SumMinutes(currentSessions),
                SumMinutes(previousSessions)),
            Compare(currentActiveDays, previousActiveDays),
            Compare(currentFinishedBooks, previousFinishedBooks));

    private static IReadOnlyList<ReadingBookForecastDto> BuildBookForecasts(
        IReadOnlyList<ReadingBookInsightData> books,
        IReadOnlyList<ReadingSessionInsightData> recentSessions,
        DateOnly today,
        TimeSpan offset) =>
        books
            .OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(book =>
            {
                var activity = recentSessions
                    .Where(x => x.BookId == book.BookId)
                    .ToList();
                var firstActivity = activity.Count == 0
                    ? (DateOnly?)null
                    : activity.Min(x => LocalDate(x.StartedAt, offset));
                var elapsedDays = firstActivity.HasValue
                    ? Math.Max(1, today.DayNumber - firstActivity.Value.DayNumber + 1)
                    : 0;
                var average = elapsedDays == 0
                    ? 0
                    : Math.Round(SumPages(activity) / (double)elapsedDays, 2);
                var remaining = Math.Max(0, book.PageCount - book.CurrentPage);
                var estimatedDays = EstimateDays(remaining, average);
                return new ReadingBookForecastDto(
                    book.LibraryItemId,
                    book.BookId,
                    book.Title,
                    book.CoverImageUrl,
                    book.CurrentPage,
                    book.PageCount,
                    remaining,
                    average,
                    estimatedDays,
                    AddDaysSafely(today, estimatedDays));
            })
            .ToList();

    private static IReadOnlyList<ReadingGoalForecastDto> BuildGoalForecasts(
        IReadOnlyList<ActiveReadingGoalInsightData> goals,
        IReadOnlyList<ReadingSessionInsightData> sessions,
        IReadOnlyList<FinishedBookInsightData> finishedBooks,
        DateTimeOffset now,
        DateOnly today,
        TimeSpan offset) =>
        goals
            .OrderBy(x => x.EndDate)
            .Select(goal =>
            {
                var pace = GetGoalPace(
                    goal,
                    sessions,
                    finishedBooks,
                    now,
                    today,
                    offset);
                var elapsedDays = pace.FirstActivityDate.HasValue
                    ? Math.Max(1, today.DayNumber - pace.FirstActivityDate.Value.DayNumber + 1)
                    : 0;
                var average = elapsedDays == 0
                    ? 0
                    : Math.Round(pace.Value / (double)elapsedDays, 2);
                var remaining = Math.Max(0, goal.TargetValue - goal.CurrentValue);
                var estimatedDays = EstimateDays(remaining, average);
                var estimatedFinishDate = AddDaysSafely(today, estimatedDays);
                var localEnd = LocalDate(goal.EndDate, offset);
                bool? isOnTrack = estimatedFinishDate.HasValue
                    ? estimatedFinishDate.Value <= localEnd
                    : null;

                return new ReadingGoalForecastDto(
                    goal.GoalId,
                    goal.Metric,
                    goal.TargetValue,
                    goal.CurrentValue,
                    remaining,
                    goal.StartDate,
                    goal.EndDate,
                    average,
                    estimatedFinishDate,
                    isOnTrack);
            })
            .ToList();

    private static GoalPaceData GetGoalPace(
        ActiveReadingGoalInsightData goal,
        IReadOnlyList<ReadingSessionInsightData> sessions,
        IReadOnlyList<FinishedBookInsightData> finishedBooks,
        DateTimeOffset now,
        DateOnly today,
        TimeSpan offset)
    {
        var rollingFrom = today.AddDays(-29);
        var goalLocalStart = LocalDate(goal.StartDate, offset);
        var paceFrom = goalLocalStart > rollingFrom ? goalLocalStart : rollingFrom;
        if (paceFrom > today)
        {
            return new GoalPaceData(null, 0);
        }

        if (goal.Metric == ReadingGoalMetric.BOOKS)
        {
            var activities = finishedBooks
                .Where(x =>
                    x.FinishedAt >= goal.StartDate &&
                    x.FinishedAt <= now &&
                    x.FinishedAt <= goal.EndDate &&
                    LocalDate(x.FinishedAt, offset) >= paceFrom)
                .ToList();
            return activities.Count == 0
                ? new GoalPaceData(null, 0)
                : new GoalPaceData(
                    activities.Min(x => LocalDate(x.FinishedAt, offset)),
                    activities.Count);
        }

        var sessionActivities = sessions
            .Where(x =>
                x.StartedAt >= goal.StartDate &&
                x.StartedAt <= now &&
                x.StartedAt <= goal.EndDate &&
                LocalDate(x.StartedAt, offset) >= paceFrom)
            .ToList();
        if (sessionActivities.Count == 0)
        {
            return new GoalPaceData(null, 0);
        }

        var value = goal.Metric switch
        {
            ReadingGoalMetric.PAGES => SumPages(sessionActivities),
            ReadingGoalMetric.MINUTES => SumMinutes(sessionActivities),
            _ => 0
        };
        return new GoalPaceData(
            sessionActivities.Min(x => LocalDate(x.StartedAt, offset)),
            value);
    }

    private static int? EstimateDays(int remaining, double averagePerDay)
    {
        if (remaining <= 0)
        {
            return 0;
        }

        if (averagePerDay <= 0)
        {
            return null;
        }

        var estimated = Math.Ceiling(remaining / averagePerDay);
        return estimated > int.MaxValue ? null : (int)estimated;
    }

    private static DateOnly? AddDaysSafely(DateOnly date, int? days)
    {
        if (!days.HasValue || days.Value > DateOnly.MaxValue.DayNumber - date.DayNumber)
        {
            return null;
        }

        return date.AddDays(days.Value);
    }

    private static List<ReadingSessionInsightData> SessionsInRange(
        IEnumerable<ReadingSessionInsightData> sessions,
        DateOnly fromDate,
        DateOnly toDate,
        TimeSpan offset) =>
        sessions
            .Where(x =>
            {
                var date = LocalDate(x.StartedAt, offset);
                return date >= fromDate && date <= toDate;
            })
            .ToList();

    private static List<FinishedBookInsightData> FinishedInRange(
        IEnumerable<FinishedBookInsightData> books,
        DateOnly fromDate,
        DateOnly toDate,
        TimeSpan offset) =>
        books
            .Where(x =>
            {
                var date = LocalDate(x.FinishedAt, offset);
                return date >= fromDate && date <= toDate;
            })
            .ToList();

    private static HashSet<DateOnly> ActiveDates(
        IEnumerable<ReadingSessionInsightData> sessions,
        TimeSpan offset) =>
        sessions.Select(x => LocalDate(x.StartedAt, offset)).ToHashSet();

    private static DateOnly LocalDate(DateTimeOffset value, TimeSpan offset) =>
        DateOnly.FromDateTime(value.ToOffset(offset).DateTime);

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static int CalculateCurrentStreak(HashSet<DateOnly> activeDays, DateOnly today)
    {
        var cursor = today;
        if (!activeDays.Contains(cursor) && activeDays.Contains(cursor.AddDays(-1)))
        {
            cursor = cursor.AddDays(-1);
        }

        var streak = 0;
        while (activeDays.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private static int CalculateLongestStreak(HashSet<DateOnly> activeDays)
    {
        var longest = 0;
        var current = 0;
        DateOnly? previous = null;
        foreach (var date in activeDays.Order())
        {
            current = previous.HasValue && date.DayNumber == previous.Value.DayNumber + 1
                ? current + 1
                : 1;
            longest = Math.Max(longest, current);
            previous = date;
        }

        return longest;
    }

    private static long SumPages(IEnumerable<ReadingSessionInsightData> sessions) =>
        sessions.Sum(x => (long)x.PagesRead);

    private static long SumMinutes(IEnumerable<ReadingSessionInsightData> sessions) =>
        sessions.Sum(x => (long)x.DurationMinutes);

    private static double Average(long total, int divisor) =>
        divisor == 0 ? 0 : Math.Round(total / (double)divisor, 2);

    private static InsightMetricComparisonDto Compare(long current, long previous)
    {
        double? changePercent = previous == 0
            ? current == 0 ? 0 : null
            : Math.Round((current - previous) * 100d / previous, 2);
        return new InsightMetricComparisonDto(current, previous, changePercent);
    }

    private static void ValidateAllowed(
        int value,
        IReadOnlyCollection<int> supported,
        string code,
        string message)
    {
        if (!supported.Contains(value))
        {
            throw ServiceErrors.BadRequest(code, message);
        }
    }

    private static TimeSpan ValidateOffset(int utcOffsetMinutes)
    {
        if (utcOffsetMinutes is < -840 or > 840)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_UTC_OFFSET",
                "Độ lệch múi giờ phải từ -840 đến 840 phút.");
        }

        return TimeSpan.FromMinutes(utcOffsetMinutes);
    }

    private sealed record GoalPaceData(DateOnly? FirstActivityDate, long Value);
}
