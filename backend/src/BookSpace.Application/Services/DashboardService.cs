using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class DashboardService(IBookSpaceDbContext db) : IDashboardService
{
    private readonly ServiceMapper _mapper = new(db);

    public DashboardDto Get(Guid userId)
    {
        var sessions = db.ReadingSessions.Where(x => x.UserId == userId).ToList();
        var readingItems = db.LibraryItems
            .Where(x => x.UserId == userId && x.Status == LibraryStatus.READING)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(6)
            .ToList()
            .Select(_mapper.Library)
            .ToList();
        var recentSessions = sessions
            .OrderByDescending(x => x.StartedAt)
            .Take(5)
            .Select(_mapper.Session)
            .ToList();
        var activeChallengeIds = db.ChallengeParticipations
            .Where(x => x.UserId == userId)
            .Select(x => x.ChallengeId)
            .ToList();
        var now = DateTimeOffset.UtcNow;
        var activeChallenges = db.ReadingChallenges
            .Where(x => activeChallengeIds.Contains(x.Id) && x.EndsAt >= now)
            .OrderBy(x => x.EndsAt)
            .Take(5)
            .ToList()
            .Select(x => _mapper.Challenge(x, userId))
            .ToList();

        var today = DateTimeOffset.UtcNow.Date;
        var weekly = Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(offset - 6))
            .Select(day => new WeeklyMetricDto(
                VietnameseDay(day.DayOfWeek),
                sessions.Where(x => x.StartedAt.UtcDateTime.Date == day).Sum(x => x.PagesRead)))
            .ToList();

        return new DashboardDto(
            db.LibraryItems.Count(x => x.UserId == userId && x.Status == LibraryStatus.READ),
            sessions.Sum(x => x.PagesRead),
            sessions.Sum(x => x.DurationMinutes),
            CalculateStreak(sessions.Select(x => x.StartedAt.UtcDateTime.Date).Distinct().ToHashSet(), today),
            weekly,
            readingItems,
            recentSessions,
            activeChallenges);
    }

    private static int CalculateStreak(HashSet<DateTime> days, DateTime today)
    {
        var cursor = today;
        if (!days.Contains(cursor) && days.Contains(cursor.AddDays(-1)))
        {
            cursor = cursor.AddDays(-1);
        }

        var streak = 0;
        while (days.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private static string VietnameseDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        _ => "CN"
    };
}
