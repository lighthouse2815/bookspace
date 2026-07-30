using BookSpace.Domain.Enums;

namespace BookSpace.Application.Contracts;

public sealed record ReadingGoalCountDto(
    int Total,
    int Active,
    int Completed,
    int Expired);

public sealed record InsightMetricComparisonDto(
    long Current,
    long Previous,
    double? ChangePercent);

public sealed record ReadingPeriodComparisonDto(
    DateOnly CurrentFromDate,
    DateOnly CurrentToDate,
    DateOnly PreviousFromDate,
    DateOnly PreviousToDate,
    InsightMetricComparisonDto Sessions,
    InsightMetricComparisonDto Pages,
    InsightMetricComparisonDto Minutes,
    InsightMetricComparisonDto ActiveDays,
    InsightMetricComparisonDto BooksFinished);

public sealed record ReadingBookForecastDto(
    Guid LibraryItemId,
    Guid BookId,
    string Title,
    string? CoverImageUrl,
    int CurrentPage,
    int PageCount,
    int RemainingPages,
    double AveragePagesPerDay,
    int? EstimatedDaysRemaining,
    DateOnly? EstimatedFinishDate);

public sealed record ReadingGoalForecastDto(
    Guid GoalId,
    ReadingGoalMetric Metric,
    int TargetValue,
    int CurrentValue,
    int RemainingValue,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    double AveragePerDay,
    DateOnly? EstimatedFinishDate,
    bool? IsOnTrack);

public sealed record ReadingInsightsOverviewDto(
    int Days,
    DateOnly FromDate,
    DateOnly ToDate,
    int UtcOffsetMinutes,
    int TotalSessions,
    long TotalPages,
    long TotalMinutes,
    int BooksFinished,
    int ActiveDays,
    double AveragePagesPerActiveDay,
    double AverageMinutesPerActiveDay,
    double AverageSessionsPerActiveDay,
    int CurrentStreak,
    int LongestStreak,
    ReadingGoalCountDto Goals,
    ReadingPeriodComparisonDto Comparison,
    IReadOnlyList<ReadingBookForecastDto> Forecasts,
    IReadOnlyList<ReadingGoalForecastDto> GoalForecasts);

public sealed record ReadingCalendarDayDto(
    DateOnly Date,
    int SessionCount,
    long PagesRead,
    long MinutesRead,
    bool IsActive);

public sealed record ReadingCalendarDto(
    int? Year,
    int Days,
    DateOnly FromDate,
    DateOnly ToDate,
    int UtcOffsetMinutes,
    int ActiveDays,
    int TotalSessions,
    long TotalPages,
    long TotalMinutes,
    IReadOnlyList<ReadingCalendarDayDto> DaysData);

public sealed record ReadingWeekDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int Sessions,
    long Pages,
    long Minutes,
    int ActiveDays,
    int BooksFinished,
    double AveragePagesPerActiveDay,
    double AverageMinutesPerActiveDay);

public sealed record ReadingWeeklyInsightsDto(
    int Weeks,
    DateOnly FromDate,
    DateOnly ToDate,
    int UtcOffsetMinutes,
    IReadOnlyList<ReadingWeekDto> Items);

public sealed record ReadingMonthDto(
    DateOnly MonthStart,
    DateOnly MonthEnd,
    int Sessions,
    long Pages,
    long Minutes,
    int ActiveDays,
    int BooksFinished,
    double AveragePagesPerActiveDay,
    double AverageMinutesPerActiveDay);

public sealed record ReadingMonthlyInsightsDto(
    int Months,
    DateOnly FromDate,
    DateOnly ToDate,
    int UtcOffsetMinutes,
    IReadOnlyList<ReadingMonthDto> Items);
