using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.UnitTests;

public sealed class FocusReadingSessionTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 8, 1, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Active_session_counts_only_running_seconds_across_pause_and_resume()
    {
        var session = new ActiveReadingSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            25,
            StartedAt);

        Assert.Equal(90, session.ElapsedSecondsAt(StartedAt.AddSeconds(90)));

        session.Pause(StartedAt.AddSeconds(90));
        Assert.Equal(ActiveReadingSessionStatus.PAUSED, session.Status);
        Assert.Equal(90, session.ElapsedSecondsAt(StartedAt.AddHours(2)));

        session.Resume(StartedAt.AddMinutes(10));
        Assert.Equal(ActiveReadingSessionStatus.RUNNING, session.Status);
        Assert.Equal(130, session.ElapsedSecondsAt(StartedAt.AddMinutes(10).AddSeconds(40)));
    }

    [Fact]
    public void Pause_and_resume_are_idempotent_without_losing_elapsed_time()
    {
        var session = new ActiveReadingSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            StartedAt);

        session.Pause(StartedAt.AddSeconds(90));
        session.Pause(StartedAt.AddMinutes(5));
        Assert.Equal(90, session.ElapsedSecondsAt(StartedAt.AddMinutes(8)));

        session.Resume(StartedAt.AddMinutes(10));
        session.Resume(StartedAt.AddMinutes(11));
        Assert.Equal(210, session.ElapsedSecondsAt(StartedAt.AddMinutes(12)));
    }

    [Fact]
    public void Focus_completion_keeps_actual_wall_clock_end_and_active_minutes()
    {
        var completed = ReadingSession.FromFocusReading(
            Guid.NewGuid(),
            Guid.NewGuid(),
            StartedAt,
            StartedAt.AddHours(2),
            20,
            30,
            "Ghi chú riêng tư");

        Assert.Equal(StartedAt.AddHours(2), completed.EndedAt);
        Assert.Equal(30, completed.DurationMinutes);
        Assert.Equal(20, completed.PagesRead);
    }

    [Fact]
    public void Correction_applies_only_pages_above_the_persisted_high_water()
    {
        var session = new ReadingSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            StartedAt,
            endedAt: null,
            pagesRead: 10,
            durationMinutes: 20,
            note: null);

        Assert.Equal(0, session.Correct(StartedAt, 5, 20, null));
        Assert.Equal(0, session.Correct(StartedAt, 8, 20, null));
        Assert.Equal(2, session.Correct(StartedAt, 12, 20, null));
        Assert.Equal(12, session.AppliedPagesHighWater);
    }

    [Fact]
    public void Removed_library_item_can_be_restored_for_focus_without_losing_progress()
    {
        var item = new LibraryItem(Guid.NewGuid(), Guid.NewGuid(), LibraryStatus.READING);
        item.UpdateProgress(25, 100);
        item.SoftDelete();

        item.RestoreForReading();

        Assert.False(item.IsDeleted);
        Assert.Equal(LibraryStatus.READING, item.Status);
        Assert.Equal(25, item.CurrentPage);
    }

    [Fact]
    public void Removed_library_item_restored_to_want_to_read_resets_progress()
    {
        var item = new LibraryItem(Guid.NewGuid(), Guid.NewGuid(), LibraryStatus.READING);
        item.UpdateProgress(25, 100);
        item.SoftDelete();

        item.Restore(LibraryStatus.WANT_TO_READ);

        Assert.False(item.IsDeleted);
        Assert.Equal(LibraryStatus.WANT_TO_READ, item.Status);
        Assert.Equal(0, item.CurrentPage);
    }
}
