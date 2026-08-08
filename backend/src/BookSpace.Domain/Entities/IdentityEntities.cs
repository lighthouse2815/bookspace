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
    public int AuthVersion { get; private set; }
    public OnboardingStatus OnboardingStatus { get; private set; } = OnboardingStatus.PENDING;
    public DateTimeOffset? OnboardingFinishedAt { get; private set; }
    public bool IsLocked { get; private set; }
    public bool IsReadingShelfPublic { get; private set; }
    public bool IsReadingActivityPublic { get; private set; }
    public bool IsFollowNotificationEnabled { get; private set; } = true;
    public bool IsCatalogNotificationEnabled { get; private set; } = true;
    public bool IsReviewNotificationEnabled { get; private set; } = true;
    public bool IsClubNotificationEnabled { get; private set; } = true;
    public bool IsChallengeNotificationEnabled { get; private set; } = true;
    public bool IsDirectMessageNotificationEnabled { get; private set; } = true;

    public ICollection<Follow> Followers { get; } = new List<Follow>();
    public ICollection<Follow> Following { get; } = new List<Follow>();
    public ICollection<UserPreferredCategory> PreferredCategories { get; } =
        new List<UserPreferredCategory>();
    public ICollection<UserReferenceBook> ReferenceBooks { get; } =
        new List<UserReferenceBook>();
    public ICollection<UserAuthorFollow> FollowedAuthors { get; } = new List<UserAuthorFollow>();
    public ICollection<UserCategoryFollow> FollowedCategories { get; } = new List<UserCategoryFollow>();

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
        NotificationType.CATALOG => IsCatalogNotificationEnabled,
        NotificationType.REVIEW_LIKE or NotificationType.COMMENT => IsReviewNotificationEnabled,
        NotificationType.CLUB => IsClubNotificationEnabled,
        NotificationType.CHALLENGE => IsChallengeNotificationEnabled,
        NotificationType.DIRECT_MESSAGE => IsDirectMessageNotificationEnabled,
        NotificationType.SYSTEM => true,
        _ => true
    };

    public void UpdateNotificationPreferences(
        bool isFollowNotificationEnabled,
        bool isCatalogNotificationEnabled,
        bool isReviewNotificationEnabled,
        bool isClubNotificationEnabled,
        bool isChallengeNotificationEnabled,
        bool isDirectMessageNotificationEnabled)
    {
        if (IsFollowNotificationEnabled == isFollowNotificationEnabled &&
            IsCatalogNotificationEnabled == isCatalogNotificationEnabled &&
            IsReviewNotificationEnabled == isReviewNotificationEnabled &&
            IsClubNotificationEnabled == isClubNotificationEnabled &&
            IsChallengeNotificationEnabled == isChallengeNotificationEnabled &&
            IsDirectMessageNotificationEnabled == isDirectMessageNotificationEnabled)
        {
            return;
        }

        IsFollowNotificationEnabled = isFollowNotificationEnabled;
        IsCatalogNotificationEnabled = isCatalogNotificationEnabled;
        IsReviewNotificationEnabled = isReviewNotificationEnabled;
        IsClubNotificationEnabled = isClubNotificationEnabled;
        IsChallengeNotificationEnabled = isChallengeNotificationEnabled;
        IsDirectMessageNotificationEnabled = isDirectMessageNotificationEnabled;
        Touch();
    }

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = Guard.Required(passwordHash, "Mật khẩu đã mã hóa", 500);
        AuthVersion = checked(AuthVersion + 1);
        Touch();
    }

    public void CompleteOnboarding()
    {
        if (OnboardingStatus == OnboardingStatus.COMPLETED)
        {
            return;
        }

        OnboardingStatus = OnboardingStatus.COMPLETED;
        OnboardingFinishedAt = UtcNowAtPersistencePrecision();
        Touch();
    }

    public void SkipOnboarding()
    {
        if (OnboardingStatus is OnboardingStatus.COMPLETED or OnboardingStatus.SKIPPED)
        {
            return;
        }

        OnboardingStatus = OnboardingStatus.SKIPPED;
        OnboardingFinishedAt = UtcNowAtPersistencePrecision();
        Touch();
    }

    private static DateTimeOffset UtcNowAtPersistencePrecision()
    {
        var now = DateTimeOffset.UtcNow;
        const long sqliteConverterPrecisionTicks = 1000;
        return new DateTimeOffset(
            now.Ticks - (now.Ticks % sqliteConverterPrecisionTicks),
            TimeSpan.Zero);
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

public sealed class PasswordResetToken : Entity
{
    private PasswordResetToken() { }

    public PasswordResetToken(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("PASSWORD_RESET_USER_REQUIRED", "Người dùng đặt lại mật khẩu không hợp lệ.");
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new DomainException("PASSWORD_RESET_EXPIRY_INVALID", "Thời hạn đặt lại mật khẩu phải ở tương lai.");
        }

        UserId = userId;
        TokenHash = Guard.Required(tokenHash, "Mã đặt lại mật khẩu", 200);
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset? InvalidatedAt { get; private set; }

    public bool IsActiveAt(DateTimeOffset now) =>
        !UsedAt.HasValue && !InvalidatedAt.HasValue && ExpiresAt > now;

    public void Use(DateTimeOffset now)
    {
        if (!IsActiveAt(now))
        {
            throw new DomainException(
                "PASSWORD_RESET_TOKEN_INVALID",
                "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        }

        UsedAt = now;
        Touch();
    }

    public void Invalidate(DateTimeOffset now)
    {
        if (UsedAt.HasValue || InvalidatedAt.HasValue)
        {
            return;
        }

        InvalidatedAt = now;
        Touch();
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

public sealed class UserPreferredCategory : Entity
{
    private UserPreferredCategory() { }

    public UserPreferredCategory(Guid userId, Guid categoryId)
    {
        UserId = userId;
        CategoryId = categoryId;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
}

public sealed class UserReferenceBook : Entity
{
    private UserReferenceBook() { }

    public UserReferenceBook(Guid userId, Guid bookId)
    {
        UserId = userId;
        BookId = bookId;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid BookId { get; private set; }
    public Book Book { get; private set; } = null!;
}
