using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Contracts;

public sealed record UserSummary(
    Guid Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
    bool FollowsYou,
    int MutualFollowCount,
    ProfilePrivacyDto Privacy,
    DateTimeOffset JoinedAt,
    bool IsMuted = false);

public sealed record ProfilePrivacyDto(
    bool IsReadingShelfPublic,
    bool IsReadingActivityPublic);

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
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [MaxLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    string Email,
    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [MinLength(8, ErrorMessage = "Mật khẩu cần ít nhất 8 ký tự.")]
    [MaxLength(100, ErrorMessage = "Mật khẩu không được vượt quá 100 ký tự.")]
    string Password,
    [Required(ErrorMessage = "Tên hiển thị không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên hiển thị không được vượt quá 100 ký tự.")]
    string DisplayName);

public sealed record LoginRequest(
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [MaxLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    string Email,
    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [MaxLength(100, ErrorMessage = "Mật khẩu không được vượt quá 100 ký tự.")]
    string Password);

public sealed record RequestPasswordResetRequest(
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [MaxLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    string Email);

public sealed record ResetPasswordRequest(
    [Required(ErrorMessage = "Mã đặt lại mật khẩu không được để trống.")]
    [MaxLength(500, ErrorMessage = "Mã đặt lại mật khẩu không hợp lệ.")]
    string Token,
    [Required(ErrorMessage = "Mật khẩu mới không được để trống.")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới cần ít nhất 8 ký tự.")]
    [MaxLength(100, ErrorMessage = "Mật khẩu mới không được vượt quá 100 ký tự.")]
    string Password);

public sealed record RefreshRequest(
    [Required(ErrorMessage = "Refresh token không được để trống.")]
    [MaxLength(500, ErrorMessage = "Refresh token không hợp lệ.")]
    string RefreshToken);
public sealed record LogoutRequest(
    [MaxLength(500, ErrorMessage = "Refresh token không hợp lệ.")]
    string? RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserSummary User);

public sealed record UpdateProfileRequest(
    [Required(ErrorMessage = "Tên hiển thị không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên hiển thị không được vượt quá 100 ký tự.")]
    string DisplayName,
    [MaxLength(500, ErrorMessage = "Giới thiệu không được vượt quá 500 ký tự.")]
    string? Bio,
    [Url(ErrorMessage = "Ảnh đại diện phải là một URL hợp lệ.")]
    [MaxLength(1000, ErrorMessage = "Ảnh đại diện không được vượt quá 1.000 ký tự.")]
    string? AvatarUrl);

public sealed record UpdateProfilePrivacyRequest(
    bool IsReadingShelfPublic,
    bool IsReadingActivityPublic);

public sealed record OnboardingStateDto(
    OnboardingStatus Status,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<Guid> PreferredCategoryIds,
    IReadOnlyList<Guid> ReferenceBookIds);

public sealed record UpdateOnboardingPreferencesRequest(
    [Required(ErrorMessage = "Danh sách thể loại yêu thích là bắt buộc.")]
    IReadOnlyList<Guid>? PreferredCategoryIds,
    [Required(ErrorMessage = "Danh sách sách tham chiếu là bắt buộc.")]
    IReadOnlyList<Guid>? ReferenceBookIds);

public sealed record UserSafetyEntryDto(
    UserSummary User,
    bool IsBlocked,
    bool IsMuted,
    DateTimeOffset? BlockedAt,
    DateTimeOffset? MutedAt);

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

public sealed record CatalogFollowingDto(
    IReadOnlyList<AuthorDto> Authors,
    IReadOnlyList<CategoryDto> Categories);

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

public sealed record BookRecommendationDto(
    BookSummary Book,
    string ReasonCode,
    string ReasonText);

public sealed record SaveAuthorRequest(
    [Required(ErrorMessage = "Tên tác giả không được để trống.")]
    [MaxLength(200, ErrorMessage = "Tên tác giả không được vượt quá 200 ký tự.")]
    string Name,
    [MaxLength(2000, ErrorMessage = "Tiểu sử tác giả không được vượt quá 2.000 ký tự.")]
    string? Biography,
    [Url(ErrorMessage = "Ảnh tác giả phải là một URL hợp lệ.")]
    [MaxLength(1000, ErrorMessage = "Ảnh tác giả không được vượt quá 1.000 ký tự.")]
    string? AvatarUrl);

public sealed record SaveCategoryRequest(
    [Required(ErrorMessage = "Tên thể loại không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên thể loại không được vượt quá 100 ký tự.")]
    string Name,
    [MaxLength(500, ErrorMessage = "Mô tả thể loại không được vượt quá 500 ký tự.")]
    string? Description);

public sealed record SaveBookRequest(
    [Required(ErrorMessage = "Tên sách không được để trống.")]
    [MaxLength(300, ErrorMessage = "Tên sách không được vượt quá 300 ký tự.")]
    string Title,
    [MaxLength(5000, ErrorMessage = "Mô tả sách không được vượt quá 5.000 ký tự.")]
    string? Description,
    [MaxLength(20, ErrorMessage = "ISBN không được vượt quá 20 ký tự.")]
    string? Isbn,
    [Url(ErrorMessage = "Ảnh bìa phải là một URL hợp lệ.")]
    [MaxLength(1000, ErrorMessage = "Ảnh bìa không được vượt quá 1.000 ký tự.")]
    string? CoverImageUrl,
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải từ 1 trở lên.")]
    int? PageCount,
    [Range(1000, 2200, ErrorMessage = "Năm xuất bản phải từ 1000 đến 2200.")]
    int? PublishedYear,
    [MaxLength(20, ErrorMessage = "Ngôn ngữ không được vượt quá 20 ký tự.")]
    string? Language,
    Guid AuthorId,
    IReadOnlyList<Guid>? CategoryIds);

public sealed record ImportExternalBookRequest(
    [Required(ErrorMessage = "Nhà cung cấp không được để trống.")]
    [MaxLength(50, ErrorMessage = "Tên nhà cung cấp không được vượt quá 50 ký tự.")]
    string Provider,
    [Required(ErrorMessage = "Mã sách ngoài không được để trống.")]
    [MaxLength(200, ErrorMessage = "Mã sách ngoài không được vượt quá 200 ký tự.")]
    string ExternalId,
    Guid? AuthorId,
    [MaxLength(200, ErrorMessage = "Tên tác giả không được vượt quá 200 ký tự.")]
    string? AuthorName,
    IReadOnlyList<Guid>? CategoryIds,
    IReadOnlyList<string>? CategoryNames,
    [MaxLength(5000, ErrorMessage = "Mô tả sách không được vượt quá 5.000 ký tự.")]
    string? Description,
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải từ 1 trở lên.")]
    int? PageCount,
    [Range(1000, 2200, ErrorMessage = "Năm xuất bản phải từ 1000 đến 2200.")]
    int? PublishedYear,
    [MaxLength(20, ErrorMessage = "Ngôn ngữ không được vượt quá 20 ký tự.")]
    string? Language);

public sealed record ExternalBookImportResult(
    string Status,
    string Provider,
    string ExternalId,
    BookDetail Book);

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

public sealed record PublicLibraryItemDto(
    Guid BookId,
    BookSummary Book,
    LibraryStatus Shelf,
    int ProgressPercent,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset UpdatedAt);

public sealed record AddLibraryItemRequest(Guid BookId, LibraryStatus Shelf);
public sealed record UpdateLibraryItemRequest(
    LibraryStatus? Shelf,
    [Range(0, int.MaxValue, ErrorMessage = "Trang hiện tại phải từ 0 trở lên.")]
    int? CurrentPage,
    [Range(0, 100, ErrorMessage = "Tiến độ phần trăm phải từ 0 đến 100.")]
    int? ProgressPercent);
public sealed record UpdateProgressRequest(
    [Range(0, int.MaxValue, ErrorMessage = "Trang hiện tại phải từ 0 trở lên.")]
    int CurrentPage);

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
    [Range(1, 1440, ErrorMessage = "Thời lượng đọc phải từ 1 đến 1.440 phút.")]
    int DurationMinutes,
    [Range(1, int.MaxValue, ErrorMessage = "Số trang đã đọc phải từ 1 trở lên.")]
    int PagesRead,
    [MaxLength(1000, ErrorMessage = "Ghi chú phiên đọc không được vượt quá 1.000 ký tự.")]
    string? Note);

public sealed record CorrectReadingSessionRequest(
    DateTimeOffset StartedAt,
    [Range(1, 1440, ErrorMessage = "Thời lượng đọc phải từ 1 đến 1.440 phút.")]
    int DurationMinutes,
    [Range(1, int.MaxValue, ErrorMessage = "Số trang đã đọc phải từ 1 trở lên.")]
    int PagesRead,
    [MaxLength(1000, ErrorMessage = "Ghi chú phiên đọc không được vượt quá 1.000 ký tự.")]
    string? Note);

public sealed record ActiveReadingSessionDto(
    Guid Id,
    Guid BookId,
    BookSummary? Book,
    ActiveReadingSessionStatus Status,
    int StartPage,
    DateTimeOffset StartedAt,
    long ElapsedSeconds,
    DateTimeOffset UpdatedAt);

public sealed record StartActiveReadingSessionRequest(Guid BookId);

public sealed record FinishActiveReadingSessionRequest(
    [Range(0, int.MaxValue, ErrorMessage = "Trang kết thúc phải từ 0 trở lên.")]
    int EndingPage,
    [MaxLength(1000, ErrorMessage = "Ghi chú phiên đọc không được vượt quá 1.000 ký tự.")]
    string? Note);

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
    [Range(1, 5, ErrorMessage = "Điểm đánh giá phải từ 1 đến 5.")]
    int Rating,
    [Required(ErrorMessage = "Nội dung đánh giá không được để trống.")]
    [MaxLength(5000, ErrorMessage = "Nội dung đánh giá không được vượt quá 5.000 ký tự.")]
    string Content,
    bool ContainsSpoilers);

public sealed record CreateReviewRequest(
    Guid BookId,
    [Range(1, 5, ErrorMessage = "Điểm đánh giá phải từ 1 đến 5.")]
    int Rating,
    [Required(ErrorMessage = "Nội dung đánh giá không được để trống.")]
    [MaxLength(5000, ErrorMessage = "Nội dung đánh giá không được vượt quá 5.000 ký tự.")]
    string Content,
    bool ContainsSpoilers);

public sealed record ReviewCommentDto(
    Guid Id,
    Guid ReviewId,
    UserSummary User,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record CreateCommentRequest(
    [Required(ErrorMessage = "Nội dung bình luận không được để trống.")]
    [MaxLength(2000, ErrorMessage = "Nội dung bình luận không được vượt quá 2.000 ký tự.")]
    string Content);

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
    [Required(ErrorMessage = "Nội dung bài viết không được để trống.")]
    [MaxLength(10000, ErrorMessage = "Nội dung bài viết không được vượt quá 10.000 ký tự.")]
    string Content);

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
    [Required(ErrorMessage = "Tên thử thách không được để trống.")]
    [MaxLength(200, ErrorMessage = "Tên thử thách không được vượt quá 200 ký tự.")]
    string Title,
    [MaxLength(2000, ErrorMessage = "Mô tả thử thách không được vượt quá 2.000 ký tự.")]
    string? Description,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    [Range(1, 1000, ErrorMessage = "Mục tiêu số sách phải từ 1 đến 1.000.")]
    int GoalBooks,
    [Url(ErrorMessage = "Ảnh bìa thử thách phải là một URL hợp lệ.")]
    [MaxLength(1000, ErrorMessage = "Ảnh bìa thử thách không được vượt quá 1.000 ký tự.")]
    string? CoverImageUrl);

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

public sealed record NotificationPreferencesDto(
    bool IsFollowNotificationEnabled,
    bool IsCatalogNotificationEnabled,
    bool IsReviewNotificationEnabled,
    bool IsClubNotificationEnabled,
    bool IsChallengeNotificationEnabled,
    bool IsDirectMessageNotificationEnabled);

public sealed record CreateContentReportRequest(
    ContentReportTargetType TargetType,
    Guid TargetId,
    ContentReportReason Reason,
    [MaxLength(1000, ErrorMessage = "Mô tả báo cáo không được vượt quá 1000 ký tự.")]
    string? Details);

public sealed record ResolveContentReportRequest(
    ContentReportStatus Status,
    ModerationAction Action,
    [MaxLength(1000, ErrorMessage = "Ghi chú xử lý không được vượt quá 1000 ký tự.")]
    string? ResolutionNote);

public sealed record ContentReportDto(
    Guid Id,
    UserSummary Reporter,
    ContentReportTargetType TargetType,
    Guid TargetId,
    UserSummary TargetOwner,
    ContentReportReason Reason,
    string? Details,
    string TargetPreview,
    string TargetLink,
    ContentReportStatus Status,
    ModerationAction Action,
    UserSummary? Moderator,
    string? ResolutionNote,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt);

public sealed record UpdateNotificationPreferencesRequest(
    bool IsFollowNotificationEnabled,
    bool IsCatalogNotificationEnabled,
    bool IsReviewNotificationEnabled,
    bool IsClubNotificationEnabled,
    bool IsChallengeNotificationEnabled,
    bool IsDirectMessageNotificationEnabled);

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
