using BookSpace.Domain.Common;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class ClubReadingSprint : Entity
{
    private const int MaximumTargetValue = 1_000_000;

    private ClubReadingSprint() { }

    public ClubReadingSprint(
        Guid clubId,
        Guid bookId,
        Guid createdById,
        string title,
        string? description,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        ReadingSprintTargetUnit targetUnit,
        int targetValue,
        DateTimeOffset createdAt)
    {
        EnsureIdentifier(clubId, "Mã câu lạc bộ");
        EnsureIdentifier(bookId, "Mã sách");
        EnsureIdentifier(createdById, "Mã người tạo");

        ClubId = clubId;
        BookId = bookId;
        CreatedById = createdById;
        ApplyDetails(
            bookId,
            title,
            description,
            startsAt,
            endsAt,
            targetUnit,
            targetValue);
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = null;
    }

    public Guid ClubId { get; private set; }
    public BookClub Club { get; private set; } = null!;
    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public ReadingSprintTargetUnit TargetUnit { get; private set; }
    public int TargetValue { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset? LastReminderAt { get; private set; }
    public ICollection<ClubReadingSprintParticipant> Participants { get; } =
        new List<ClubReadingSprintParticipant>();
    public ICollection<ClubReadingSprintCheckIn> CheckIns { get; } =
        new List<ClubReadingSprintCheckIn>();
    public ICollection<ClubReadingSprintMilestone> Milestones { get; } =
        new List<ClubReadingSprintMilestone>();

    public ReadingSprintStatus GetStatus(DateTimeOffset now)
    {
        if (CancelledAt.HasValue)
        {
            return ReadingSprintStatus.CANCELLED;
        }

        if (CompletedAt.HasValue)
        {
            return ReadingSprintStatus.COMPLETED;
        }

        var utcNow = now.ToUniversalTime();
        if (utcNow < StartsAt)
        {
            return ReadingSprintStatus.PLANNED;
        }

        return utcNow < EndsAt
            ? ReadingSprintStatus.ACTIVE
            : ReadingSprintStatus.ENDED;
    }

    public void RecordActivity(DateTimeOffset now)
    {
        var utcNow = now.ToUniversalTime();
        UpdatedAt = UpdatedAt.HasValue && UpdatedAt.Value >= utcNow
            ? UpdatedAt.Value.AddTicks(1)
            : utcNow;
    }

    public void Update(
        Guid bookId,
        string title,
        string? description,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        ReadingSprintTargetUnit targetUnit,
        int targetValue,
        DateTimeOffset now)
    {
        if (GetStatus(now) != ReadingSprintStatus.PLANNED)
        {
            throw new DomainException(
                "READING_SPRINT_UPDATE_NOT_ALLOWED",
                "Chỉ có thể chỉnh sửa đợt đọc trước thời điểm bắt đầu.");
        }

        EnsureIdentifier(bookId, "Mã sách");
        ApplyDetails(
            bookId,
            title,
            description,
            startsAt,
            endsAt,
            targetUnit,
            targetValue);
        Touch();
    }

    public bool Complete(DateTimeOffset now)
    {
        if (CompletedAt.HasValue)
        {
            return false;
        }

        if (CancelledAt.HasValue)
        {
            throw new DomainException(
                "READING_SPRINT_ALREADY_CANCELLED",
                "Đợt đọc đã bị hủy nên không thể hoàn thành.");
        }

        if (GetStatus(now) == ReadingSprintStatus.PLANNED)
        {
            throw new DomainException(
                "READING_SPRINT_NOT_STARTED",
                "Đợt đọc chưa bắt đầu nên chưa thể hoàn thành.");
        }

        CompletedAt = now.ToUniversalTime();
        Touch();
        return true;
    }

    public bool Cancel(DateTimeOffset now)
    {
        if (CancelledAt.HasValue)
        {
            return false;
        }

        if (CompletedAt.HasValue)
        {
            throw new DomainException(
                "READING_SPRINT_ALREADY_COMPLETED",
                "Đợt đọc đã hoàn thành nên không thể hủy.");
        }

        CancelledAt = now.ToUniversalTime();
        Touch();
        return true;
    }

    public bool MarkReminderSent(DateTimeOffset now)
    {
        if (GetStatus(now) != ReadingSprintStatus.ACTIVE)
        {
            throw new DomainException(
                "READING_SPRINT_NOT_ACTIVE",
                "Chỉ có thể gửi nhắc nhở khi đợt đọc đang diễn ra.");
        }

        var utcNow = now.ToUniversalTime();
        if (LastReminderAt?.UtcDateTime.Date == utcNow.UtcDateTime.Date)
        {
            return false;
        }

        LastReminderAt = utcNow;
        Touch();
        return true;
    }

    private void ApplyDetails(
        Guid bookId,
        string title,
        string? description,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        ReadingSprintTargetUnit targetUnit,
        int targetValue)
    {
        var utcStartsAt = startsAt.ToUniversalTime();
        var utcEndsAt = endsAt.ToUniversalTime();
        if (utcEndsAt <= utcStartsAt)
        {
            throw new DomainException(
                "INVALID_READING_SPRINT_PERIOD",
                "Thời điểm kết thúc phải ở sau thời điểm bắt đầu.");
        }

        if (!Enum.IsDefined(targetUnit))
        {
            throw new DomainException(
                "INVALID_READING_SPRINT_TARGET_UNIT",
                "Đơn vị mục tiêu của đợt đọc không hợp lệ.");
        }

        if (targetValue is <= 0 or > MaximumTargetValue)
        {
            throw new DomainException(
                "INVALID_READING_SPRINT_TARGET",
                $"Mục tiêu đợt đọc phải từ 1 đến {MaximumTargetValue:N0}.");
        }

        BookId = bookId;
        Title = Guard.Required(title, "Tên đợt đọc", 200);
        Description = Guard.Optional(description, "Mô tả đợt đọc", 2000);
        StartsAt = utcStartsAt;
        EndsAt = utcEndsAt;
        TargetUnit = targetUnit;
        TargetValue = targetValue;
    }

    private static void EnsureIdentifier(Guid id, string field)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("VALIDATION_ERROR", $"{field} không hợp lệ.");
        }
    }
}

public sealed class ClubReadingSprintParticipant : Entity
{
    private ClubReadingSprintParticipant() { }

    public ClubReadingSprintParticipant(
        Guid sprintId,
        Guid userId,
        DateTimeOffset joinedAt)
    {
        if (sprintId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException(
                "VALIDATION_ERROR",
                "Mã đợt đọc và mã người tham gia phải hợp lệ.");
        }

        SprintId = sprintId;
        UserId = userId;
        JoinedAt = joinedAt.ToUniversalTime();
        CreatedAt = JoinedAt;
    }

    public Guid SprintId { get; private set; }
    public ClubReadingSprint Sprint { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? LeftAt { get; private set; }
    public int ProgressValue { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? LastCheckInAt { get; private set; }
    public bool IsActive => !LeftAt.HasValue;

    public bool Rejoin(DateTimeOffset now)
    {
        if (!LeftAt.HasValue)
        {
            return false;
        }

        LeftAt = null;
        TouchAt(now);
        return true;
    }

    public bool Leave(DateTimeOffset now)
    {
        if (LeftAt.HasValue)
        {
            return false;
        }

        LeftAt = now.ToUniversalTime();
        TouchAt(now);
        return true;
    }

    public bool UpdateProgress(int progressValue, int targetValue, DateTimeOffset now)
    {
        if (!IsActive)
        {
            throw new DomainException(
                "READING_SPRINT_PARTICIPATION_INACTIVE",
                "Bạn cần tham gia lại đợt đọc trước khi cập nhật tiến độ.");
        }

        if (progressValue < ProgressValue)
        {
            throw new DomainException(
                "READING_SPRINT_PROGRESS_CANNOT_DECREASE",
                "Tiến độ đợt đọc không thể giảm.");
        }

        if (progressValue < 0 || progressValue > targetValue)
        {
            throw new DomainException(
                "INVALID_READING_SPRINT_PROGRESS",
                $"Tiến độ phải từ 0 đến {targetValue}.");
        }

        if (progressValue == ProgressValue)
        {
            return false;
        }

        ProgressValue = progressValue;
        LastCheckInAt = now.ToUniversalTime();
        if (progressValue == targetValue)
        {
            CompletedAt ??= LastCheckInAt;
        }

        TouchAt(now);
        return true;
    }

    private void TouchAt(DateTimeOffset now) => UpdatedAt = now.ToUniversalTime();
}

public sealed class ClubReadingSprintCheckIn : Entity
{
    private ClubReadingSprintCheckIn() { }

    public ClubReadingSprintCheckIn(
        Guid participantId,
        Guid sprintId,
        Guid userId,
        int progressValue,
        string? note,
        DateTimeOffset createdAt)
    {
        if (participantId == Guid.Empty || sprintId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException(
                "VALIDATION_ERROR",
                "Thông tin người cập nhật tiến độ không hợp lệ.");
        }

        if (progressValue <= 0)
        {
            throw new DomainException(
                "INVALID_READING_SPRINT_PROGRESS",
                "Tiến độ cập nhật phải lớn hơn 0.");
        }

        ParticipantId = participantId;
        SprintId = sprintId;
        UserId = userId;
        ProgressValue = progressValue;
        Note = Guard.Optional(note, "Ghi chú tiến độ", 1000);
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid ParticipantId { get; private set; }
    public ClubReadingSprintParticipant Participant { get; private set; } = null!;
    public Guid SprintId { get; private set; }
    public ClubReadingSprint Sprint { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public int ProgressValue { get; private set; }
    public string? Note { get; private set; }
}

public sealed class ClubReadingSprintMilestone : Entity
{
    private ClubReadingSprintMilestone() { }

    public ClubReadingSprintMilestone(
        Guid sprintId,
        Guid createdById,
        string title,
        string? description,
        int targetValue,
        int sprintTargetValue,
        DateTimeOffset createdAt)
    {
        if (sprintId == Guid.Empty || createdById == Guid.Empty)
        {
            throw new DomainException(
                "VALIDATION_ERROR",
                "Mã đợt đọc và mã người tạo cột mốc phải hợp lệ.");
        }

        SprintId = sprintId;
        CreatedById = createdById;
        ApplyDetails(title, description, targetValue, sprintTargetValue);
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid SprintId { get; private set; }
    public ClubReadingSprint Sprint { get; private set; } = null!;
    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int TargetValue { get; private set; }
    public ICollection<ClubReadingSprintMilestoneResponse> Responses { get; } =
        new List<ClubReadingSprintMilestoneResponse>();

    public void Update(
        string title,
        string? description,
        int targetValue,
        int sprintTargetValue)
    {
        ApplyDetails(title, description, targetValue, sprintTargetValue);
        Touch();
    }

    private void ApplyDetails(
        string title,
        string? description,
        int targetValue,
        int sprintTargetValue)
    {
        if (targetValue <= 0 || targetValue > sprintTargetValue)
        {
            throw new DomainException(
                "INVALID_READING_SPRINT_MILESTONE_TARGET",
                $"Mốc tiến độ phải từ 1 đến {sprintTargetValue}.");
        }

        Title = Guard.Required(title, "Tên cột mốc", 150);
        Description = Guard.Optional(description, "Mô tả cột mốc", 2000);
        TargetValue = targetValue;
    }
}

public sealed class ClubReadingSprintMilestoneResponse : Entity
{
    private ClubReadingSprintMilestoneResponse() { }

    public ClubReadingSprintMilestoneResponse(
        Guid milestoneId,
        Guid authorId,
        string content,
        DateTimeOffset createdAt)
    {
        if (milestoneId == Guid.Empty || authorId == Guid.Empty)
        {
            throw new DomainException(
                "VALIDATION_ERROR",
                "Mã cột mốc và mã tác giả phản hồi phải hợp lệ.");
        }

        MilestoneId = milestoneId;
        AuthorId = authorId;
        Content = Guard.Required(content, "Nội dung thảo luận", 2000);
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid MilestoneId { get; private set; }
    public ClubReadingSprintMilestone Milestone { get; private set; } = null!;
    public Guid AuthorId { get; private set; }
    public User Author { get; private set; } = null!;
    public string Content { get; private set; } = string.Empty;
}
