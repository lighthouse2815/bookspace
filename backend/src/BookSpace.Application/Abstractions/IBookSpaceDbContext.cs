using BookSpace.Domain.Entities;

namespace BookSpace.Application.Abstractions;

public interface IBookSpaceDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<UserPreferredCategory> UserPreferredCategories =>
        Array.Empty<UserPreferredCategory>().AsQueryable();
    IQueryable<UserPreferredCategory> UserPreferredCategoriesIncludingDeleted =>
        UserPreferredCategories;
    IQueryable<UserReferenceBook> UserReferenceBooks =>
        Array.Empty<UserReferenceBook>().AsQueryable();
    IQueryable<UserReferenceBook> UserReferenceBooksIncludingDeleted =>
        UserReferenceBooks;
    IQueryable<RefreshToken> RefreshTokens { get; }
    IQueryable<Follow> Follows { get; }
    IQueryable<UserBlock> UserBlocks => Array.Empty<UserBlock>().AsQueryable();
    IQueryable<UserMute> UserMutes => Array.Empty<UserMute>().AsQueryable();
    IQueryable<Author> Authors { get; }
    IQueryable<Category> Categories { get; }
    IQueryable<Book> Books { get; }
    IQueryable<BookAuthor> BookAuthors { get; }
    IQueryable<BookCategory> BookCategories { get; }
    IQueryable<BookList> BookLists => Array.Empty<BookList>().AsQueryable();
    IQueryable<BookListItem> BookListItems => Array.Empty<BookListItem>().AsQueryable();
    IQueryable<BookListItem> BookListItemsIncludingDeleted => BookListItems;
    IQueryable<ExternalBookLink> ExternalBookLinks =>
        Array.Empty<ExternalBookLink>().AsQueryable();
    IQueryable<LibraryItem> LibraryItems { get; }
    IQueryable<LibraryItem> LibraryItemsIncludingDeleted { get; }
    IQueryable<ReadingSession> ReadingSessions { get; }
    IQueryable<ActiveReadingSession> ActiveReadingSessions { get; }
    IQueryable<Review> Reviews { get; }
    IQueryable<Review> ReviewsIncludingDeleted => Reviews;
    IQueryable<ReviewComment> ReviewComments { get; }
    IQueryable<ReviewComment> ReviewCommentsIncludingDeleted => ReviewComments;
    IQueryable<ReviewLike> ReviewLikes { get; }
    IQueryable<BookClub> BookClubs { get; }
    IQueryable<BookClubMember> BookClubMembers { get; }
    IQueryable<ClubInvitation> ClubInvitations { get; }
    IQueryable<ClubPost> ClubPosts { get; }
    IQueryable<ClubPost> ClubPostsIncludingDeleted => ClubPosts;
    IQueryable<ClubPostComment> ClubPostComments { get; }
    IQueryable<ClubPostComment> ClubPostCommentsIncludingDeleted => ClubPostComments;
    IQueryable<ClubChatMessage> ClubChatMessages { get; }
    IQueryable<ClubChatMessage> ClubChatMessagesIncludingDeleted => ClubChatMessages;
    IQueryable<ClubChatReadState> ClubChatReadStates { get; }
    IQueryable<Conversation> Conversations => Array.Empty<Conversation>().AsQueryable();
    IQueryable<DirectMessage> DirectMessages => Array.Empty<DirectMessage>().AsQueryable();
    IQueryable<DirectMessage> DirectMessagesIncludingDeleted => DirectMessages;
    IQueryable<DirectMessageReadState> DirectMessageReadStates =>
        Array.Empty<DirectMessageReadState>().AsQueryable();
    IQueryable<ClubReadingSprint> ClubReadingSprints { get; }
    IQueryable<ClubReadingSprintParticipant> ClubReadingSprintParticipants { get; }
    IQueryable<ClubReadingSprintCheckIn> ClubReadingSprintCheckIns { get; }
    IQueryable<ClubReadingSprintMilestone> ClubReadingSprintMilestones { get; }
    IQueryable<ClubReadingSprintMilestone> ClubReadingSprintMilestonesIncludingDeleted { get; }
    IQueryable<ClubReadingSprintMilestoneResponse> ClubReadingSprintMilestoneResponses { get; }
    IQueryable<ReadingChallenge> ReadingChallenges { get; }
    IQueryable<ChallengeParticipation> ChallengeParticipations { get; }
    IQueryable<Notification> Notifications { get; }
    IQueryable<ContentReport> ContentReports => Array.Empty<ContentReport>().AsQueryable();

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
    string? Description,
    int? PageCount,
    int? PublishedYear,
    string? Language,
    IReadOnlyList<string> Categories,
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
    Task<ExternalBookSearchResult> GetByIdAsync(string externalId, CancellationToken cancellationToken);
}
