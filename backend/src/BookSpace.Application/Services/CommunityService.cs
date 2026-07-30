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
            db.Add(new Notification(
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
            db.Add(new Notification(
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

    public PageResult<FeedItem> GetFeed(Guid userId, int page, int pageSize)
    {
        var actorIds = db.Follows
            .Where(x => x.FollowerId == userId)
            .Select(x => x.FollowingId)
            .ToList();
        actorIds.Add(userId);
        actorIds = actorIds.Distinct().ToList();
        var entries = new List<FeedEntry>();

        foreach (var review in db.Reviews.Where(x => actorIds.Contains(x.UserId)).ToList())
        {
            var book = db.Books.FirstOrDefault(x => x.Id == review.BookId);
            if (book is null)
            {
                continue;
            }

            entries.Add(new FeedEntry(
                new FeedItem(
                    review.Id,
                    "REVIEW",
                    _mapper.User(review.UserId),
                    _mapper.Review(review, userId),
                    _mapper.Book(book),
                    null,
                    null,
                    review.Content,
                    null,
                    review.CreatedAt),
                review.CreatedAt));
        }

        foreach (var item in db.LibraryItems
                     .Where(x => actorIds.Contains(x.UserId) && x.StartedAt.HasValue)
                     .ToList())
        {
            var book = db.Books.FirstOrDefault(x => x.Id == item.BookId);
            if (book is null || !item.StartedAt.HasValue)
            {
                continue;
            }

            var progress = book.PageCount == 0
                ? 0
                : Math.Clamp((int)Math.Round(item.CurrentPage * 100d / book.PageCount), 0, 100);
            entries.Add(new FeedEntry(
                new FeedItem(
                    item.Id,
                    "READING_PROGRESS",
                    _mapper.User(item.UserId),
                    null,
                    _mapper.Book(book),
                    null,
                    null,
                    null,
                    progress,
                    item.StartedAt.Value),
                item.StartedAt.Value));
        }

        var visibleClubIds = db.BookClubs
            .Where(x =>
                x.Visibility == ClubVisibility.PUBLIC ||
                db.BookClubMembers.Any(member => member.ClubId == x.Id && member.UserId == userId))
            .Select(x => x.Id)
            .ToList();
        var clubs = db.BookClubs
            .Where(x => visibleClubIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        foreach (var post in db.ClubPosts
                     .Where(x => actorIds.Contains(x.AuthorId) && visibleClubIds.Contains(x.ClubId))
                     .ToList())
        {
            if (!clubs.TryGetValue(post.ClubId, out var club))
            {
                continue;
            }

            entries.Add(new FeedEntry(
                new FeedItem(
                    post.Id,
                    "CLUB_POST",
                    _mapper.User(post.AuthorId),
                    null,
                    null,
                    _mapper.Club(club, userId),
                    null,
                    post.Content,
                    null,
                    post.CreatedAt),
                post.CreatedAt));
        }

        var publishedChallenges = db.ReadingChallenges
            .Where(x => x.IsPublished)
            .ToDictionary(x => x.Id);
        foreach (var participation in db.ChallengeParticipations
                     .Where(x => actorIds.Contains(x.UserId) && x.CompletedAt.HasValue)
                     .ToList())
        {
            if (!participation.CompletedAt.HasValue ||
                !publishedChallenges.TryGetValue(participation.ChallengeId, out var challenge))
            {
                continue;
            }

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
                    participation.CompletedAt.Value),
                participation.CompletedAt.Value));
        }

        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = entries.Count;
        var items = entries
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Item.Id)
            .Skip(skip)
            .Take(size)
            .Select(x => x.Item)
            .ToList();
        return PageResult<FeedItem>.Create(items, normalizedPage, size, total);
    }

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

    private sealed record FeedEntry(FeedItem Item, DateTimeOffset OccurredAt);
}
