using BookSpace.Domain.Common;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class ReadingChallenge : Entity
{
    private ReadingChallenge() { }

    public ReadingChallenge(
        Guid createdById,
        string title,
        string? description,
        int targetBooks,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string? coverImageUrl,
        bool isPublished)
    {
        CreatedById = createdById;
        Update(title, description, targetBooks, startsAt, endsAt, coverImageUrl);
        IsPublished = isPublished;
        UpdatedAt = null;
    }

    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int TargetBooks { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public bool IsPublished { get; private set; }
    public ICollection<ChallengeParticipation> Participants { get; } = new List<ChallengeParticipation>();

    public void Update(
        string title,
        string? description,
        int targetBooks,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string? coverImageUrl)
    {
        if (endsAt <= startsAt)
        {
            throw new DomainException("INVALID_CHALLENGE_PERIOD", "Thời gian kết thúc phải sau thời gian bắt đầu.");
        }

        Title = Guard.Required(title, "Tên thử thách", 200);
        Description = Guard.Optional(description, "Mô tả thử thách", 2000);
        TargetBooks = Guard.Positive(targetBooks, "Mục tiêu số sách");
        StartsAt = startsAt;
        EndsAt = endsAt;
        CoverImageUrl = Guard.Optional(coverImageUrl, "Ảnh bìa thử thách", 1000);
        Touch();
    }

    public void Publish()
    {
        IsPublished = true;
        Touch();
    }

    public void Unpublish()
    {
        IsPublished = false;
        Touch();
    }
}

public sealed class ChallengeParticipation : Entity
{
    private ChallengeParticipation() { }
    public ChallengeParticipation(Guid challengeId, Guid userId)
    {
        ChallengeId = challengeId;
        UserId = userId;
    }

    public Guid ChallengeId { get; private set; }
    public ReadingChallenge Challenge { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public int CompletedBooks { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void UpdateProgress(int completedBooks, int targetBooks)
    {
        if (completedBooks < CompletedBooks)
        {
            throw new DomainException("INVALID_CHALLENGE_PROGRESS", "Tiến độ thử thách không thể giảm.");
        }

        if (completedBooks > targetBooks)
        {
            throw new DomainException("INVALID_CHALLENGE_PROGRESS", "Tiến độ không được vượt quá mục tiêu thử thách.");
        }

        CompletedBooks = completedBooks;
        if (completedBooks >= targetBooks && !CompletedAt.HasValue)
        {
            CompletedAt = DateTimeOffset.UtcNow;
        }

        Touch();
    }
}
