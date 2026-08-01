using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Services;
using BookSpace.Domain.Common;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.UnitTests;

public sealed class FeedServiceTests
{
    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("0")]
    [InlineData("1")]
    public void Invalid_feed_type_is_rejected_with_the_contract_error(string type)
    {
        var service = new CommunityService(new FakeBookSpaceDbContext());

        var error = Assert.Throws<UseCaseException>(() =>
            service.GetFeed(Guid.NewGuid(), type, 1, 20));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal("INVALID_FEED_TYPE", error.Code);
        Assert.Equal(
            "Loại bảng tin không hợp lệ. Giá trị hỗ trợ: REVIEW, READING, CLUB, CHALLENGE.",
            error.Message);
    }

    [Fact]
    public void Reading_filter_is_case_insensitive_and_uses_real_reading_events()
    {
        var fixture = FeedFixture.Create();
        var startedOnly = new LibraryItem(
            fixture.Viewer.Id,
            fixture.Book.Id,
            LibraryStatus.READING);
        var sessionStartedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var session = new ReadingSession(
            fixture.Viewer.Id,
            fixture.Book.Id,
            sessionStartedAt,
            null,
            50,
            30,
            "Ghi chú riêng tư không được lộ ra feed.");
        var finished = new LibraryItem(
            fixture.Viewer.Id,
            fixture.Book.Id,
            LibraryStatus.READING);
        finished.UpdateProgress(fixture.Book.PageCount, fixture.Book.PageCount);
        var review = new Review(
            fixture.Viewer.Id,
            fixture.Book.Id,
            5,
            "Đánh giá không thuộc bộ lọc đọc sách.",
            false);
        fixture.Db.AddRange([startedOnly, finished]);
        fixture.Db.Add(session);
        fixture.Db.Add(review);

        var page = fixture.Service.GetFeed(fixture.Viewer.Id, "  reading  ", 1, 20);

        Assert.Equal(2, page.TotalItems);
        Assert.DoesNotContain(page.Items, item => item.Id == startedOnly.Id);
        Assert.DoesNotContain(page.Items, item => item.Id == review.Id);
        var progress = Assert.Single(page.Items, item => item.Type == "READING_PROGRESS");
        Assert.Equal(session.Id, progress.Id);
        Assert.Equal(sessionStartedAt, progress.CreatedAt);
        Assert.Equal(25, progress.ProgressPercent);
        Assert.Null(progress.Content);
        var completion = Assert.Single(page.Items, item => item.Type == "BOOK_FINISHED");
        Assert.Equal(finished.Id, completion.Id);
        Assert.Equal(100, completion.ProgressPercent);
        Assert.Equal(finished.FinishedAt, completion.CreatedAt);
    }

    [Fact]
    public void Feed_hides_other_users_private_reading_but_keeps_their_social_activity()
    {
        var fixture = FeedFixture.Create();
        var publicReader = new User(
            "public-reader@bookspace.local",
            "hash",
            "Độc giả công khai");
        publicReader.UpdatePublicReadingVisibility(false, true);
        var privateReader = new User(
            "private-reader@bookspace.local",
            "hash",
            "Độc giả riêng tư");
        fixture.Db.AddRange([publicReader, privateReader]);
        fixture.Db.AddRange([
            new Follow(fixture.Viewer.Id, publicReader.Id),
            new Follow(fixture.Viewer.Id, privateReader.Id)
        ]);
        var publicSession = Session(publicReader.Id, fixture.Book.Id, -3);
        var privateSession = Session(privateReader.Id, fixture.Book.Id, -2);
        var ownSession = Session(fixture.Viewer.Id, fixture.Book.Id, -1);
        fixture.Db.AddRange([publicSession, privateSession, ownSession]);
        var privateReview = new Review(
            privateReader.Id,
            fixture.Book.Id,
            4,
            "Đánh giá công khai vẫn xuất hiện.",
            false);
        fixture.Db.Add(privateReview);

        var page = fixture.Service.GetFeed(fixture.Viewer.Id, null, 1, 20);

        Assert.Contains(page.Items, item => item.Id == publicSession.Id);
        Assert.Contains(page.Items, item => item.Id == ownSession.Id);
        Assert.DoesNotContain(page.Items, item => item.Id == privateSession.Id);
        Assert.Contains(page.Items, item => item.Id == privateReview.Id && item.Type == "REVIEW");
        Assert.Equal(3, page.TotalItems);
    }

    [Fact]
    public void Stable_paging_orders_equal_timestamps_by_descending_id()
    {
        var fixture = FeedFixture.Create();
        var occurredAt = DateTimeOffset.UtcNow.AddDays(-1);
        var expectedIds = Enumerable.Range(1, 5)
            .Select(index => new Guid($"00000000-0000-0000-0000-{index:000000000000}"))
            .OrderByDescending(id => id)
            .ToList();

        foreach (var id in expectedIds)
        {
            var review = new Review(
                fixture.Viewer.Id,
                fixture.Book.Id,
                5,
                $"Đánh giá {id}",
                false);
            SetIdentity(review, id, occurredAt);
            fixture.Db.Add(review);
        }

        var first = fixture.Service.GetFeed(fixture.Viewer.Id, "REVIEW", 1, 2);
        var second = fixture.Service.GetFeed(fixture.Viewer.Id, "review", 2, 2);
        var third = fixture.Service.GetFeed(fixture.Viewer.Id, "REVIEW", 3, 2);

        Assert.Equal(5, first.TotalItems);
        Assert.Equal(expectedIds[..2], first.Items.Select(item => item.Id));
        Assert.Equal(expectedIds[2..4], second.Items.Select(item => item.Id));
        Assert.Equal(expectedIds[4..], third.Items.Select(item => item.Id));
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
    }

    [Fact]
    public void Public_profile_activity_contract_still_rejects_a_private_activity_tab()
    {
        var fixture = FeedFixture.Create();
        var privateReader = new User(
            "private-profile@bookspace.local",
            "hash",
            "Hồ sơ riêng tư");
        fixture.Db.Add(privateReader);

        var error = Assert.Throws<UseCaseException>(() =>
            fixture.Service.GetUserActivity(privateReader.Id, fixture.Viewer.Id, 1, 20));

        Assert.Equal(403, error.StatusCode);
        Assert.Equal("PROFILE_SECTION_PRIVATE", error.Code);
    }

    private static ReadingSession Session(Guid userId, Guid bookId, int hoursAgo) =>
        new(
            userId,
            bookId,
            DateTimeOffset.UtcNow.AddHours(hoursAgo),
            null,
            10,
            15,
            "Nội dung riêng tư");

    private static void SetIdentity(Entity entity, Guid id, DateTimeOffset createdAt)
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);
        typeof(Entity).GetProperty(nameof(Entity.CreatedAt))!.SetValue(entity, createdAt);
    }

    private sealed record FeedFixture(
        FakeBookSpaceDbContext Db,
        CommunityService Service,
        User Viewer,
        Book Book)
    {
        public static FeedFixture Create()
        {
            var db = new FakeBookSpaceDbContext();
            var viewer = new User(
                "viewer@bookspace.local",
                "hash",
                "Người xem");
            var book = new Book(
                "Sách kiểm thử feed",
                null,
                null,
                null,
                200,
                2026);
            db.Add(viewer);
            db.Add(book);
            return new FeedFixture(db, new CommunityService(db), viewer, book);
        }
    }

    private sealed class FakeBookSpaceDbContext : IBookSpaceDbContext
    {
        private readonly Dictionary<Type, object> _sets = [];

        public IQueryable<User> Users => Query<User>();
        public IQueryable<RefreshToken> RefreshTokens => Query<RefreshToken>();
        public IQueryable<Follow> Follows => Query<Follow>();
        public IQueryable<Author> Authors => Query<Author>();
        public IQueryable<Category> Categories => Query<Category>();
        public IQueryable<Book> Books => Query<Book>();
        public IQueryable<BookAuthor> BookAuthors => Query<BookAuthor>();
        public IQueryable<BookCategory> BookCategories => Query<BookCategory>();
        public IQueryable<LibraryItem> LibraryItems => Query<LibraryItem>();
        public IQueryable<LibraryItem> LibraryItemsIncludingDeleted => Query<LibraryItem>();
        public IQueryable<ReadingSession> ReadingSessions => Query<ReadingSession>();
        public IQueryable<ActiveReadingSession> ActiveReadingSessions => Query<ActiveReadingSession>();
        public IQueryable<Review> Reviews => Query<Review>();
        public IQueryable<ReviewComment> ReviewComments => Query<ReviewComment>();
        public IQueryable<ReviewLike> ReviewLikes => Query<ReviewLike>();
        public IQueryable<BookClub> BookClubs => Query<BookClub>();
        public IQueryable<BookClubMember> BookClubMembers => Query<BookClubMember>();
        public IQueryable<ClubInvitation> ClubInvitations => Query<ClubInvitation>();
        public IQueryable<ClubPost> ClubPosts => Query<ClubPost>();
        public IQueryable<ClubPostComment> ClubPostComments => Query<ClubPostComment>();
        public IQueryable<ClubReadingSprint> ClubReadingSprints => Query<ClubReadingSprint>();
        public IQueryable<ClubReadingSprintParticipant> ClubReadingSprintParticipants =>
            Query<ClubReadingSprintParticipant>();
        public IQueryable<ClubReadingSprintCheckIn> ClubReadingSprintCheckIns =>
            Query<ClubReadingSprintCheckIn>();
        public IQueryable<ClubReadingSprintMilestone> ClubReadingSprintMilestones =>
            Query<ClubReadingSprintMilestone>().Where(x => x.DeletedAt == null);
        public IQueryable<ClubReadingSprintMilestone> ClubReadingSprintMilestonesIncludingDeleted =>
            Query<ClubReadingSprintMilestone>();
        public IQueryable<ClubReadingSprintMilestoneResponse> ClubReadingSprintMilestoneResponses =>
            Query<ClubReadingSprintMilestoneResponse>();
        public IQueryable<ReadingChallenge> ReadingChallenges => Query<ReadingChallenge>();
        public IQueryable<ChallengeParticipation> ChallengeParticipations =>
            Query<ChallengeParticipation>();
        public IQueryable<Notification> Notifications => Query<Notification>();

        public void Add<T>(T entity) where T : class => Set<T>().Add(entity);

        public void AddRange<T>(IEnumerable<T> entities) where T : class =>
            Set<T>().AddRange(entities);

        public void Remove<T>(T entity) where T : class => Set<T>().Remove(entity);

        public void RemoveRange<T>(IEnumerable<T> entities) where T : class =>
            Set<T>().RemoveAll(item => entities.Contains(item));

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        private List<T> Set<T>() where T : class
        {
            if (!_sets.TryGetValue(typeof(T), out var set))
            {
                set = new List<T>();
                _sets[typeof(T)] = set;
            }

            return (List<T>)set;
        }

        private IQueryable<T> Query<T>() where T : class => Set<T>().AsQueryable();
    }
}
