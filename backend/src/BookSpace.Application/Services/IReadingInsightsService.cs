using BookSpace.Application.Contracts;

namespace BookSpace.Application.Services;

public interface IReadingInsightsService
{
    Task<ReadingInsightsOverviewDto> GetOverviewAsync(
        Guid userId,
        int days,
        int utcOffsetMinutes,
        CancellationToken cancellationToken);

    Task<ReadingCalendarDto> GetCalendarAsync(
        Guid userId,
        int? year,
        int days,
        int utcOffsetMinutes,
        CancellationToken cancellationToken);

    Task<ReadingWeeklyInsightsDto> GetWeeklyAsync(
        Guid userId,
        int weeks,
        int utcOffsetMinutes,
        CancellationToken cancellationToken);

    Task<ReadingMonthlyInsightsDto> GetMonthlyAsync(
        Guid userId,
        int months,
        int utcOffsetMinutes,
        CancellationToken cancellationToken);
}
