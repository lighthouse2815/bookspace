using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
    UserSummary GetMe(Guid userId);
}

public interface IUserService
{
    UserProfile Get(Guid userId, Guid? viewerId);
    Task<PageResult<UserDiscoveryItem>> SearchAsync(
        string? search,
        Guid? viewerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<PageResult<UserDiscoveryItem>> GetSuggestionsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<UserProfile> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task FollowAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken);
    Task UnfollowAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken);
    PageResult<UserSummary> GetFollowers(Guid userId, int page, int pageSize);
    PageResult<UserSummary> GetFollowing(Guid userId, int page, int pageSize);
}

public interface ICatalogService
{
    PageResult<BookSummary> GetBooks(
        string? search,
        Guid? authorId,
        Guid? categoryId,
        string? sort,
        Guid? viewerId,
        int page,
        int pageSize);
    BookDetail GetBook(Guid bookId, Guid? viewerId);
    PageResult<AuthorDto> GetAuthors(int page, int pageSize);
    PageResult<CategoryDto> GetCategories(int page, int pageSize);
    Task<BookDetail> CreateBookAsync(SaveBookRequest request, CancellationToken cancellationToken);
    Task<BookDetail> UpdateBookAsync(Guid id, SaveBookRequest request, CancellationToken cancellationToken);
    Task DeleteBookAsync(Guid id, CancellationToken cancellationToken);
    Task<AuthorDto> CreateAuthorAsync(SaveAuthorRequest request, CancellationToken cancellationToken);
    Task<AuthorDto> UpdateAuthorAsync(Guid id, SaveAuthorRequest request, CancellationToken cancellationToken);
    Task DeleteAuthorAsync(Guid id, CancellationToken cancellationToken);
    Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken);
    Task<CategoryDto> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken);
    Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken);
}

public interface IReadingService
{
    PageResult<LibraryItemDto> GetLibrary(Guid userId, LibraryStatus? status, int page, int pageSize);
    Task<LibraryItemDto> AddLibraryItemAsync(Guid userId, AddLibraryItemRequest request, CancellationToken cancellationToken);
    Task<LibraryItemDto> UpdateLibraryItemAsync(Guid userId, Guid itemId, UpdateLibraryItemRequest request, CancellationToken cancellationToken);
    Task<LibraryItemDto> UpdateProgressAsync(Guid userId, Guid itemId, UpdateProgressRequest request, CancellationToken cancellationToken);
    Task RemoveLibraryItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);
    PageResult<ReadingSessionDto> GetSessions(Guid userId, int page, int pageSize);
    Task<ReadingSessionDto> AddSessionAsync(Guid userId, CreateReadingSessionRequest request, CancellationToken cancellationToken);
}

public interface ICommunityService
{
    ReviewDto GetReview(Guid reviewId, Guid? viewerId);
    PageResult<ReviewDto> GetBookReviews(Guid bookId, Guid? viewerId, int page, int pageSize);
    Task<ReviewDto> CreateReviewAsync(Guid userId, CreateReviewRequest request, CancellationToken cancellationToken);
    Task<ReviewDto> UpdateReviewAsync(Guid userId, bool isAdmin, Guid reviewId, SaveReviewRequest request, CancellationToken cancellationToken);
    Task DeleteReviewAsync(Guid userId, bool isAdmin, Guid reviewId, CancellationToken cancellationToken);
    Task LikeReviewAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken);
    Task UnlikeReviewAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken);
    PageResult<ReviewCommentDto> GetComments(Guid reviewId, int page, int pageSize);
    Task<ReviewCommentDto> AddCommentAsync(Guid userId, Guid reviewId, CreateCommentRequest request, CancellationToken cancellationToken);
    Task DeleteCommentAsync(Guid userId, bool isAdmin, Guid commentId, CancellationToken cancellationToken);
    PageResult<FeedItem> GetFeed(Guid userId, int page, int pageSize);
}

public interface IClubService
{
    PageResult<ClubSummary> GetClubs(Guid? viewerId, string? search, int page, int pageSize);
    ClubSummary GetClub(Guid clubId, Guid? viewerId);
    Task<ClubSummary> CreateAsync(Guid ownerId, CreateClubRequest request, CancellationToken cancellationToken);
    Task<ClubSummary> UpdateAsync(Guid ownerId, Guid clubId, UpdateClubRequest request, CancellationToken cancellationToken);
    Task JoinAsync(Guid userId, Guid clubId, CancellationToken cancellationToken);
    Task LeaveAsync(Guid userId, Guid clubId, CancellationToken cancellationToken);
    PageResult<ClubMemberDto> GetMembers(Guid clubId, Guid? viewerId, int page, int pageSize);
    Task<ClubMemberDto> UpdateMemberRoleAsync(
        Guid ownerId,
        Guid clubId,
        Guid memberUserId,
        UpdateClubMemberRoleRequest request,
        CancellationToken cancellationToken);
    Task RemoveMemberAsync(
        Guid actorId,
        Guid clubId,
        Guid memberUserId,
        CancellationToken cancellationToken);
    Task<ClubInvitationDto> InviteAsync(
        Guid actorId,
        Guid clubId,
        InviteClubMemberRequest request,
        CancellationToken cancellationToken);
    Task<PageResult<ClubInvitationDto>> GetClubInvitationsAsync(
        Guid actorId,
        Guid clubId,
        ClubInvitationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<PageResult<ClubInvitationDto>> GetMyInvitationsAsync(
        Guid userId,
        ClubInvitationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<ClubMemberDto> AcceptInvitationAsync(
        Guid userId,
        Guid invitationId,
        CancellationToken cancellationToken);
    Task<ClubInvitationDto> DeclineInvitationAsync(
        Guid userId,
        Guid invitationId,
        CancellationToken cancellationToken);
    Task<ClubInvitationDto> RevokeInvitationAsync(
        Guid actorId,
        Guid clubId,
        Guid invitationId,
        CancellationToken cancellationToken);
    Task<ClubSummary> SetCurrentBookAsync(
        Guid actorId,
        Guid clubId,
        SetClubCurrentBookRequest request,
        CancellationToken cancellationToken);
    Task<ClubSummary> ClearCurrentBookAsync(
        Guid actorId,
        Guid clubId,
        CancellationToken cancellationToken);
    PageResult<ClubPostDto> GetPosts(Guid clubId, Guid? viewerId, int page, int pageSize);
    Task<ClubPostDto> AddPostAsync(Guid userId, Guid clubId, CreateClubPostRequest request, CancellationToken cancellationToken);
    Task DeletePostAsync(Guid userId, bool isAdmin, Guid postId, CancellationToken cancellationToken);
    PageResult<ClubPostCommentDto> GetPostComments(Guid postId, Guid? viewerId, int page, int pageSize);
    Task<ClubPostCommentDto> AddPostCommentAsync(Guid userId, Guid postId, CreateCommentRequest request, CancellationToken cancellationToken);
    Task DeletePostCommentAsync(Guid userId, bool isAdmin, Guid commentId, CancellationToken cancellationToken);
}

public interface IChallengeService
{
    Task<PageResult<ChallengeDto>> GetChallengesAsync(Guid? userId, int page, int pageSize, CancellationToken cancellationToken);
    PageResult<ChallengeDto> GetAdminChallenges(int page, int pageSize);
    Task<PageResult<ChallengeDto>> GetMineAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<ChallengeDto> GetPublicAsync(Guid challengeId, Guid? userId, CancellationToken cancellationToken);
    Task SyncProgressAsync(Guid userId, CancellationToken cancellationToken);
    Task<ChallengeDto> JoinAsync(Guid userId, Guid challengeId, CancellationToken cancellationToken);
    Task<ChallengeDto> LeaveAsync(Guid userId, Guid challengeId, CancellationToken cancellationToken);
    Task<ChallengeDto> CreateAsync(Guid adminId, SaveChallengeRequest request, CancellationToken cancellationToken);
    Task<ChallengeDto> UpdateAsync(Guid challengeId, SaveChallengeRequest request, CancellationToken cancellationToken);
    Task<ChallengeDto> PublishAsync(Guid challengeId, PublishChallengeRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid challengeId, CancellationToken cancellationToken);
}

public interface INotificationService
{
    NotificationDto GetOne(Guid userId, Guid notificationId);
    PageResult<NotificationDto> Get(Guid userId, bool? unreadOnly, int page, int pageSize);
    int GetUnreadCount(Guid userId);
    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);
    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IExternalCatalogService
{
    Task<ExternalBookSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken);
}
