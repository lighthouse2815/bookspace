namespace BookSpace.Application.Services;

public static class ChallengeProgress
{
    public static int Derive(
        IEnumerable<DateTimeOffset> finishedBooks,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int targetBooks,
        int currentBooks)
    {
        var derived = finishedBooks.Count(finishedAt =>
            finishedAt >= startsAt && finishedAt <= endsAt);
        return Math.Max(currentBooks, Math.Min(derived, targetBooks));
    }
}
