using BookSpace.Application.Services;

namespace BookSpace.UnitTests;

public sealed class ChallengeProgressTests
{
    [Fact]
    public void Derived_progress_uses_closed_challenge_window_and_caps_at_target()
    {
        var start = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(30);
        var finishedBooks = new[]
        {
            start.AddTicks(-1),
            start,
            start.AddDays(10),
            end,
            end.AddTicks(1)
        };

        Assert.Equal(3, ChallengeProgress.Derive(finishedBooks, start, end, 10, 0));
        Assert.Equal(2, ChallengeProgress.Derive(finishedBooks, start, end, 2, 0));
    }

    [Fact]
    public void Derived_progress_never_reduces_the_server_high_water_mark()
    {
        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = start.AddDays(2);

        Assert.Equal(4, ChallengeProgress.Derive([], start, end, 5, 4));
    }
}
