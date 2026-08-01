using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class CommunityService(IBookSpaceDbContext db) : ICommunityService
{
    private readonly ServiceMapper _mapper = new(db);

    public ReviewDto GetReview(Guid reviewId, Guid? viewerId) =>
        _mapper.Review(FindReview(reviewId), viewerId);

    public PageResult<ReviewDto> GetBookReviews(Guid bookId, Guid? viewerId, int page, int pageSize)
    {
        EnsureBook(bookId);
        var query = db.Reviews.Where(x => x.BookId == bookId);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(x => _mapper.Review(x, viewerId))
            .ToList();
        return PageResult<ReviewDto>.Create(items, normalizedPage, size, total);
    }

    public PageResult<ReviewDto> GetUserReviews(
        Guid userId,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        EnsurePublicUser(userId);
        var query = db.Reviews.Where(x => x.UserId == userId);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(x => _mapper.Review(x, viewerId))
            .ToList();
        return PageResult<ReviewDto>.Create(items, normalizedPage, size, total);
    }

    public async Task<ReviewDto> CreateReviewAsync(
        Guid userId,
        CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        EnsureBook(request.BookId);
        if (db.Reviews.Any(x => x.UserId == userId && x.BookId == request.BookId))
        {
            throw ServiceErrors.Conflict("REVIEW_ALREADY_EXISTS", "Bạn đã đánh giá cuốn sách này.");
        }

        var review = new Review(userId, request.BookId, request.Rating, request.Content, request.ContainsSpoilers);
        db.Add(review);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.Review(review, userId);
    }

    public async Task<ReviewDto> UpdateReviewAsync(
        Guid userId,
        bool isAdmin,
        Guid reviewId,
        SaveReviewRequest request,
        CancellationToken cancellationToken)
    {
        var review = FindReview(reviewId);
        EnsureOwner(review.UserId, userId, isAdmin);
        review.Update(request.Rating, request.Content, request.ContainsSpoilers);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.Review(review, userId);
    }

    public async Task DeleteReviewAsync(
        Guid userId,
        bool isAdmin,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var review = FindReview(reviewId);
        EnsureOwner(review.UserId, userId, isAdmin);
        review.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LikeReviewAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken)
    {
        var review = FindReview(reviewId);
        if (db.ReviewLikes.Any(x => x.ReviewId == reviewId && x.UserId == userId))
        {
            return;
        }

        db.Add(new ReviewLike(reviewId, userId));
        if (review.UserId != userId)
        {
            var actorName = db.Users.Where(x => x.Id == userId).Select(x => x.DisplayName).First();
            NotificationDelivery.AddIfEnabled(db, new Notification(
                review.UserId,
                NotificationType.REVIEW_LIKE,
                "Đánh giá của bạn được yêu thích",
                $"{actorName} đã thích đánh giá của bạn.",
                $"/books/{review.BookId}"));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlikeReviewAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken)
    {
        FindReview(reviewId);
        var like = db.ReviewLikes.FirstOrDefault(x => x.ReviewId == reviewId && x.UserId == userId);
        if (like is null)
        {
            return;
        }

        db.Remove(like);
        await db.SaveChangesAsync(cancellationToken);
    }

    public PageResult<ReviewCommentDto> GetComments(Guid reviewId, int page, int pageSize)
    {
        FindReview(reviewId);
        var query = db.ReviewComments.Where(x => x.ReviewId == reviewId);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderBy(x => x.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(_mapper.ReviewComment)
            .ToList();
        return PageResult<ReviewCommentDto>.Create(items, normalizedPage, size, total);
    }

    public async Task<ReviewCommentDto> AddCommentAsync(
        Guid userId,
        Guid reviewId,
        CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var review = FindReview(reviewId);
        var comment = new ReviewComment(reviewId, userId, request.Content);
        db.Add(comment);
        if (review.UserId != userId)
        {
            var actorName = db.Users.Where(x => x.Id == userId).Select(x => x.DisplayName).First();
            NotificationDelivery.AddIfEnabled(db, new Notification(
                review.UserId,
                NotificationType.COMMENT,
                "Bình luận mới",
                $"{actorName} đã bình luận đánh giá của bạn.",
                $"/books/{review.BookId}"));
        }

        await db.SaveChangesAsync(cancellationToken);
        return _mapper.ReviewComment(comment);
    }

    public async Task DeleteCommentAsync(
        Guid userId,
        bool isAdmin,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var comment = db.ReviewComments.FirstOrDefault(x => x.Id == commentId)
                      ?? throw ServiceErrors.NotFound("COMMENT_NOT_FOUND", "Không tìm thấy bình luận.");
        EnsureOwner(comment.UserId, userId, isAdmin);
        comment.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }

    public PageResult<FeedItem> GetFeed(Guid userId, string? type, int page, int pageSize)
    {
        var filter = ParseFeedType(type);
        var actorIds = db.Follows
            .Where(x => x.FollowerId == userId)
            .Select(x => x.FollowingId)
            .ToList();
        actorIds.Add(userId);
        actorIds = actorIds.Distinct().ToList();
        return GetActivityForActors(actorIds, userId, filter, page, pageSize);
    }

    public PageResult<FeedItem> GetUserActivity(
        Guid userId,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        var user = EnsurePublicUser(userId);
        if (viewerId != userId && !user.IsReadingActivityPublic)
        {
            throw ServiceErrors.Forbidden(
                "PROFILE_SECTION_PRIVATE",
                "Dòng hoạt động của độc giả này đang được đặt ở chế độ riêng tư.");
        }

        return GetActivityForActors([userId], viewerId, null, page, pageSize);
    }

    private PageResult<FeedItem> GetActivityForActors(
        IReadOnlyCollection<Guid> actorIds,
        Guid? viewerId,
        FeedType? filter,
        int page,
        int pageSize)
    {
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var candidateLimit = skip + size;
        var entries = new List<FeedEntry>();
        long total = 0;

        if (filter is null or FeedType.REVIEW)
        {
            var query = db.Reviews.Where(x =>
                actorIds.Contains(x.UserId) &&
                db.Books.Any(book => book.Id == x.BookId));
            total += query.LongCount();
            foreach (var review in query
                         .OrderByDescending(x => x.CreatedAt)
                         .ThenByDescending(x => x.Id)
                         .Take(candidateLimit)
                         .ToList())
            {
                var book = db.Books.First(x => x.Id == review.BookId);
                entries.Add(new FeedEntry(
                    new FeedItem(
                        review.Id,
                        "REVIEW",
                        _mapper.User(review.UserId),
                        _mapper.Review(review, viewerId),
                        _mapper.Book(book, viewerId),
                        null,
                        null,
                        review.Content,
                        null,
                        review.CreatedAt)));
            }
        }

        if (filter is null or FeedType.READING)
        {
            var readableActorIds = db.Users
                .Where(x =>
                    actorIds.Contains(x.Id) &&
                    (viewerId.HasValue && x.Id == viewerId.Value || x.IsReadingActivityPublic))
                .Select(x => x.Id)
                .ToList();

            var sessionQuery = db.ReadingSessions.Where(x =>
                readableActorIds.Contains(x.UserId) &&
                db.Books.Any(book => book.Id == x.BookId));
            total += sessionQuery.LongCount();
            foreach (var session in sessionQuery
                         .OrderByDescending(x => x.StartedAt)
                         .ThenByDescending(x => x.Id)
                         .Take(candidateLimit)
                         .ToList())
            {
                var book = db.Books.First(x => x.Id == session.BookId);
                var progress = book.PageCount == 0
                    ? 0
                    : Math.Clamp(
                        (int)Math.Round(session.PagesRead * 100d / book.PageCount),
                        0,
                        100);
                entries.Add(new FeedEntry(
                    new FeedItem(
                        session.Id,
                        "READING_PROGRESS",
                        _mapper.User(session.UserId),
                        null,
                        _mapper.Book(book, viewerId),
                        null,
                        null,
                        null,
                        progress,
                        session.StartedAt)));
            }

            var finishedQuery = db.LibraryItems.Where(x =>
                readableActorIds.Contains(x.UserId) &&
                x.FinishedAt.HasValue &&
                db.Books.Any(book => book.Id == x.BookId));
            total += finishedQuery.LongCount();
            foreach (var item in finishedQuery
                         .OrderByDescending(x => x.FinishedAt)
                         .ThenByDescending(x => x.Id)
                         .Take(candidateLimit)
                         .ToList())
            {
                var book = db.Books.First(x => x.Id == item.BookId);
                entries.Add(new FeedEntry(
                    new FeedItem(
                        item.Id,
                        "BOOK_FINISHED",
                        _mapper.User(item.UserId),
                        null,
                        _mapper.Book(book, viewerId),
                        null,
                        null,
                        null,
                        100,
                        item.FinishedAt!.Value)));
            }
        }

        if (filter is null or FeedType.CLUB)
        {
            var query = db.ClubPosts.Where(post =>
                actorIds.Contains(post.AuthorId) &&
                db.BookClubs.Any(club =>
                    club.Id == post.ClubId &&
                    (club.Visibility == ClubVisibility.PUBLIC ||
                     viewerId.HasValue && db.BookClubMembers.Any(member =>
                         member.ClubId == club.Id && member.UserId == viewerId.Value))));
            total += query.LongCount();
            foreach (var post in query
                         .OrderByDescending(x => x.CreatedAt)
                         .ThenByDescending(x => x.Id)
                         .Take(candidateLimit)
                         .ToList())
            {
                var club = db.BookClubs.First(x => x.Id == post.ClubId);
                entries.Add(new FeedEntry(
                    new FeedItem(
                        post.Id,
                        "CLUB_POST",
                        _mapper.User(post.AuthorId),
                        null,
                        null,
                        _mapper.Club(club, viewerId),
                        null,
                        post.Content,
                        null,
                        post.CreatedAt)));
            }
        }

        if (filter is null or FeedType.CHALLENGE)
        {
            var query = db.ChallengeParticipations.Where(x =>
                actorIds.Contains(x.UserId) &&
                x.CompletedAt.HasValue &&
                db.ReadingChallenges.Any(challenge =>
                    challenge.Id == x.ChallengeId && challenge.IsPublished));
            total += query.LongCount();
            foreach (var participation in query
                         .OrderByDescending(x => x.CompletedAt)
                         .ThenByDescending(x => x.Id)
                         .Take(candidateLimit)
                         .ToList())
            {
                var challenge = db.ReadingChallenges.First(x => x.Id == participation.ChallengeId);
                entries.Add(new FeedEntry(
                    new FeedItem(
                        participation.Id,
                        "CHALLENGE",
                        _mapper.User(participation.UserId),
                        null,
                        null,
                        null,
                        _mapper.Challenge(challenge, participation.UserId),
                        null,
                        100,
                        participation.CompletedAt!.Value)));
            }
        }

        var items = entries
            .OrderByDescending(x => x.Item.CreatedAt)
            .ThenByDescending(x => x.Item.Id)
            .Skip(skip)
            .Take(size)
            .Select(x => x.Item)
            .ToList();
        return PageResult<FeedItem>.Create(items, normalizedPage, size, total);
    }

    private static FeedType? ParseFeedType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        return type.Trim().ToUpperInvariant() switch
        {
            "REVIEW" => FeedType.REVIEW,
            "READING" => FeedType.READING,
            "CLUB" => FeedType.CLUB,
            "CHALLENGE" => FeedType.CHALLENGE,
            _ => throw ServiceErrors.BadRequest(
                "INVALID_FEED_TYPE",
                "Loại bảng tin không hợp lệ. Giá trị hỗ trợ: REVIEW, READING, CLUB, CHALLENGE.")
        };
    }

    private User EnsurePublicUser(Guid userId) =>
        db.Users.FirstOrDefault(x => x.Id == userId && !x.IsLocked)
        ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");

    private void EnsureBook(Guid bookId)
    {
        if (!db.Books.Any(x => x.Id == bookId))
        {
            throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
        }
    }

    private Review FindReview(Guid reviewId) =>
        db.Reviews.FirstOrDefault(x => x.Id == reviewId)
        ?? throw ServiceErrors.NotFound("REVIEW_NOT_FOUND", "Không tìm thấy đánh giá.");

    private static void EnsureOwner(Guid ownerId, Guid userId, bool isAdmin)
    {
        if (ownerId != userId && !isAdmin)
        {
            throw ServiceErrors.Forbidden("FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.");
        }
    }

    private sealed record FeedEntry(FeedItem Item);

    private enum FeedType
    {
        REVIEW,
        READING,
        CLUB,
        CHALLENGE
    }
}
