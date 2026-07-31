using System.ComponentModel.DataAnnotations;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Contracts;

public sealed record UserSummary(
    Guid Id,
    string? Email,
    string DisplayName,
    string? AvatarUrl,
    UserRole Role);

public sealed record UserProfile(
    Guid Id,
    string? Email,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    UserRole Role,
    int FollowerCount,
    int FollowingCount,
    int BooksReadCount,
    bool IsFollowing,
    DateTimeOffset JoinedAt);

public sealed record UserDiscoveryItem(
    Guid Id,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    int FollowerCount,
    int BooksReadCount,
    bool IsFollowing,
    bool FollowsYou,
    int MutualFollowCount,
    string Reason,
    string ReasonText);

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8), MaxLength(100)] string Password,
    [Required, MaxLength(100)] string DisplayName);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record RefreshRequest([Required] string RefreshToken);
public sealed record LogoutRequest(string? RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserSummary User);

public sealed record UpdateProfileRequest(
    [Required, MaxLength(100)] string DisplayName,
    [MaxLength(500)] string? Bio,
    [Url, MaxLength(1000)] string? AvatarUrl);

public sealed record AuthorDto(
    Guid Id,
    string Name,
    string? Biography,
    string? AvatarUrl,
    int BookCount);

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    int BookCount);

public sealed record BookSummary(
    Guid Id,
    string Title,
    string? Description,
    string? Isbn,
    string? CoverImageUrl,
    int? PageCount,
    int? PublishedYear,
    string? Publisher,
    string Language,
    double AverageRating,
    int ReviewCount,
    AuthorDto? Author,
    Guid? AuthorId,
    IReadOnlyList<CategoryDto> Categories,
    LibraryStatus? Shelf);

public sealed record BookDetail(
    Guid Id,
    string Title,
    string? Description,
    string? Isbn,
    string? CoverImageUrl,
    int PageCount,
    int? PublishedYear,
    string? Publisher,
    string Language,
    AuthorDto? Author,
    Guid? AuthorId,
    IReadOnlyList<CategoryDto> Categories,
    double AverageRating,
    int ReviewCount,
    LibraryStatus? Shelf,
    DateTimeOffset CreatedAt);

public sealed record SaveAuthorRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(2000)] string? Biography,
    [Url, MaxLength(1000)] string? AvatarUrl);

public sealed record SaveCategoryRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(500)] string? Description);

public sealed record SaveBookRequest(
    [Required, MaxLength(300)] string Title,
    [MaxLength(5000)] string? Description,
    [MaxLength(20)] string? Isbn,
    [Url, MaxLength(1000)] string? CoverImageUrl,
    [Range(1, int.MaxValue)] int? PageCount,
    [Range(1000, 2200)] int? PublishedYear,
    [MaxLength(20)] string? Language,
    Guid AuthorId,
    IReadOnlyList<Guid>? CategoryIds);

public sealed record LibraryItemDto(
    Guid Id,
    Guid UserId,
    Guid BookId,
    BookSummary Book,
    LibraryStatus Shelf,
    int CurrentPage,
    int ProgressPercent,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset UpdatedAt);

public sealed record AddLibraryItemRequest(Guid BookId, LibraryStatus Shelf);
public sealed record UpdateLibraryItemRequest(
    LibraryStatus? Shelf,
    [Range(0, int.MaxValue)] int? CurrentPage,
    [Range(0, 100)] int? ProgressPercent);
public sealed record UpdateProgressRequest([Range(0, int.MaxValue)] int CurrentPage);

public sealed record ReadingSessionDto(
    Guid Id,
    Guid BookId,
    BookSummary? Book,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int DurationMinutes,
    int PagesRead,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record CreateReadingSessionRequest(
    Guid BookId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    [Range(1, 1440)] int DurationMinutes,
    [Range(1, int.MaxValue)] int PagesRead,
    [MaxLength(1000)] string? Note);

public sealed record ReviewDto(
    Guid Id,
    Guid BookId,
    BookSummary? Book,
    UserSummary User,
    int Rating,
    string Content,
    bool ContainsSpoilers,
    int LikeCount,
    int CommentCount,
    bool LikedByCurrentUser,
    IReadOnlyList<ReviewCommentDto>? Comments,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record SaveReviewRequest(
    [Range(1, 5)] int Rating,
    [Required, MaxLength(5000)] string Content,
    bool ContainsSpoilers);

public sealed record CreateReviewRequest(
    Guid BookId,
    [Range(1, 5)] int Rating,
    [Required, MaxLength(5000)] string Content,
    bool ContainsSpoilers);

public sealed record ReviewCommentDto(
    Guid Id,
    Guid ReviewId,
    UserSummary User,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record CreateCommentRequest([Required, MaxLength(2000)] string Content);

public sealed record FeedItem(
    Guid Id,
    string Type,
    UserSummary Actor,
    ReviewDto? Review,
    BookSummary? Book,
    ClubSummary? Club,
    ChallengeDto? Challenge,
    string? Content,
    int? ProgressPercent,
    DateTimeOffset CreatedAt);

public sealed record ClubSummary(
    Guid Id,
    string Name,
    string? Description,
    string? CoverImageUrl,
    int MemberCount,
    bool IsPrivate,
    bool IsJoined,
    BookSummary? CurrentBook,
    UserSummary? Owner,
    IReadOnlyList<ClubPostDto>? Posts,
    DateTimeOffset CreatedAt,
    ClubMemberRole? ViewerRole,
    ClubPermissionsDto Permissions);

public sealed record ClubPermissionsDto(
    bool CanEdit,
    bool CanInvite,
    bool CanManageMembers,
    bool CanManageCurrentBook,
    bool CanLeave);

public sealed record ClubDetail(
    ClubSummary Club,
    IReadOnlyList<ClubMemberDto> Members,
    IReadOnlyList<ClubPostDto> RecentPosts);

public sealed record ClubMemberDto(
    Guid Id,
    UserSummary User,
    ClubMemberRole Role,
    DateTimeOffset JoinedAt);

public sealed record CreateClubRequest(
    [Required(ErrorMessage = "Tên câu lạc bộ không được để trống.")]
    [MaxLength(150, ErrorMessage = "Tên câu lạc bộ không được vượt quá 150 ký tự.")]
    string Name,
    [MaxLength(2000, ErrorMessage = "Mô tả câu lạc bộ không được vượt quá 2000 ký tự.")]
    string? Description,
    [Url(ErrorMessage = "Ảnh bìa câu lạc bộ phải là một URL hợp lệ.")]
    [MaxLength(1000, ErrorMessage = "Ảnh bìa câu lạc bộ không được vượt quá 1000 ký tự.")]
    string? CoverImageUrl,
    bool IsPrivate);

public sealed record UpdateClubRequest(
    [Required(ErrorMessage = "Tên câu lạc bộ không được để trống.")]
    [MaxLength(150, ErrorMessage = "Tên câu lạc bộ không được vượt quá 150 ký tự.")]
    string Name,
    [MaxLength(2000, ErrorMessage = "Mô tả câu lạc bộ không được vượt quá 2000 ký tự.")]
    string? Description,
    [Url(ErrorMessage = "Ảnh bìa câu lạc bộ phải là một URL hợp lệ.")]
    [MaxLength(1000, ErrorMessage = "Ảnh bìa câu lạc bộ không được vượt quá 1000 ký tự.")]
    string? CoverImageUrl,
    bool IsPrivate);

public sealed record InviteClubMemberRequest(
    [Required(ErrorMessage = "Email người được mời không được để trống.")]
    [EmailAddress(ErrorMessage = "Email người được mời không hợp lệ.")]
    [MaxLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    string Email);

public sealed record UpdateClubMemberRoleRequest(ClubMemberRole Role);

public sealed record SetClubCurrentBookRequest(
    [Required(ErrorMessage = "Mã sách không được để trống.")]
    Guid BookId);

public sealed record ClubInvitationDto(
    Guid Id,
    ClubSummary Club,
    UserSummary Inviter,
    UserSummary InvitedUser,
    ClubInvitationStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt,
    DateTimeOffset CreatedAt);

public sealed record ClubPostDto(
    Guid Id,
    Guid ClubId,
    UserSummary Author,
    string Content,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt);

public sealed record CreateClubPostRequest(
    [Required, MaxLength(10000)] string Content);

public sealed record ClubPostCommentDto(
    Guid Id,
    Guid PostId,
    UserSummary Author,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record ChallengeDto(
    Guid Id,
    string Title,
    string? Description,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    int GoalBooks,
    int CurrentBooks,
    int ParticipantCount,
    bool IsJoined,
    string? CoverImageUrl,
    bool IsPublished,
    DateTimeOffset? CompletedAt);

public sealed record SaveChallengeRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(2000)] string? Description,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    [Range(1, 1000)] int GoalBooks,
    [Url, MaxLength(1000)] string? CoverImageUrl);

public sealed record PublishChallengeRequest(bool IsPublished);

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    string? Link,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record DashboardDto(
    int BooksRead,
    int PagesRead,
    int ReadingMinutes,
    int CurrentStreak,
    IReadOnlyList<WeeklyMetricDto> WeeklyPages,
    IReadOnlyList<LibraryItemDto> CurrentlyReading,
    IReadOnlyList<ReadingSessionDto> RecentSessions,
    IReadOnlyList<ChallengeDto> ActiveChallenges);

public sealed record WeeklyMetricDto(string Label, int Value);
