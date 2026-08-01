using BookSpace.Domain.Common;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class User : Entity
{
    private User() { }

    public User(string email, string passwordHash, string displayName, UserRole role = UserRole.USER)
    {
        Email = Guard.Email(email);
        PasswordHash = Guard.Required(passwordHash, "Mật khẩu đã mã hóa", 500);
        DisplayName = Guard.Required(displayName, "Tên hiển thị", 100);
        Role = role;
    }

    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public UserRole Role { get; private set; } = UserRole.USER;
    public bool IsLocked { get; private set; }
    public bool IsReadingShelfPublic { get; private set; }
    public bool IsReadingActivityPublic { get; private set; }
    public bool IsFollowNotificationEnabled { get; private set; } = true;
    public bool IsReviewNotificationEnabled { get; private set; } = true;
    public bool IsClubNotificationEnabled { get; private set; } = true;
    public bool IsChallengeNotificationEnabled { get; private set; } = true;

    public ICollection<Follow> Followers { get; } = new List<Follow>();
    public ICollection<Follow> Following { get; } = new List<Follow>();

    public void UpdateProfile(string displayName, string? bio, string? avatarUrl)
    {
        DisplayName = Guard.Required(displayName, "Tên hiển thị", 100);
        Bio = Guard.Optional(bio, "Tiểu sử", 500);
        AvatarUrl = Guard.Optional(avatarUrl, "Ảnh đại diện", 1000);
        Touch();
    }

    public void UpdatePublicReadingVisibility(
        bool isReadingShelfPublic,
        bool isReadingActivityPublic)
    {
        if (IsReadingShelfPublic == isReadingShelfPublic &&
            IsReadingActivityPublic == isReadingActivityPublic)
        {
            return;
        }

        IsReadingShelfPublic = isReadingShelfPublic;
        IsReadingActivityPublic = isReadingActivityPublic;
        Touch();
    }

    public bool AllowsNotification(NotificationType type) => type switch
    {
        NotificationType.FOLLOW => IsFollowNotificationEnabled,
        NotificationType.REVIEW_LIKE or NotificationType.COMMENT => IsReviewNotificationEnabled,
        NotificationType.CLUB => IsClubNotificationEnabled,
        NotificationType.CHALLENGE => IsChallengeNotificationEnabled,
        NotificationType.SYSTEM => true,
        _ => true
    };

    public void UpdateNotificationPreferences(
        bool isFollowNotificationEnabled,
        bool isReviewNotificationEnabled,
        bool isClubNotificationEnabled,
        bool isChallengeNotificationEnabled)
    {
        if (IsFollowNotificationEnabled == isFollowNotificationEnabled &&
            IsReviewNotificationEnabled == isReviewNotificationEnabled &&
            IsClubNotificationEnabled == isClubNotificationEnabled &&
            IsChallengeNotificationEnabled == isChallengeNotificationEnabled)
        {
            return;
        }

        IsFollowNotificationEnabled = isFollowNotificationEnabled;
        IsReviewNotificationEnabled = isReviewNotificationEnabled;
        IsClubNotificationEnabled = isClubNotificationEnabled;
        IsChallengeNotificationEnabled = isChallengeNotificationEnabled;
        Touch();
    }

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = Guard.Required(passwordHash, "Mật khẩu đã mã hóa", 500);
        Touch();
    }

    public void Lock()
    {
        IsLocked = true;
        Touch();
    }

    public void Unlock()
    {
        IsLocked = false;
        Touch();
    }

    public void EnsureCanLogin()
    {
        if (IsDeleted || IsLocked)
        {
            throw new DomainException("ACCOUNT_UNAVAILABLE", "Tài khoản hiện không thể đăng nhập.");
        }
    }
}

public sealed class RefreshToken : Entity
{
    private RefreshToken() { }

    public RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        UserId = userId;
        TokenHash = Guard.Required(tokenHash, "Refresh token", 200);
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsActive => !RevokedAt.HasValue && ExpiresAt > DateTimeOffset.UtcNow;

    public void Revoke(Guid? replacedByTokenId = null)
    {
        if (!RevokedAt.HasValue)
        {
            RevokedAt = DateTimeOffset.UtcNow;
            ReplacedByTokenId = replacedByTokenId;
            Touch();
        }
    }
}

public sealed class Follow : Entity
{
    private Follow() { }

    public Follow(Guid followerId, Guid followingId)
    {
        if (followerId == followingId)
        {
            throw new DomainException("CANNOT_FOLLOW_SELF", "Bạn không thể tự theo dõi chính mình.");
        }

        FollowerId = followerId;
        FollowingId = followingId;
    }

    public Guid FollowerId { get; private set; }
    public User Follower { get; private set; } = null!;
    public Guid FollowingId { get; private set; }
    public User Following { get; private set; } = null!;
}
