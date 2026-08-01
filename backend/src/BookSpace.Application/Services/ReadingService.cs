using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ReadingService(
    IBookSpaceDbContext db,
    IChallengeProgressSynchronizer progressSynchronizer,
    IReadingMutationBoundary mutationBoundary,
    TimeProvider timeProvider,
    IReadingGoalService readingGoalService) : IReadingService
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
        LibraryItem? addedItem = null;
        return await progressSynchronizer.ExecuteMutationAndSyncAsync(
            userId,
            async transactionCancellationToken =>
            {
                var book = EnsureBook(request.BookId);
                var existingItem = db.LibraryItemsIncludingDeleted.FirstOrDefault(x =>
                    x.UserId == userId &&
                    x.BookId == request.BookId);
                if (existingItem is not null && !existingItem.IsDeleted)
                {
                    throw ServiceErrors.Conflict(
                        "BOOK_ALREADY_IN_LIBRARY",
                        "Sách đã có trong thư viện của bạn.");
                }

                if (existingItem is null)
                {
                    addedItem = new LibraryItem(userId, request.BookId, request.Shelf);
                    db.Add(addedItem);
                }
                else
                {
                    existingItem.Restore(request.Shelf);
                    addedItem = existingItem;
                }

                if (request.Shelf == LibraryStatus.READ)
                {
                    addedItem.UpdateProgress(book.PageCount, book.PageCount);
                }
                await db.SaveChangesAsync(transactionCancellationToken);
                await readingGoalService.SynchronizeCompletionsAsync(
                    userId,
                    transactionCancellationToken);
            },
            () => _mapper.Library(addedItem!),
            cancellationToken);
    }

    public async Task<LibraryItemDto> UpdateLibraryItemAsync(
        Guid userId,
        Guid itemId,
        UpdateLibraryItemRequest request,
        CancellationToken cancellationToken)
    {
        LibraryItem? updatedItem = null;
        return await progressSynchronizer.ExecuteMutationAndSyncAsync(
            userId,
            async transactionCancellationToken =>
            {
                var item = FindItem(userId, itemId);
                var book = db.Books.FirstOrDefault(x => x.Id == item.BookId)
                           ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
                if (request.Shelf.HasValue)
                {
                    if (request.Shelf.Value != LibraryStatus.READING &&
                        db.ActiveReadingSessions.Any(x =>
                            x.UserId == userId &&
                            x.BookId == item.BookId))
                    {
                        throw ServiceErrors.Conflict(
                            "ACTIVE_READING_SESSION_EXISTS",
                            "Hãy hoàn tất hoặc hủy phiên đọc tập trung trước khi chuyển kệ sách.");
                    }

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

                updatedItem = item;
                await db.SaveChangesAsync(transactionCancellationToken);
                await readingGoalService.SynchronizeCompletionsAsync(
                    userId,
                    transactionCancellationToken);
            },
            () => _mapper.Library(updatedItem!),
            cancellationToken);
    }

    public async Task<LibraryItemDto> UpdateProgressAsync(
        Guid userId,
        Guid itemId,
        UpdateProgressRequest request,
        CancellationToken cancellationToken)
    {
        LibraryItem? updatedItem = null;
        return await progressSynchronizer.ExecuteMutationAndSyncAsync(
            userId,
            async transactionCancellationToken =>
            {
                var item = FindItem(userId, itemId);
                var pageCount = db.Books
                    .Where(x => x.Id == item.BookId)
                    .Select(x => x.PageCount)
                    .FirstOrDefault();
                if (pageCount == 0)
                {
                    throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
                }

                item.UpdateProgress(request.CurrentPage, pageCount);
                updatedItem = item;
                await db.SaveChangesAsync(transactionCancellationToken);
                await readingGoalService.SynchronizeCompletionsAsync(
                    userId,
                    transactionCancellationToken);
            },
            () => _mapper.Library(updatedItem!),
            cancellationToken);
    }

    public async Task RemoveLibraryItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        await progressSynchronizer.ExecuteMutationAndSyncAsync(
            userId,
            async transactionCancellationToken =>
            {
                var item = FindItem(userId, itemId);
                if (db.ActiveReadingSessions.Any(x =>
                        x.UserId == userId &&
                        x.BookId == item.BookId))
                {
                    throw ServiceErrors.Conflict(
                        "ACTIVE_READING_SESSION_EXISTS",
                        "Hãy hoàn tất hoặc hủy phiên đọc tập trung trước khi xóa sách khỏi thư viện.");
                }

                item.SoftDelete();
                await db.SaveChangesAsync(transactionCancellationToken);
                await readingGoalService.SynchronizeCompletionsAsync(
                    userId,
                    transactionCancellationToken);
            },
            () => true,
            cancellationToken);
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
        ReadingSession? addedSession = null;
        return await progressSynchronizer.ExecuteMutationAndSyncAsync(
            userId,
            async transactionCancellationToken =>
            {
                var book = EnsureBook(request.BookId);
                if (db.ActiveReadingSessions.Any(x =>
                        x.UserId == userId &&
                        x.BookId == request.BookId))
                {
                    throw ServiceErrors.Conflict(
                        "ACTIVE_READING_SESSION_EXISTS",
                        "Hãy hoàn tất hoặc hủy phiên đọc tập trung trước khi ghi phiên đọc thủ công cho sách này.");
                }

                if (request.PagesRead > book.PageCount)
                {
                    throw ServiceErrors.BadRequest(
                        "INVALID_PAGES_READ",
                        "Số trang trong một phiên đọc không được vượt quá số trang của sách.");
                }

                addedSession = new ReadingSession(
                    userId,
                    request.BookId,
                    request.StartedAt,
                    request.EndedAt,
                    request.PagesRead,
                    request.DurationMinutes,
                    request.Note);
                db.Add(addedSession);

                var item = db.LibraryItemsIncludingDeleted.FirstOrDefault(x =>
                    x.UserId == userId &&
                    x.BookId == request.BookId);
                if (item is null)
                {
                    item = new LibraryItem(userId, request.BookId, LibraryStatus.READING);
                    db.Add(item);
                }
                else if (item.IsDeleted)
                {
                    item.RestoreForReading();
                }

                item.UpdateProgress(
                    Math.Min(item.CurrentPage + request.PagesRead, book.PageCount),
                    book.PageCount);
                await db.SaveChangesAsync(transactionCancellationToken);
                await readingGoalService.SynchronizeCompletionsAsync(
                    userId,
                    transactionCancellationToken);
            },
            () => _mapper.Session(addedSession!),
            cancellationToken);
    }

    public async Task<ReadingSessionDto> CorrectSessionAsync(
        Guid userId,
        Guid sessionId,
        CorrectReadingSessionRequest request,
        CancellationToken cancellationToken)
    {
        ReadingSession? correctedSession = null;
        return await progressSynchronizer.ExecuteMutationAndSyncAsync(
            userId,
            async transactionCancellationToken =>
            {
                var session = db.ReadingSessions.FirstOrDefault(x =>
                                  x.Id == sessionId &&
                                  x.UserId == userId)
                              ?? throw ServiceErrors.NotFound(
                                  "READING_SESSION_NOT_FOUND",
                                  "Không tìm thấy phiên đọc của bạn.");
                var book = EnsureBook(session.BookId);
                if (request.PagesRead > book.PageCount)
                {
                    throw ServiceErrors.BadRequest(
                        "INVALID_PAGES_READ",
                        "Số trang trong một phiên đọc không được vượt quá số trang của sách.");
                }

                if (request.PagesRead > session.AppliedPagesHighWater &&
                    db.ActiveReadingSessions.Any(x =>
                        x.UserId == userId &&
                        x.BookId == session.BookId))
                {
                    throw ServiceErrors.Conflict(
                        "ACTIVE_READING_SESSION_EXISTS",
                        "Hãy hoàn tất hoặc hủy phiên đọc tập trung trước khi tăng số trang của phiên cũ cho sách này.");
                }

                var addedPages = session.Correct(
                    request.StartedAt,
                    request.PagesRead,
                    request.DurationMinutes,
                    request.Note);
                if (addedPages > 0)
                {
                    var item = db.LibraryItemsIncludingDeleted.FirstOrDefault(x =>
                        x.UserId == userId &&
                        x.BookId == session.BookId);
                    if (item is null)
                    {
                        item = new LibraryItem(userId, session.BookId, LibraryStatus.READING);
                        db.Add(item);
                    }
                    else if (item.IsDeleted)
                    {
                        item.RestoreForReading();
                    }

                    item.UpdateProgress(
                        Math.Min(item.CurrentPage + addedPages, book.PageCount),
                        book.PageCount);
                }

                correctedSession = session;
                await db.SaveChangesAsync(transactionCancellationToken);
                await readingGoalService.SynchronizeCompletionsAsync(
                    userId,
                    transactionCancellationToken);
            },
            () => _mapper.Session(correctedSession!),
            cancellationToken);
    }

    public ActiveReadingSessionDto? GetActiveSession(Guid userId)
    {
        var session = db.ActiveReadingSessions.FirstOrDefault(x => x.UserId == userId);
        return session is null
            ? null
            : _mapper.ActiveSession(session, timeProvider.GetUtcNow());
    }

    public Task<ActiveReadingSessionDto> StartActiveSessionAsync(
        Guid userId,
        StartActiveReadingSessionRequest request,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async transactionCancellationToken =>
            {
                if (db.ActiveReadingSessions.Any(x => x.UserId == userId))
                {
                    throw ServiceErrors.Conflict(
                        "ACTIVE_READING_SESSION_EXISTS",
                        "Bạn đang có một phiên đọc tập trung chưa hoàn tất.");
                }

                var book = EnsureBook(request.BookId);
                var item = db.LibraryItemsIncludingDeleted.FirstOrDefault(x =>
                    x.UserId == userId &&
                    x.BookId == request.BookId);
                if (item is not null &&
                    (item.Status == LibraryStatus.READ || item.CurrentPage >= book.PageCount))
                {
                    throw ServiceErrors.Conflict(
                        "BOOK_ALREADY_FINISHED",
                        "Sách này đã được đọc xong.");
                }

                if (item is null)
                {
                    item = new LibraryItem(userId, request.BookId, LibraryStatus.READING);
                    db.Add(item);
                }
                else if (item.IsDeleted)
                {
                    item.RestoreForReading();
                }
                else if (item.Status == LibraryStatus.WANT_TO_READ)
                {
                    item.ChangeStatus(LibraryStatus.READING);
                }

                var now = timeProvider.GetUtcNow();
                var session = new ActiveReadingSession(
                    userId,
                    request.BookId,
                    item.CurrentPage,
                    now);
                db.Add(session);
                await db.SaveChangesAsync(transactionCancellationToken);
                return _mapper.ActiveSession(session, now);
            },
            cancellationToken);

    public Task<ActiveReadingSessionDto> PauseActiveSessionAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        MutateActiveSessionAsync(
            userId,
            (session, now) => session.Pause(now),
            cancellationToken);

    public Task<ActiveReadingSessionDto> ResumeActiveSessionAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        MutateActiveSessionAsync(
            userId,
            (session, now) => session.Resume(now),
            cancellationToken);

    public async Task<ReadingSessionDto> FinishActiveSessionAsync(
        Guid userId,
        FinishActiveReadingSessionRequest request,
        CancellationToken cancellationToken)
    {
        ReadingSession? completedSession = null;
        return await progressSynchronizer.ExecuteMutationAndSyncAsync(
            userId,
            async transactionCancellationToken =>
            {
                var activeSession = FindActiveSession(userId);
                var now = timeProvider.GetUtcNow();
                var elapsedSeconds = activeSession.ElapsedSecondsAt(now);
                if (elapsedSeconds < 60)
                {
                    throw ServiceErrors.BadRequest(
                        "FOCUS_READING_TOO_SHORT",
                        "Phiên đọc tập trung cần kéo dài ít nhất 1 phút trước khi hoàn tất.");
                }

                if (elapsedSeconds / 60 > int.MaxValue)
                {
                    throw ServiceErrors.BadRequest(
                        "FOCUS_READING_DURATION_OUT_OF_RANGE",
                        "Thời lượng phiên đọc tập trung vượt quá giới hạn có thể lưu.");
                }

                var book = EnsureBook(activeSession.BookId);
                if (request.EndingPage <= activeSession.StartPage ||
                    request.EndingPage > book.PageCount)
                {
                    throw ServiceErrors.BadRequest(
                        "INVALID_FOCUS_END_PAGE",
                        $"Trang kết thúc phải lớn hơn {activeSession.StartPage} và không vượt quá {book.PageCount}.");
                }

                var item = db.LibraryItems.FirstOrDefault(x =>
                               x.UserId == userId &&
                               x.BookId == activeSession.BookId)
                           ?? throw ServiceErrors.Conflict(
                               "FOCUS_READING_LIBRARY_ITEM_MISSING",
                               "Sách của phiên đọc không còn trong thư viện. Hãy hủy phiên và thử lại.");
                if (request.EndingPage < item.CurrentPage)
                {
                    throw ServiceErrors.Conflict(
                        "READING_PROGRESS_CANNOT_DECREASE",
                        "Trang kết thúc không được thấp hơn tiến độ hiện tại trong thư viện.");
                }

                var durationMinutes = (int)(elapsedSeconds / 60);
                completedSession = ReadingSession.FromFocusReading(
                    userId,
                    activeSession.BookId,
                    activeSession.StartedAt,
                    now,
                    request.EndingPage - activeSession.StartPage,
                    durationMinutes,
                    request.Note);
                db.Add(completedSession);
                item.UpdateProgress(request.EndingPage, book.PageCount);
                db.Remove(activeSession);
                await db.SaveChangesAsync(transactionCancellationToken);
                await readingGoalService.SynchronizeCompletionsAsync(
                    userId,
                    transactionCancellationToken);
            },
            () => _mapper.Session(completedSession!),
            cancellationToken);
    }

    public Task CancelActiveSessionAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async transactionCancellationToken =>
            {
                db.Remove(FindActiveSession(userId));
                await db.SaveChangesAsync(transactionCancellationToken);
                return true;
            },
            cancellationToken);

    private Task<ActiveReadingSessionDto> MutateActiveSessionAsync(
        Guid userId,
        Action<ActiveReadingSession, DateTimeOffset> mutation,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async transactionCancellationToken =>
            {
                var session = FindActiveSession(userId);
                var now = timeProvider.GetUtcNow();
                mutation(session, now);
                await db.SaveChangesAsync(transactionCancellationToken);
                return _mapper.ActiveSession(session, now);
            },
            cancellationToken);

    private Book EnsureBook(Guid bookId) =>
        db.Books.FirstOrDefault(x => x.Id == bookId)
        ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");

    private LibraryItem FindItem(Guid userId, Guid itemId) =>
        db.LibraryItems.FirstOrDefault(x => x.Id == itemId && x.UserId == userId)
        ?? throw ServiceErrors.NotFound("LIBRARY_ITEM_NOT_FOUND", "Không tìm thấy sách trong thư viện của bạn.");

    private ActiveReadingSession FindActiveSession(Guid userId) =>
        db.ActiveReadingSessions.FirstOrDefault(x => x.UserId == userId)
        ?? throw ServiceErrors.NotFound(
            "ACTIVE_READING_SESSION_NOT_FOUND",
            "Bạn không có phiên đọc tập trung đang hoạt động.");
}
