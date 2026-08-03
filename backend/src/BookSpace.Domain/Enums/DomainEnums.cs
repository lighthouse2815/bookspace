namespace BookSpace.Domain.Enums;

public enum UserRole
{
    USER,
    ADMIN
}

public enum OnboardingStatus
{
    PENDING,
    COMPLETED,
    SKIPPED
}

public enum LibraryStatus
{
    WANT_TO_READ,
    READING,
    READ
}

public enum ActiveReadingSessionStatus
{
    RUNNING,
    PAUSED
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

public enum ContentReportTargetType
{
    USER,
    REVIEW,
    REVIEW_COMMENT,
    CLUB_POST,
    CLUB_POST_COMMENT,
    CLUB_CHAT_MESSAGE
}

public enum ContentReportReason
{
    SPAM,
    HARASSMENT,
    HATEFUL_CONTENT,
    INAPPROPRIATE_CONTENT,
    MISINFORMATION,
    OTHER
}

public enum ContentReportStatus
{
    PENDING,
    RESOLVED,
    DISMISSED
}

public enum ModerationAction
{
    NONE,
    CONTENT_REMOVED,
    USER_LOCKED
}
