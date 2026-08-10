using BookSpace.Domain.Enums;

namespace BookSpace.Application.Contracts;

public sealed record AdminDashboardDto(
    DateTimeOffset GeneratedAt,
    int TotalUsers,
    int LockedUsers,
    int NewUsersLast30Days,
    int ActiveReadersLast30Days,
    int TotalBooks,
    int TotalAuthors,
    int TotalCategories,
    int TotalReviews,
    int TotalClubs,
    int PublishedChallenges,
    int ActiveChallenges,
    int PendingReports,
    int ReadingSessionsLast30Days,
    int PagesReadLast30Days,
    int ReadingMinutesLast30Days,
    IReadOnlyList<AdminDailyActivityDto> ActivityLast7Days,
    IReadOnlyList<AdminRecentUserDto> RecentUsers);

public sealed record AdminDailyActivityDto(
    DateOnly Date,
    int NewUsers,
    int ReadingSessions,
    int PagesRead);

public sealed record AdminRecentUserDto(
    Guid Id,
    string DisplayName,
    UserRole Role,
    bool IsLocked,
    DateTimeOffset JoinedAt);
