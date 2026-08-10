using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class AdminDashboardService(
    IBookSpaceDbContext db,
    TimeProvider timeProvider) : IAdminDashboardService
{
    public AdminDashboardDto Get()
    {
        var now = timeProvider.GetUtcNow();
        var thirtyDaysAgo = now.AddDays(-30);
        var todayStart = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            0,
            0,
            0,
            TimeSpan.Zero);
        var sevenDaysStart = todayStart.AddDays(-6);

        var recentSessions = db.ReadingSessions
            .Where(session => session.StartedAt >= thirtyDaysAgo && session.StartedAt <= now)
            .ToList();
        var dailySessions = recentSessions
            .Where(session => session.StartedAt >= sevenDaysStart)
            .ToList();
        var dailyUsers = db.Users
            .Where(user => user.CreatedAt >= sevenDaysStart && user.CreatedAt <= now)
            .ToList();

        var activity = Enumerable.Range(0, 7)
            .Select(offset => sevenDaysStart.AddDays(offset))
            .Select(dayStart =>
            {
                var dayEnd = dayStart.AddDays(1);
                var sessions = dailySessions
                    .Where(session => session.StartedAt >= dayStart && session.StartedAt < dayEnd)
                    .ToList();
                return new AdminDailyActivityDto(
                    DateOnly.FromDateTime(dayStart.UtcDateTime),
                    dailyUsers.Count(user => user.CreatedAt >= dayStart && user.CreatedAt < dayEnd),
                    sessions.Count,
                    sessions.Sum(session => session.PagesRead));
            })
            .ToList();

        var recentUsers = db.Users
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Take(6)
            .ToList()
            .Select(user => new AdminRecentUserDto(
                user.Id,
                user.DisplayName,
                user.Role,
                user.IsLocked,
                user.CreatedAt))
            .ToList();

        return new AdminDashboardDto(
            now,
            db.Users.Count(),
            db.Users.Count(user => user.IsLocked),
            db.Users.Count(user => user.CreatedAt >= thirtyDaysAgo && user.CreatedAt <= now),
            recentSessions.Select(session => session.UserId).Distinct().Count(),
            db.Books.Count(),
            db.Authors.Count(),
            db.Categories.Count(),
            db.Reviews.Count(),
            db.BookClubs.Count(),
            db.ReadingChallenges.Count(challenge => challenge.IsPublished),
            db.ReadingChallenges.Count(challenge =>
                challenge.IsPublished &&
                challenge.StartsAt <= now &&
                challenge.EndsAt >= now),
            db.ContentReports.Count(report => report.Status == ContentReportStatus.PENDING),
            recentSessions.Count,
            recentSessions.Sum(session => session.PagesRead),
            recentSessions.Sum(session => session.DurationMinutes),
            activity,
            recentUsers);
    }
}
