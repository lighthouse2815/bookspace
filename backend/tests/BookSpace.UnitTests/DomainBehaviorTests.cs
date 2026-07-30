using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.UnitTests;

public sealed class DomainBehaviorTests
{
    [Fact]
    public void Library_progress_starts_and_finishes_reading()
    {
        var item = new LibraryItem(Guid.NewGuid(), Guid.NewGuid(), LibraryStatus.WANT_TO_READ);

        item.UpdateProgress(20, 100);
        Assert.Equal(LibraryStatus.READING, item.Status);
        Assert.NotNull(item.StartedAt);

        item.UpdateProgress(100, 100);
        Assert.Equal(LibraryStatus.READ, item.Status);
        Assert.NotNull(item.FinishedAt);
    }

    [Fact]
    public void Library_rejects_progress_past_book_length()
    {
        var item = new LibraryItem(Guid.NewGuid(), Guid.NewGuid(), LibraryStatus.READING);

        var error = Assert.Throws<DomainException>(() => item.UpdateProgress(101, 100));

        Assert.Equal("INVALID_READING_PROGRESS", error.Code);
    }

    [Fact]
    public void Library_progress_cannot_move_backwards()
    {
        var item = new LibraryItem(Guid.NewGuid(), Guid.NewGuid(), LibraryStatus.READING);
        item.UpdateProgress(40, 100);

        var error = Assert.Throws<DomainException>(() => item.UpdateProgress(39, 100));

        Assert.Equal("READING_PROGRESS_CANNOT_DECREASE", error.Code);
    }

    [Fact]
    public void Challenge_progress_is_monotonic_and_capped()
    {
        var participation = new ChallengeParticipation(Guid.NewGuid(), Guid.NewGuid());
        participation.UpdateProgress(3, 5);

        Assert.Throws<DomainException>(() => participation.UpdateProgress(2, 5));
        Assert.Throws<DomainException>(() => participation.UpdateProgress(6, 5));

        participation.UpdateProgress(5, 5);
        var completedAt = Assert.IsType<DateTimeOffset>(participation.CompletedAt);

        participation.UpdateProgress(5, 5);
        Assert.Equal(completedAt, participation.CompletedAt);
    }

    [Fact]
    public void Follow_rejects_following_self()
    {
        var userId = Guid.NewGuid();

        var error = Assert.Throws<DomainException>(() => new Follow(userId, userId));

        Assert.Equal("CANNOT_FOLLOW_SELF", error.Code);
    }

    [Fact]
    public void Review_rating_must_be_between_one_and_five()
    {
        var error = Assert.Throws<DomainException>(() =>
            new Review(Guid.NewGuid(), Guid.NewGuid(), 6, "Nội dung hợp lệ", false));

        Assert.Equal("VALIDATION_ERROR", error.Code);
    }

    [Fact]
    public void Reading_session_rejects_inconsistent_duration()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-2);

        var error = Assert.Throws<DomainException>(() =>
            new ReadingSession(
                Guid.NewGuid(),
                Guid.NewGuid(),
                startedAt,
                startedAt.AddMinutes(90),
                20,
                30,
                null));

        Assert.Equal("INVALID_READING_DURATION", error.Code);
    }
}
