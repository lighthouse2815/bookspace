using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ReadingService(
    IBookSpaceDbContext db,
    IChallengeProgressSynchronizer progressSynchronizer) : IReadingService
{
    private readonly ServiceMapper _mapper = new(db);

    public PageResult<LibraryItemDto> GetLibrary(Guid userId, LibraryStatus? status, int page, int pageSize)
    {
        var query = db.LibraryItems.Where(x => x.UserId == userId);
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(_mapper.Library)
            .ToList();
        return PageResult<LibraryItemDto>.Create(items, normalizedPage, size, total);
    }

    public PageResult<PublicLibraryItemDto> GetPublicLibrary(
        Guid userId,
        Guid? viewerId,
        LibraryStatus? status,
        int page,
        int pageSize)
    {
        var owner = db.Users.FirstOrDefault(x => x.Id == userId && !x.IsLocked)
                    ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        if (viewerId != userId && !owner.IsReadingShelfPublic)
        {
            throw ServiceErrors.Forbidden(
                "PROFILE_SECTION_PRIVATE",
                "Kệ sách của độc giả này đang được đặt ở chế độ riêng tư.");
        }

        var query = db.LibraryItems.Where(x => x.UserId == userId);
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(item => _mapper.PublicLibrary(item, viewerId))
            .ToList();
        return PageResult<PublicLibraryItemDto>.Create(items, normalizedPage, size, total);
    }

    public async Task<LibraryItemDto> AddLibraryItemAsync(
        Guid userId,
        AddLibraryItemRequest request,
        CancellationToken cancellationToken)
    {
        EnsureBook(request.BookId);
        if (db.LibraryItems.Any(x => x.UserId == userId && x.BookId == request.BookId))
        {
            throw ServiceErrors.Conflict("BOOK_ALREADY_IN_LIBRARY", "Sách đã có trong thư viện của bạn.");
        }

        var item = new LibraryItem(userId, request.BookId, request.Shelf);
        db.Add(item);
        await progressSynchronizer.SaveChangesAndSyncAsync(userId, cancellationToken);
        return _mapper.Library(item);
    }

    public async Task<LibraryItemDto> UpdateLibraryItemAsync(
        Guid userId,
        Guid itemId,
        UpdateLibraryItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = FindItem(userId, itemId);
        var book = db.Books.FirstOrDefault(x => x.Id == item.BookId)
                   ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
        if (request.Shelf.HasValue)
        {
            item.ChangeStatus(request.Shelf.Value);
            if (request.Shelf.Value == LibraryStatus.READ)
            {
                item.UpdateProgress(book.PageCount, book.PageCount);
            }
        }

        if (request.CurrentPage.HasValue)
        {
            item.UpdateProgress(request.CurrentPage.Value, book.PageCount);
        }
        else if (request.ProgressPercent.HasValue)
        {
            var page = (int)Math.Round(book.PageCount * request.ProgressPercent.Value / 100d);
            item.UpdateProgress(page, book.PageCount);
        }

        await progressSynchronizer.SaveChangesAndSyncAsync(userId, cancellationToken);
        return _mapper.Library(item);
    }

    public async Task<LibraryItemDto> UpdateProgressAsync(
        Guid userId,
        Guid itemId,
        UpdateProgressRequest request,
        CancellationToken cancellationToken)
    {
        var item = FindItem(userId, itemId);
        var pageCount = db.Books.Where(x => x.Id == item.BookId).Select(x => x.PageCount).FirstOrDefault();
        if (pageCount == 0)
        {
            throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
        }

        item.UpdateProgress(request.CurrentPage, pageCount);
        await progressSynchronizer.SaveChangesAndSyncAsync(userId, cancellationToken);
        return _mapper.Library(item);
    }

    public async Task RemoveLibraryItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        FindItem(userId, itemId).SoftDelete();
        await progressSynchronizer.SaveChangesAndSyncAsync(userId, cancellationToken);
    }

    public PageResult<ReadingSessionDto> GetSessions(Guid userId, int page, int pageSize)
    {
        var query = db.ReadingSessions.Where(x => x.UserId == userId);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.StartedAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(_mapper.Session)
            .ToList();
        return PageResult<ReadingSessionDto>.Create(items, normalizedPage, size, total);
    }

    public async Task<ReadingSessionDto> AddSessionAsync(
        Guid userId,
        CreateReadingSessionRequest request,
        CancellationToken cancellationToken)
    {
        var book = EnsureBook(request.BookId);
        if (request.PagesRead > book.PageCount)
        {
            throw new Common.UseCaseException(
                "INVALID_PAGES_READ",
                "Số trang trong một phiên đọc không được vượt quá số trang của sách.");
        }
        var session = new ReadingSession(
            userId,
            request.BookId,
            request.StartedAt,
            request.EndedAt,
            request.PagesRead,
            request.DurationMinutes,
            request.Note);
        db.Add(session);

        var item = db.LibraryItems.FirstOrDefault(x => x.UserId == userId && x.BookId == request.BookId);
        if (item is null)
        {
            item = new LibraryItem(userId, request.BookId, LibraryStatus.READING);
            db.Add(item);
        }

        item.UpdateProgress(Math.Min(item.CurrentPage + request.PagesRead, book.PageCount), book.PageCount);
        await progressSynchronizer.SaveChangesAndSyncAsync(userId, cancellationToken);
        return _mapper.Session(session);
    }

    private Book EnsureBook(Guid bookId) =>
        db.Books.FirstOrDefault(x => x.Id == bookId)
        ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");

    private LibraryItem FindItem(Guid userId, Guid itemId) =>
        db.LibraryItems.FirstOrDefault(x => x.Id == itemId && x.UserId == userId)
        ?? throw ServiceErrors.NotFound("LIBRARY_ITEM_NOT_FOUND", "Không tìm thấy sách trong thư viện của bạn.");
}
