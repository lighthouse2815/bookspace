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

        if (endedAt.HasValue &&
            Math.Abs((normalizedEnd - startedAt).TotalMinutes - normalizedDuration) > 1)
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
        DurationMinutes = normalizedDuration;
        Note = Guard.Optional(note, "Ghi chú", 1000);
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset EndedAt { get; private set; }
    public int PagesRead { get; private set; }
    public int DurationMinutes { get; private set; }
    public string? Note { get; private set; }
}
