using BookSpace.Application.Abstractions;
using BookSpace.Application.Services;
using BookSpace.Domain.Common;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.UnitTests;

public sealed class RecommendationServiceTests
{
    [Fact]
    public void Recommendations_exclude_active_library_items_reviewed_books_and_deleted_books()
    {
        var fixture = RecommendationFixture.Create();
        var owned = fixture.AddBook("Đã có trong thư viện");
        var reviewed = fixture.AddBook("Đã được chính người dùng đánh giá");
        var formerlyOwned = fixture.AddBook("Đã xóa khỏi thư viện");
        var deleted = fixture.AddBook("Sách đã xóa");
        var available = fixture.AddBook("Có thể đề xuất");
        fixture.Db.Add(new LibraryItem(fixture.Viewer.Id, owned.Id, LibraryStatus.WANT_TO_READ));
        fixture.Db.Add(new Review(
            fixture.Viewer.Id,
            reviewed.Id,
            3,
            "Sách này đã được người dùng biết đến.",
            false));
        var removedItem = new LibraryItem(
            fixture.Viewer.Id,
            formerlyOwned.Id,
            LibraryStatus.WANT_TO_READ);
        removedItem.SoftDelete();
        fixture.Db.Add(removedItem);
        deleted.SoftDelete();

        var result = fixture.Service.GetRecommendations(fixture.Viewer.Id, 1, 12);

        Assert.Equal(2, result.TotalItems);
        Assert.DoesNotContain(result.Items, item => item.Book.Id == owned.Id);
        Assert.DoesNotContain(result.Items, item => item.Book.Id == reviewed.Id);
        Assert.DoesNotContain(result.Items, item => item.Book.Id == deleted.Id);
        Assert.Contains(result.Items, item => item.Book.Id == formerlyOwned.Id);
        Assert.Contains(result.Items, item => item.Book.Id == available.Id);
    }

    [Fact]
    public void Recommendations_rank_explainable_social_and_personal_signals_before_fallback()
    {
        var fixture = RecommendationFixture.Create();
        var followedReader = fixture.AddUser("followed@bookspace.local", "Độc giả đang theo dõi");
        var communityReader = fixture.AddUser("community@bookspace.local", "Độc giả cộng đồng");
        var secondCommunityReader = fixture.AddUser("community-2@bookspace.local", "Độc giả cộng đồng 2");
        fixture.Db.Add(new Follow(fixture.Viewer.Id, followedReader.Id));

        var preferredAuthor = fixture.AddAuthor("Tác giả yêu thích");
        var otherAuthor = fixture.AddAuthor("Tác giả khác");
        var thirdAuthor = fixture.AddAuthor("Tác giả thứ ba");
        var preferredCategory = fixture.AddCategory("Thể loại yêu thích");
        var otherCategory = fixture.AddCategory("Thể loại khác");

        var historyBook = fixture.AddBook("Sách trong lịch sử đọc");
        fixture.Link(historyBook, preferredAuthor, preferredCategory);
        fixture.Db.Add(new LibraryItem(
            fixture.Viewer.Id,
            historyBook.Id,
            LibraryStatus.READ));

        var followedBook = fixture.AddBook("Được người đang theo dõi yêu thích");
        fixture.Link(followedBook, otherAuthor, otherCategory);
        var authorBook = fixture.AddBook("Cùng tác giả");
        fixture.Link(authorBook, preferredAuthor, otherCategory);
        var categoryBook = fixture.AddBook("Cùng thể loại");
        fixture.Link(categoryBook, thirdAuthor, preferredCategory);
        var fallbackBook = fixture.AddBook("Nổi bật trong cộng đồng");
        fixture.Link(fallbackBook, thirdAuthor, otherCategory);

        fixture.Db.Add(new Review(
            followedReader.Id,
            followedBook.Id,
            4,
            "Một đánh giá công khai tích cực.",
            false));
        fixture.Db.AddRange([
            new Review(
                communityReader.Id,
                fallbackBook.Id,
                5,
                "Đánh giá cộng đồng thứ nhất.",
                false),
            new Review(
                secondCommunityReader.Id,
                fallbackBook.Id,
                5,
                "Đánh giá cộng đồng thứ hai.",
                false)
        ]);

        var result = fixture.Service.GetRecommendations(fixture.Viewer.Id, 1, 12);
        var relevant = result.Items
            .Where(item => new[]
            {
                followedBook.Id,
                authorBook.Id,
                categoryBook.Id,
                fallbackBook.Id
            }.Contains(item.Book.Id))
            .ToList();

        Assert.Equal(
            new[] { followedBook.Id, authorBook.Id, categoryBook.Id, fallbackBook.Id },
            relevant.Select(item => item.Book.Id));
        Assert.Equal("FOLLOWED_READER_LIKED", relevant[0].ReasonCode);
        Assert.Equal("Được độc giả bạn theo dõi đánh giá cao.", relevant[0].ReasonText);
        Assert.Equal("MATCHED_AUTHOR", relevant[1].ReasonCode);
        Assert.Equal("Cùng tác giả với sách bạn quan tâm.", relevant[1].ReasonText);
        Assert.Equal("MATCHED_CATEGORY", relevant[2].ReasonCode);
        Assert.Equal("Cùng thể loại với sách bạn quan tâm.", relevant[2].ReasonText);
        Assert.Equal("POPULAR_FALLBACK", relevant[3].ReasonCode);
        Assert.Equal("Được cộng đồng BookSpace đánh giá cao.", relevant[3].ReasonText);
    }

    [Fact]
    public void Positive_own_reviews_seed_preferences_but_low_reviews_do_not()
    {
        var fixture = RecommendationFixture.Create();
        var likedAuthor = fixture.AddAuthor("Tác giả được đánh giá cao");
        var dislikedAuthor = fixture.AddAuthor("Tác giả bị đánh giá thấp");
        var category = fixture.AddCategory("Thể loại độc lập");
        var positiveSource = fixture.AddBook("Nguồn đánh giá tích cực");
        var lowSource = fixture.AddBook("Nguồn đánh giá thấp");
        var likedAuthorCandidate = fixture.AddBook("Sách cùng tác giả yêu thích");
        var lowAuthorCandidate = fixture.AddBook("Sách cùng tác giả bị đánh giá thấp");
        fixture.Link(positiveSource, likedAuthor, category);
        fixture.Link(lowSource, dislikedAuthor, category);
        fixture.Link(likedAuthorCandidate, likedAuthor);
        fixture.Link(lowAuthorCandidate, dislikedAuthor);
        fixture.Db.AddRange([
            new Review(
                fixture.Viewer.Id,
                positiveSource.Id,
                5,
                "Đánh giá tốt của chính mình.",
                false),
            new Review(
                fixture.Viewer.Id,
                lowSource.Id,
                2,
                "Đánh giá thấp của chính mình.",
                false)
        ]);

        var result = fixture.Service.GetRecommendations(fixture.Viewer.Id, 1, 12);

        Assert.Equal(
            "MATCHED_AUTHOR",
            Assert.Single(result.Items, item => item.Book.Id == likedAuthorCandidate.Id).ReasonCode);
        Assert.Equal(
            "POPULAR_FALLBACK",
            Assert.Single(result.Items, item => item.Book.Id == lowAuthorCandidate.Id).ReasonCode);
    }

    [Fact]
    public void Cold_start_uses_only_active_public_reviews_then_stable_book_id()
    {
        var fixture = RecommendationFixture.Create();
        var firstReviewer = fixture.AddUser("first@bookspace.local", "Độc giả thứ nhất");
        var secondReviewer = fixture.AddUser("second@bookspace.local", "Độc giả thứ hai");
        var lockedReviewer = fixture.AddUser("locked@bookspace.local", "Độc giả bị khóa");
        lockedReviewer.Lock();
        var deletedReviewer = fixture.AddUser("deleted@bookspace.local", "Độc giả đã xóa");
        deletedReviewer.SoftDelete();

        var highestAverage = fixture.AddBook(
            "Điểm trung bình cao nhất",
            new Guid("00000000-0000-0000-0000-000000000004"));
        var moreReviews = fixture.AddBook(
            "Nhiều đánh giá hơn",
            new Guid("00000000-0000-0000-0000-000000000003"));
        var fewerReviews = fixture.AddBook(
            "Ít đánh giá hơn",
            new Guid("00000000-0000-0000-0000-000000000002"));
        var privateSignalsOnly = fixture.AddBook(
            "Chỉ có tín hiệu không hợp lệ",
            new Guid("00000000-0000-0000-0000-000000000001"));
        fixture.Db.AddRange([
            new Review(firstReviewer.Id, highestAverage.Id, 5, "Năm sao.", false),
            new Review(firstReviewer.Id, moreReviews.Id, 4, "Bốn sao thứ nhất.", false),
            new Review(secondReviewer.Id, moreReviews.Id, 4, "Bốn sao thứ hai.", false),
            new Review(firstReviewer.Id, fewerReviews.Id, 4, "Một đánh giá bốn sao.", false),
            new Review(lockedReviewer.Id, privateSignalsOnly.Id, 5, "Không dùng đánh giá này.", false),
            new Review(deletedReviewer.Id, privateSignalsOnly.Id, 5, "Không dùng đánh giá đã xóa.", false)
        ]);

        var result = fixture.Service.GetRecommendations(fixture.Viewer.Id, 1, 12);

        Assert.Equal(
            new[] { highestAverage.Id, moreReviews.Id, fewerReviews.Id, privateSignalsOnly.Id },
            result.Items.Select(item => item.Book.Id));
        Assert.All(result.Items, item => Assert.Equal("POPULAR_FALLBACK", item.ReasonCode));
    }

    [Fact]
    public void Recommendation_paging_is_stable_when_all_signals_are_equal()
    {
        var fixture = RecommendationFixture.Create();
        var expectedIds = Enumerable.Range(1, 5)
            .Select(index => new Guid($"00000000-0000-0000-0000-{index:000000000000}"))
            .ToList();
        foreach (var id in expectedIds.OrderByDescending(id => id))
        {
            fixture.AddBook($"Sách {id}", id);
        }

        var first = fixture.Service.GetRecommendations(fixture.Viewer.Id, 1, 2);
        var second = fixture.Service.GetRecommendations(fixture.Viewer.Id, 2, 2);
        var third = fixture.Service.GetRecommendations(fixture.Viewer.Id, 3, 2);

        Assert.Equal(5, first.TotalItems);
        Assert.Equal(expectedIds[..2], first.Items.Select(item => item.Book.Id));
        Assert.Equal(expectedIds[2..4], second.Items.Select(item => item.Book.Id));
        Assert.Equal(expectedIds[4..], third.Items.Select(item => item.Book.Id));
        Assert.Empty(first.Items.Select(item => item.Book.Id)
            .Intersect(second.Items.Select(item => item.Book.Id)));
    }

    [Fact]
    public void Another_users_private_library_never_becomes_a_recommendation_signal()
    {
        var fixture = RecommendationFixture.Create();
        var otherReader = fixture.AddUser("private-reader@bookspace.local", "Độc giả riêng tư");
        var author = fixture.AddAuthor("Tác giả trong thư viện riêng tư");
        var privateLibraryBook = fixture.AddBook("Sách riêng tư");
        var candidate = fixture.AddBook("Sách cùng tác giả riêng tư");
        fixture.Link(privateLibraryBook, author);
        fixture.Link(candidate, author);
        fixture.Db.Add(new LibraryItem(
            otherReader.Id,
            privateLibraryBook.Id,
            LibraryStatus.READ));

        var result = fixture.Service.GetRecommendations(fixture.Viewer.Id, 1, 12);

        Assert.Equal(
            "POPULAR_FALLBACK",
            Assert.Single(result.Items, item => item.Book.Id == candidate.Id).ReasonCode);
    }

    [Fact]
    public void Recommendations_reflect_follow_review_and_library_mutations_without_stale_state()
    {
        var fixture = RecommendationFixture.Create();
        var reader = fixture.AddUser("fresh@bookspace.local", "Độc giả mới theo dõi");
        var book = fixture.AddBook("Sách thay đổi tín hiệu");

        var before = fixture.Service.GetRecommendations(fixture.Viewer.Id, 1, 12);
        Assert.Equal(
            "POPULAR_FALLBACK",
            Assert.Single(before.Items, item => item.Book.Id == book.Id).ReasonCode);

        fixture.Db.Add(new Follow(fixture.Viewer.Id, reader.Id));
        fixture.Db.Add(new Review(reader.Id, book.Id, 5, "Đánh giá vừa được thêm.", false));
        var afterSocialSignal = fixture.Service.GetRecommendations(fixture.Viewer.Id, 1, 12);
        Assert.Equal(
            "FOLLOWED_READER_LIKED",
            Assert.Single(afterSocialSignal.Items, item => item.Book.Id == book.Id).ReasonCode);

        fixture.Db.Add(new LibraryItem(
            fixture.Viewer.Id,
            book.Id,
            LibraryStatus.WANT_TO_READ));
        var afterLibraryMutation = fixture.Service.GetRecommendations(fixture.Viewer.Id, 1, 12);
        Assert.DoesNotContain(afterLibraryMutation.Items, item => item.Book.Id == book.Id);
    }

    private sealed record RecommendationFixture(
        FakeBookSpaceDbContext Db,
        CatalogService Service,
        User Viewer)
    {
        public static RecommendationFixture Create()
        {
            var db = new FakeBookSpaceDbContext();
            var viewer = new User(
                "viewer@bookspace.local",
                "hash",
                "Độc giả hiện tại");
            db.Add(viewer);
            return new RecommendationFixture(db, new CatalogService(db), viewer);
        }

        public User AddUser(string email, string displayName)
        {
            var user = new User(email, "hash", displayName);
            Db.Add(user);
            return user;
        }

        public Book AddBook(string title, Guid? id = null)
        {
            var book = new Book(title, null, null, null, 200, 2026);
            if (id.HasValue)
            {
                SetIdentity(book, id.Value);
            }

            Db.Add(book);
            return book;
        }

        public Author AddAuthor(string name)
        {
            var author = new Author(name);
            Db.Add(author);
            return author;
        }

        public Category AddCategory(string name)
        {
            var category = new Category(name);
            Db.Add(category);
            return category;
        }

        public void Link(Book book, Author author, params Category[] categories)
        {
            Db.Add(new BookAuthor(book.Id, author.Id));
            Db.AddRange(categories.Select(category => new BookCategory(book.Id, category.Id)));
        }
    }

    private static void SetIdentity(Entity entity, Guid id) =>
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);

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
        public IQueryable<ClubChatMessage> ClubChatMessages => Query<ClubChatMessage>();
        public IQueryable<ClubChatReadState> ClubChatReadStates => Query<ClubChatReadState>();
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
