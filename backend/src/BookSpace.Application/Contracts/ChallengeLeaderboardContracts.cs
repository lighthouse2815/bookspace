namespace BookSpace.Application.Contracts;

public sealed record ChallengeLeaderboardItem(
    int Rank,
    UserSummary User,
    int CurrentBooks,
    int TargetBooks,
    int ProgressPercent,
    DateTimeOffset? CompletedAt,
    bool IsCurrentUser);
