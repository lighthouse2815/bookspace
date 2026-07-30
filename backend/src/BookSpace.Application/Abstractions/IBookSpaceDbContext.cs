using BookSpace.Domain.Entities;

namespace BookSpace.Application.Abstractions;

public interface IBookSpaceDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<RefreshToken> RefreshTokens { get; }
    IQueryable<Follow> Follows { get; }
    IQueryable<Author> Authors { get; }
    IQueryable<Category> Categories { get; }
    IQueryable<Book> Books { get; }
    IQueryable<BookAuthor> BookAuthors { get; }
    IQueryable<BookCategory> BookCategories { get; }
    IQueryable<LibraryItem> LibraryItems { get; }
    IQueryable<ReadingSession> ReadingSessions { get; }
    IQueryable<Review> Reviews { get; }
    IQueryable<ReviewComment> ReviewComments { get; }
    IQueryable<ReviewLike> ReviewLikes { get; }
    IQueryable<BookClub> BookClubs { get; }
    IQueryable<BookClubMember> BookClubMembers { get; }
    IQueryable<ClubInvitation> ClubInvitations { get; }
    IQueryable<ClubPost> ClubPosts { get; }
    IQueryable<ClubPostComment> ClubPostComments { get; }
    IQueryable<ClubReadingSprint> ClubReadingSprints { get; }
    IQueryable<ClubReadingSprintParticipant> ClubReadingSprintParticipants { get; }
    IQueryable<ClubReadingSprintCheckIn> ClubReadingSprintCheckIns { get; }
    IQueryable<ClubReadingSprintMilestone> ClubReadingSprintMilestones { get; }
    IQueryable<ClubReadingSprintMilestone> ClubReadingSprintMilestonesIncludingDeleted { get; }
    IQueryable<ClubReadingSprintMilestoneResponse> ClubReadingSprintMilestoneResponses { get; }
    IQueryable<ReadingChallenge> ReadingChallenges { get; }
    IQueryable<ChallengeParticipation> ChallengeParticipations { get; }
    IQueryable<Notification> Notifications { get; }

    void Add<T>(T entity) where T : class;
    void AddRange<T>(IEnumerable<T> entities) where T : class;
    void Remove<T>(T entity) where T : class;
    void RemoveRange<T>(IEnumerable<T> entities) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public sealed record IssuedTokens(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    string RefreshTokenHash,
    DateTimeOffset RefreshExpiresAt);

public interface ITokenIssuer
{
    IssuedTokens Issue(BookSpace.Domain.Entities.User user);
    string HashRefreshToken(string refreshToken);
}

public sealed record ExternalBookResult(
    string ExternalId,
    string Title,
    IReadOnlyList<string> Authors,
    string? CoverImageUrl,
    string? Isbn,
    decimal? Price,
    string? PurchaseUrl);

public sealed record ExternalBookSearchResult(
    bool Available,
    string Provider,
    string Message,
    IReadOnlyList<ExternalBookResult> Items);

public interface IExternalBookProvider
{
    Task<ExternalBookSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken);
}
