namespace BookSpace.Domain.Enums;

public enum UserRole
{
    USER,
    ADMIN
}

public enum LibraryStatus
{
    WANT_TO_READ,
    READING,
    READ
}

public enum ClubMemberRole
{
    OWNER,
    MODERATOR,
    MEMBER
}

public enum ClubVisibility
{
    PUBLIC,
    PRIVATE
}

public enum ClubInvitationStatus
{
    PENDING,
    ACCEPTED,
    DECLINED,
    REVOKED,
    EXPIRED
}

public enum NotificationType
{
    FOLLOW,
    REVIEW_LIKE,
    COMMENT,
    CLUB,
    CHALLENGE,
    SYSTEM
}

public enum NotificationCategory
{
    FOLLOW,
    REVIEW,
    CLUB,
    CHALLENGE,
    SYSTEM
}
