using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

internal sealed class ServiceMapper(IBookSpaceDbContext db)
{
    public UserSummary User(User user) =>
        new(user.Id, null, user.DisplayName, user.AvatarUrl, user.Role);

    public UserSummary User(Guid userId)
    {
        var user = db.Users.FirstOrDefault(x => x.Id == userId)
                   ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        return User(user);
    }

    public AuthorDto Author(Author author) =>
        new(
            author.Id,
            author.Name,
            author.Biography,
            author.AvatarUrl,
            db.BookAuthors.Count(x => x.AuthorId == author.Id));

    public CategoryDto Category(Category category) =>
        new(
            category.Id,
            category.Name,
            category.Description,
            db.BookCategories.Count(x => x.CategoryId == category.Id));

    public BookSummary Book(Book book, Guid? viewerId = null)
    {
        var authorIds = db.BookAuthors.Where(x => x.BookId == book.Id).Select(x => x.AuthorId).ToList();
        var categoryIds = db.BookCategories.Where(x => x.BookId == book.Id).Select(x => x.CategoryId).ToList();
        var ratings = db.Reviews.Where(x => x.BookId == book.Id).Select(x => x.Rating).ToList();
        var primaryAuthor = db.Authors.Where(x => authorIds.Contains(x.Id)).OrderBy(x => x.Name).FirstOrDefault();
        return new BookSummary(
            book.Id,
            book.Title,
            book.Description,
            book.Isbn,
            book.CoverUrl,
            book.PageCount,
            book.PublicationYear,
            null,
            book.Language,
            ratings.Count == 0 ? 0 : Math.Round(ratings.Average(), 1),
            ratings.Count,
            primaryAuthor is null ? null : Author(primaryAuthor),
            primaryAuthor?.Id,
            db.Categories.Where(x => categoryIds.Contains(x.Id)).OrderBy(x => x.Name).ToList().Select(Category).ToList(),
            viewerId.HasValue
                ? db.LibraryItems
                    .Where(x => x.UserId == viewerId.Value && x.BookId == book.Id)
                    .Select(x => (LibraryStatus?)x.Status)
                    .FirstOrDefault()
                : null);
    }

    public BookDetail BookDetail(Book book, Guid? viewerId = null)
    {
        var authorIds = db.BookAuthors.Where(x => x.BookId == book.Id).Select(x => x.AuthorId).ToList();
        var categoryIds = db.BookCategories.Where(x => x.BookId == book.Id).Select(x => x.CategoryId).ToList();
        var ratings = db.Reviews.Where(x => x.BookId == book.Id).Select(x => x.Rating).ToList();
        var primaryAuthor = db.Authors.Where(x => authorIds.Contains(x.Id)).OrderBy(x => x.Name).FirstOrDefault();
        return new BookDetail(
            book.Id,
            book.Title,
            book.Description,
            book.Isbn,
            book.CoverUrl,
            book.PageCount,
            book.PublicationYear,
            null,
            book.Language,
            primaryAuthor is null ? null : Author(primaryAuthor),
            primaryAuthor?.Id,
            db.Categories.Where(x => categoryIds.Contains(x.Id)).OrderBy(x => x.Name).ToList().Select(Category).ToList(),
            ratings.Count == 0 ? 0 : Math.Round(ratings.Average(), 1),
            ratings.Count,
            viewerId.HasValue
                ? db.LibraryItems
                    .Where(x => x.UserId == viewerId.Value && x.BookId == book.Id)
                    .Select(x => (LibraryStatus?)x.Status)
                    .FirstOrDefault()
                : null,
            book.CreatedAt);
    }

    public LibraryItemDto Library(LibraryItem item)
    {
        var book = db.Books.FirstOrDefault(x => x.Id == item.BookId)
                   ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
        var progress = book.PageCount == 0 ? 0 : Math.Clamp((int)Math.Round(item.CurrentPage * 100d / book.PageCount), 0, 100);
        return new LibraryItemDto(
            item.Id,
            item.UserId,
            item.BookId,
            Book(book),
            item.Status,
            item.CurrentPage,
            progress,
            item.StartedAt,
            item.FinishedAt,
            item.UpdatedAt ?? item.CreatedAt);
    }

    public PublicLibraryItemDto PublicLibrary(LibraryItem item, Guid? viewerId)
    {
        var book = db.Books.FirstOrDefault(x => x.Id == item.BookId)
                   ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
        var progress = book.PageCount == 0
            ? 0
            : Math.Clamp((int)Math.Round(item.CurrentPage * 100d / book.PageCount), 0, 100);
        return new PublicLibraryItemDto(
            item.BookId,
            Book(book, viewerId),
            item.Status,
            progress,
            item.StartedAt,
            item.FinishedAt,
            item.UpdatedAt ?? item.CreatedAt);
    }

    public ReadingSessionDto Session(ReadingSession session)
    {
        var book = db.Books.FirstOrDefault(x => x.Id == session.BookId);
        return new ReadingSessionDto(
            session.Id,
            session.BookId,
            book is null ? null : Book(book),
            session.StartedAt,
            session.EndedAt,
            session.DurationMinutes,
            session.PagesRead,
            session.Note,
            session.CreatedAt);
    }

    public ActiveReadingSessionDto ActiveSession(
        ActiveReadingSession session,
        DateTimeOffset now)
    {
        var book = db.Books.FirstOrDefault(x => x.Id == session.BookId);
        return new ActiveReadingSessionDto(
            session.Id,
            session.BookId,
            book is null ? null : Book(book),
            session.Status,
            session.StartPage,
            session.StartedAt,
            session.ElapsedSecondsAt(now),
            session.UpdatedAt ?? session.CreatedAt);
    }

    public ReviewDto Review(Review review, Guid? viewerId)
    {
        var book = db.Books.FirstOrDefault(x => x.Id == review.BookId);
        return new ReviewDto(
            review.Id,
            review.BookId,
            book is null ? null : Book(book),
            User(review.UserId),
            review.Rating,
            review.Content,
            review.ContainsSpoilers,
            db.ReviewLikes.Count(x => x.ReviewId == review.Id),
            db.ReviewComments.Count(x => x.ReviewId == review.Id),
            viewerId.HasValue && db.ReviewLikes.Any(x => x.ReviewId == review.Id && x.UserId == viewerId.Value),
            null,
            review.CreatedAt,
            review.UpdatedAt);
    }

    public ReviewCommentDto ReviewComment(ReviewComment comment) =>
        new(comment.Id, comment.ReviewId, User(comment.UserId), comment.Content, comment.CreatedAt);

    public ClubSummary Club(BookClub club, Guid? viewerId)
    {
        var viewerRole = viewerId.HasValue
            ? db.BookClubMembers
                .Where(x => x.ClubId == club.Id && x.UserId == viewerId.Value)
                .Select(x => (ClubMemberRole?)x.Role)
                .FirstOrDefault()
            : null;
        var canModerate = viewerRole is ClubMemberRole.OWNER or ClubMemberRole.MODERATOR;
        var currentBook = club.CurrentBookId.HasValue
            ? db.Books.FirstOrDefault(x => x.Id == club.CurrentBookId.Value)
            : null;
        return new ClubSummary(
            club.Id,
            club.Name,
            club.Description,
            club.CoverUrl,
            db.BookClubMembers.Count(x => x.ClubId == club.Id),
            club.Visibility == ClubVisibility.PRIVATE,
            viewerRole.HasValue,
            currentBook is null ? null : Book(currentBook, viewerId),
            User(club.OwnerId),
            null,
            club.CreatedAt,
            viewerRole,
            new ClubPermissionsDto(
                viewerRole == ClubMemberRole.OWNER,
                canModerate,
                canModerate,
                canModerate,
                viewerRole.HasValue && viewerRole != ClubMemberRole.OWNER));
    }

    public ClubMemberDto ClubMember(BookClubMember member) =>
        new(member.Id, User(member.UserId), member.Role, member.CreatedAt);

    public ClubInvitationDto ClubInvitation(ClubInvitation invitation, Guid viewerId)
    {
        var club = db.BookClubs.FirstOrDefault(x => x.Id == invitation.ClubId)
                   ?? throw ServiceErrors.NotFound("CLUB_NOT_FOUND", "Không tìm thấy câu lạc bộ.");
        return new ClubInvitationDto(
            invitation.Id,
            Club(club, viewerId),
            User(invitation.InviterId),
            User(invitation.InvitedUserId),
            invitation.Status,
            invitation.ExpiresAt,
            invitation.RespondedAt,
            invitation.CreatedAt);
    }

    public ClubPostDto ClubPost(ClubPost post) =>
        new(
            post.Id,
            post.ClubId,
            User(post.AuthorId),
            post.Content,
            0,
            db.ClubPostComments.Count(x => x.PostId == post.Id),
            post.CreatedAt);

    public ClubPostCommentDto ClubPostComment(ClubPostComment comment) =>
        new(comment.Id, comment.PostId, User(comment.AuthorId), comment.Content, comment.CreatedAt);

    public ChallengeDto Challenge(ReadingChallenge challenge, Guid? userId)
    {
        var participation = userId.HasValue
            ? db.ChallengeParticipations.FirstOrDefault(x => x.ChallengeId == challenge.Id && x.UserId == userId.Value)
            : null;
        return new ChallengeDto(
            challenge.Id,
            challenge.Title,
            challenge.Description,
            challenge.StartsAt,
            challenge.EndsAt,
            challenge.TargetBooks,
            participation?.CompletedBooks ?? 0,
            db.ChallengeParticipations.Count(x => x.ChallengeId == challenge.Id),
            participation is not null,
            challenge.CoverImageUrl,
            challenge.IsPublished,
            participation?.CompletedAt);
    }

    public NotificationDto Notification(Notification notification) =>
        new(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.Link,
            notification.IsRead,
            notification.CreatedAt,
            notification.ReadAt);
}

internal static class ServiceErrors
{
    public static Common.UseCaseException BadRequest(string code, string message) => new(code, message);
    public static Common.UseCaseException NotFound(string code, string message) => new(code, message, 404);
    public static Common.UseCaseException Conflict(string code, string message) => new(code, message, 409);
    public static Common.UseCaseException Forbidden(string code, string message) => new(code, message, 403);
    public static Common.UseCaseException Unauthorized(string code, string message) => new(code, message, 401);
}
