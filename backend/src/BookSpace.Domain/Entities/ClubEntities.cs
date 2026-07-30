using BookSpace.Domain.Common;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class BookClub : Entity
{
    private BookClub() { }

    public BookClub(Guid ownerId, string name, string? description, string? coverUrl, ClubVisibility visibility)
    {
        OwnerId = ownerId;
        Update(name, description, coverUrl, visibility);
        UpdatedAt = null;
    }

    public Guid OwnerId { get; private set; }
    public User Owner { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? CoverUrl { get; private set; }
    public ClubVisibility Visibility { get; private set; }
    public Guid? CurrentBookId { get; private set; }
    public Book? CurrentBook { get; private set; }
    public ICollection<BookClubMember> Members { get; } = new List<BookClubMember>();
    public ICollection<ClubPost> Posts { get; } = new List<ClubPost>();
    public ICollection<ClubInvitation> Invitations { get; } = new List<ClubInvitation>();

    public void Update(string name, string? description, string? coverUrl, ClubVisibility visibility)
    {
        Name = Guard.Required(name, "Tên câu lạc bộ", 150);
        Description = Guard.Optional(description, "Mô tả câu lạc bộ", 2000);
        CoverUrl = Guard.Optional(coverUrl, "Ảnh bìa câu lạc bộ", 1000);
        Visibility = visibility;
        Touch();
    }

    public void SetCurrentBook(Guid? bookId)
    {
        if (CurrentBookId == bookId)
        {
            return;
        }

        CurrentBookId = bookId;
        Touch();
    }
}

public sealed class BookClubMember : Entity
{
    private BookClubMember() { }
    public BookClubMember(Guid clubId, Guid userId, ClubMemberRole role)
    {
        ClubId = clubId;
        UserId = userId;
        Role = role;
    }

    public Guid ClubId { get; private set; }
    public BookClub Club { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public ClubMemberRole Role { get; private set; }

    public void RecordActivity(DateTimeOffset now)
    {
        var utcNow = now.ToUniversalTime();
        UpdatedAt = UpdatedAt.HasValue && UpdatedAt.Value >= utcNow
            ? UpdatedAt.Value.AddTicks(1)
            : utcNow;
    }

    public void ChangeRole(ClubMemberRole role)
    {
        if (Role == ClubMemberRole.OWNER)
        {
            throw new DomainException("OWNER_ROLE_IMMUTABLE", "Không thể thay đổi vai trò của chủ câu lạc bộ.");
        }

        if (role == ClubMemberRole.OWNER)
        {
            throw new DomainException("OWNER_ROLE_RESERVED", "Không thể gán vai trò chủ câu lạc bộ cho thành viên.");
        }

        if (role is not ClubMemberRole.MODERATOR and not ClubMemberRole.MEMBER)
        {
            throw new DomainException(
                "INVALID_CLUB_MEMBER_ROLE",
                "Vai trò thành viên câu lạc bộ không hợp lệ.");
        }

        if (Role == role)
        {
            return;
        }

        Role = role;
        Touch();
    }
}

public sealed class ClubInvitation : Entity
{
    private ClubInvitation() { }

    public ClubInvitation(
        Guid clubId,
        Guid inviterId,
        Guid invitedUserId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (expiresAt <= createdAt)
        {
            throw new DomainException(
                "INVALID_CLUB_INVITATION_EXPIRY",
                "Thời hạn lời mời phải ở sau thời điểm tạo.");
        }

        ClubId = clubId;
        InviterId = inviterId;
        InvitedUserId = invitedUserId;
        Status = ClubInvitationStatus.PENDING;
        CreatedAt = createdAt.ToUniversalTime();
        ExpiresAt = expiresAt.ToUniversalTime();
    }

    public Guid ClubId { get; private set; }
    public BookClub Club { get; private set; } = null!;
    public Guid InviterId { get; private set; }
    public User Inviter { get; private set; } = null!;
    public Guid InvitedUserId { get; private set; }
    public User InvitedUser { get; private set; } = null!;
    public ClubInvitationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    public bool ExpireIfNeeded(DateTimeOffset now)
    {
        if (Status != ClubInvitationStatus.PENDING || now < ExpiresAt)
        {
            return false;
        }

        Status = ClubInvitationStatus.EXPIRED;
        RespondedAt = now.ToUniversalTime();
        Touch();
        return true;
    }

    public bool Accept(DateTimeOffset now)
    {
        if (Status == ClubInvitationStatus.ACCEPTED)
        {
            return false;
        }

        EnsurePending(now);
        Status = ClubInvitationStatus.ACCEPTED;
        RespondedAt = now.ToUniversalTime();
        Touch();
        return true;
    }

    public bool Decline(DateTimeOffset now)
    {
        if (Status == ClubInvitationStatus.DECLINED)
        {
            return false;
        }

        EnsurePending(now);
        Status = ClubInvitationStatus.DECLINED;
        RespondedAt = now.ToUniversalTime();
        Touch();
        return true;
    }

    public bool Revoke(DateTimeOffset now)
    {
        if (Status == ClubInvitationStatus.REVOKED)
        {
            return false;
        }

        EnsurePending(now);
        Status = ClubInvitationStatus.REVOKED;
        RespondedAt = now.ToUniversalTime();
        Touch();
        return true;
    }

    private void EnsurePending(DateTimeOffset now)
    {
        if (ExpireIfNeeded(now))
        {
            throw new DomainException("CLUB_INVITATION_EXPIRED", "Lời mời tham gia câu lạc bộ đã hết hạn.");
        }

        if (Status != ClubInvitationStatus.PENDING)
        {
            throw new DomainException(
                "CLUB_INVITATION_NOT_PENDING",
                "Lời mời không còn ở trạng thái chờ xử lý.");
        }
    }
}

public sealed class ClubPost : Entity
{
    private ClubPost() { }
    public ClubPost(Guid clubId, Guid authorId, string title, string content)
    {
        ClubId = clubId;
        AuthorId = authorId;
        Title = Guard.Required(title, "Tiêu đề bài viết", 250);
        Content = Guard.Required(content, "Nội dung bài viết", 10000);
    }

    public Guid ClubId { get; private set; }
    public BookClub Club { get; private set; } = null!;
    public Guid AuthorId { get; private set; }
    public User Author { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public ICollection<ClubPostComment> Comments { get; } = new List<ClubPostComment>();
}

public sealed class ClubPostComment : Entity
{
    private ClubPostComment() { }
    public ClubPostComment(Guid postId, Guid authorId, string content)
    {
        PostId = postId;
        AuthorId = authorId;
        Content = Guard.Required(content, "Nội dung bình luận", 2000);
    }

    public Guid PostId { get; private set; }
    public ClubPost Post { get; private set; } = null!;
    public Guid AuthorId { get; private set; }
    public User Author { get; private set; } = null!;
    public string Content { get; private set; } = string.Empty;
}
