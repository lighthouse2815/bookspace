using BookSpace.Domain.Common;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class LibraryItem : Entity
{
    private LibraryItem() { }

    public LibraryItem(Guid userId, Guid bookId, LibraryStatus status)
    {
        UserId = userId;
        BookId = bookId;
        ChangeStatus(status);
        UpdatedAt = null;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public LibraryStatus Status { get; private set; }
    public int CurrentPage { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    public void ChangeStatus(LibraryStatus status)
    {
        Status = status;
        var now = DateTimeOffset.UtcNow;
        if (status == LibraryStatus.READING)
        {
            StartedAt ??= now;
            FinishedAt = null;
        }
        else if (status == LibraryStatus.READ)
        {
            StartedAt ??= now;
            FinishedAt ??= now;
        }
        else
        {
            StartedAt = null;
            FinishedAt = null;
            CurrentPage = 0;
        }

        Touch();
    }

    public void UpdateProgress(int page, int bookPageCount)
    {
        if (page < 0 || page > bookPageCount)
        {
            throw new DomainException("INVALID_READING_PROGRESS", $"Trang hiện tại phải từ 0 đến {bookPageCount}.");
        }

        if (page < CurrentPage)
        {
            throw new DomainException("READING_PROGRESS_CANNOT_DECREASE", "Tiến độ đọc không thể giảm.");
        }

        CurrentPage = page;
        if (page > 0 && Status == LibraryStatus.WANT_TO_READ)
        {
            ChangeStatus(LibraryStatus.READING);
        }

        if (page == bookPageCount)
        {
            ChangeStatus(LibraryStatus.READ);
        }

        Touch();
    }

    public void Restore(LibraryStatus status)
    {
        if (!DeletedAt.HasValue)
        {
            return;
        }

        DeletedAt = null;
        ChangeStatus(status);
    }

    public void RestoreForReading() => Restore(LibraryStatus.READING);
}

public sealed class ReadingSession : Entity
{
    private ReadingSession() { }

    public ReadingSession(
        Guid userId,
        Guid bookId,
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt,
        int pagesRead,
        int durationMinutes,
        string? note)
        : this(
            userId,
            bookId,
            startedAt,
            endedAt,
            pagesRead,
            durationMinutes,
            note,
            allowPausedTime: false)
    {
    }

    private ReadingSession(
        Guid userId,
        Guid bookId,
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt,
        int pagesRead,
        int durationMinutes,
        string? note,
        bool allowPausedTime)
    {
        if (startedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            throw new DomainException("INVALID_READING_DATE", "Thời gian đọc không được nằm trong tương lai.");
        }

        var normalizedDuration = Guard.Positive(durationMinutes, "Số phút đã đọc");
        var normalizedEnd = endedAt ?? startedAt.AddMinutes(normalizedDuration);
        if (normalizedEnd <= startedAt)
        {
            throw new DomainException("INVALID_READING_DATE", "Thời gian kết thúc phải sau thời gian bắt đầu.");
        }

        var wallClockMinutes = (normalizedEnd - startedAt).TotalMinutes;
        if (endedAt.HasValue &&
            (!allowPausedTime && Math.Abs(wallClockMinutes - normalizedDuration) > 1 ||
             allowPausedTime && normalizedDuration - wallClockMinutes > 1))
        {
            throw new DomainException(
                "INVALID_READING_DURATION",
                "Thời lượng đọc không khớp với thời gian bắt đầu và kết thúc.");
        }

        UserId = userId;
        BookId = bookId;
        StartedAt = startedAt;
        EndedAt = normalizedEnd;
        PagesRead = Guard.Positive(pagesRead, "Số trang đã đọc");
        AppliedPagesHighWater = PagesRead;
        DurationMinutes = normalizedDuration;
        Note = Guard.Optional(note, "Ghi chú", 1000);
    }

    public static ReadingSession FromFocusReading(
        Guid userId,
        Guid bookId,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        int pagesRead,
        int durationMinutes,
        string? note) =>
        new(
            userId,
            bookId,
            startedAt,
            endedAt,
            pagesRead,
            durationMinutes,
            note,
            allowPausedTime: true);

    public int Correct(
        DateTimeOffset startedAt,
        int pagesRead,
        int durationMinutes,
        string? note)
    {
        var corrected = new ReadingSession(
            UserId,
            BookId,
            startedAt,
            endedAt: null,
            pagesRead,
            durationMinutes,
            note);
        StartedAt = corrected.StartedAt;
        EndedAt = corrected.EndedAt;
        PagesRead = corrected.PagesRead;
        DurationMinutes = corrected.DurationMinutes;
        Note = corrected.Note;
        var additionalPages = Math.Max(0, PagesRead - AppliedPagesHighWater);
        AppliedPagesHighWater = Math.Max(AppliedPagesHighWater, PagesRead);
        Touch();
        return additionalPages;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset EndedAt { get; private set; }
    public int PagesRead { get; private set; }
    public int AppliedPagesHighWater { get; private set; }
    public int DurationMinutes { get; private set; }
    public string? Note { get; private set; }
}

public sealed class ActiveReadingSession : Entity
{
    private ActiveReadingSession() { }

    public ActiveReadingSession(
        Guid userId,
        Guid bookId,
        int startPage,
        DateTimeOffset startedAt)
    {
        if (startPage < 0)
        {
            throw new DomainException(
                "INVALID_FOCUS_START_PAGE",
                "Trang bắt đầu phiên tập trung không hợp lệ.");
        }

        UserId = userId;
        BookId = bookId;
        StartPage = startPage;
        StartedAt = startedAt;
        LastResumedAt = startedAt;
        Status = ActiveReadingSessionStatus.RUNNING;
        CreatedAt = startedAt;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public ActiveReadingSessionStatus Status { get; private set; }
    public int StartPage { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? LastResumedAt { get; private set; }
    public long AccumulatedSeconds { get; private set; }

    public long ElapsedSecondsAt(DateTimeOffset now)
    {
        var runningSeconds = Status == ActiveReadingSessionStatus.RUNNING && LastResumedAt.HasValue
            ? WholeSecondsBetween(LastResumedAt.Value, now)
            : 0;
        return AccumulatedSeconds > long.MaxValue - runningSeconds
            ? long.MaxValue
            : AccumulatedSeconds + runningSeconds;
    }

    public void Pause(DateTimeOffset now)
    {
        if (Status == ActiveReadingSessionStatus.PAUSED)
        {
            return;
        }

        AccumulatedSeconds = ElapsedSecondsAt(now);
        LastResumedAt = null;
        Status = ActiveReadingSessionStatus.PAUSED;
        UpdatedAt = now;
    }

    public void Resume(DateTimeOffset now)
    {
        if (Status == ActiveReadingSessionStatus.RUNNING)
        {
            return;
        }

        LastResumedAt = now;
        Status = ActiveReadingSessionStatus.RUNNING;
        UpdatedAt = now;
    }

    private static long WholeSecondsBetween(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            return 0;
        }

        var seconds = Math.Floor((to - from).TotalSeconds);
        return seconds >= long.MaxValue ? long.MaxValue : (long)seconds;
    }
}
