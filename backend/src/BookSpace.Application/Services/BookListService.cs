using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class BookListService(
    IBookSpaceDbContext db,
    IBookListMutationBoundary mutationBoundary) : IBookListService
{
    private const int MaximumListsPerUser = 50;
    private const int MaximumBooksPerList = 200;
    private readonly ServiceMapper _mapper = new(db);

    public PageResult<BookListSummaryDto> GetMine(
        Guid userId,
        BookListVisibility? visibility,
        Guid? bookId,
        int page,
        int pageSize)
    {
        EnsureActiveUser(userId);
        var query = db.BookLists.Where(x => x.OwnerId == userId);
        if (visibility.HasValue)
        {
            query = query.Where(x => x.Visibility == visibility.Value);
        }

        return Page(query, userId, bookId, page, pageSize);
    }

    public PageResult<BookListSummaryDto> GetPublicByUser(
        Guid ownerId,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        EnsureActiveUser(ownerId);
        UserSafetyPolicy.EnsureCanView(db, viewerId, ownerId);
        return Page(
            db.BookLists.Where(x =>
                x.OwnerId == ownerId &&
                x.Visibility == BookListVisibility.PUBLIC),
            viewerId,
            null,
            page,
            pageSize);
    }

    public BookListDetailDto Get(Guid listId, Guid? viewerId)
    {
        var list = FindViewable(listId, viewerId);
        return MapDetail(list, viewerId);
    }

    public Task<BookListDetailDto> CreateAsync(
        Guid userId,
        CreateBookListRequest request,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async operationCancellationToken =>
            {
                EnsureActiveUser(userId);
                if (db.BookLists.Count(x => x.OwnerId == userId) >= MaximumListsPerUser)
                {
                    throw ServiceErrors.Conflict(
                        "BOOK_LIST_LIMIT_REACHED",
                        $"Mỗi tài khoản chỉ được tạo tối đa {MaximumListsPerUser} bộ sưu tập.");
                }

                var list = new BookList(userId, request.Name, request.Description, request.Visibility);
                EnsureNameAvailable(userId, list.NormalizedName, null);
                db.Add(list);
                await db.SaveChangesAsync(operationCancellationToken);
                return MapDetail(list, userId);
            },
            cancellationToken);

    public Task<BookListDetailDto> UpdateAsync(
        Guid userId,
        Guid listId,
        UpdateBookListRequest request,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async operationCancellationToken =>
            {
                var list = FindOwned(listId, userId);
                var normalizedName = (request.Name ?? string.Empty).Trim().ToUpperInvariant();
                EnsureNameAvailable(userId, normalizedName, list.Id);
                list.Update(request.Name ?? string.Empty, request.Description, request.Visibility);
                await db.SaveChangesAsync(operationCancellationToken);
                return MapDetail(list, userId);
            },
            cancellationToken);

    public async Task DeleteAsync(
        Guid userId,
        Guid listId,
        CancellationToken cancellationToken)
    {
        _ = await mutationBoundary.ExecuteAsync(
            async operationCancellationToken =>
            {
                var list = FindOwned(listId, userId);
                foreach (var item in db.BookListItems.Where(x => x.BookListId == listId).ToList())
                {
                    item.SoftDelete();
                }

                list.SoftDelete();
                await db.SaveChangesAsync(operationCancellationToken);
                return true;
            },
            cancellationToken);
    }

    public Task<BookListDetailDto> AddBookAsync(
        Guid userId,
        Guid listId,
        AddBookToListRequest request,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async operationCancellationToken =>
            {
                var list = FindOwned(listId, userId);
                if (request.BookId == Guid.Empty)
                {
                    throw ServiceErrors.BadRequest("INVALID_BOOK_ID", "Sách không hợp lệ.");
                }

                _ = db.Books.FirstOrDefault(x => x.Id == request.BookId)
                    ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
                var activeItems = db.BookListItems
                    .Where(x => x.BookListId == listId)
                    .OrderBy(x => x.Position)
                    .ToList();
                if (activeItems.Any(x => x.BookId == request.BookId))
                {
                    throw ServiceErrors.Conflict(
                        "BOOK_ALREADY_IN_LIST",
                        "Sách đã có trong bộ sưu tập này.");
                }

                if (activeItems.Count >= MaximumBooksPerList)
                {
                    throw ServiceErrors.Conflict(
                        "BOOK_LIST_ITEM_LIMIT_REACHED",
                        $"Mỗi bộ sưu tập chỉ được chứa tối đa {MaximumBooksPerList} sách.");
                }

                var deletedItem = db.BookListItemsIncludingDeleted.FirstOrDefault(x =>
                    x.BookListId == listId &&
                    x.BookId == request.BookId &&
                    x.DeletedAt != null);
                if (deletedItem is null)
                {
                    db.Add(new BookListItem(listId, request.BookId, activeItems.Count));
                }
                else
                {
                    deletedItem.Restore(activeItems.Count);
                }

                list.MarkItemsChanged();
                await db.SaveChangesAsync(operationCancellationToken);
                return MapDetail(list, userId);
            },
            cancellationToken);

    public Task<BookListDetailDto> RemoveBookAsync(
        Guid userId,
        Guid listId,
        Guid bookId,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async operationCancellationToken =>
            {
                var list = FindOwned(listId, userId);
                var item = db.BookListItems.FirstOrDefault(x =>
                    x.BookListId == listId && x.BookId == bookId)
                    ?? throw ServiceErrors.NotFound(
                        "BOOK_LIST_ITEM_NOT_FOUND",
                        "Sách không có trong bộ sưu tập này.");
                item.SoftDelete();
                NormalizePositions(listId, item.Id);
                list.MarkItemsChanged();
                await db.SaveChangesAsync(operationCancellationToken);
                return MapDetail(list, userId);
            },
            cancellationToken);

    public Task<BookListDetailDto> ReorderAsync(
        Guid userId,
        Guid listId,
        ReorderBookListRequest request,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            async operationCancellationToken =>
            {
                var list = FindOwned(listId, userId);
                var requestedIds = request.BookIds
                    ?? throw ServiceErrors.BadRequest(
                        "BOOK_LIST_ORDER_REQUIRED",
                        "Thứ tự sách là bắt buộc.");
                var activeItems = db.BookListItems
                    .Where(x => x.BookListId == listId)
                    .ToList();
                if (requestedIds.Count != activeItems.Count ||
                    requestedIds.Distinct().Count() != requestedIds.Count ||
                    activeItems.Any(item => !requestedIds.Contains(item.BookId)))
                {
                    throw ServiceErrors.BadRequest(
                        "INVALID_BOOK_LIST_ORDER",
                        "Thứ tự mới phải chứa đúng mỗi sách hiện có một lần.");
                }

                var itemByBookId = activeItems.ToDictionary(x => x.BookId);
                for (var position = 0; position < requestedIds.Count; position++)
                {
                    itemByBookId[requestedIds[position]].MoveTo(position);
                }

                list.MarkItemsChanged();
                await db.SaveChangesAsync(operationCancellationToken);
                return MapDetail(list, userId);
            },
            cancellationToken);

    private PageResult<BookListSummaryDto> Page(
        IQueryable<BookList> query,
        Guid? viewerId,
        Guid? bookId,
        int page,
        int pageSize)
    {
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(x => MapSummary(x, viewerId, bookId))
            .ToList();
        return PageResult<BookListSummaryDto>.Create(items, normalizedPage, size, total);
    }

    private BookListSummaryDto MapSummary(BookList list, Guid? viewerId, Guid? bookId)
    {
        var itemQuery = db.BookListItems.Where(x => x.BookListId == list.Id);
        var previewBooks = itemQuery
            .OrderBy(x => x.Position)
            .Take(4)
            .Select(x => x.Book)
            .ToList()
            .Select(book => _mapper.Book(book, viewerId))
            .ToList();
        return new BookListSummaryDto(
            list.Id,
            list.Name,
            list.Description,
            list.Visibility,
            _mapper.User(list.OwnerId),
            itemQuery.Count(),
            previewBooks,
            viewerId == list.OwnerId,
            bookId.HasValue ? itemQuery.Any(x => x.BookId == bookId.Value) : null,
            list.CreatedAt,
            list.UpdatedAt);
    }

    private BookListDetailDto MapDetail(BookList list, Guid? viewerId)
    {
        var listItems = db.BookListItems
            .Where(x => x.BookListId == list.Id)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.CreatedAt)
            .ToList();
        var bookIds = listItems.Select(x => x.BookId).ToList();
        var booksById = db.Books
            .Where(x => bookIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        var items = listItems
            .Select(x => new BookListItemDto(
                x.Id,
                _mapper.Book(booksById[x.BookId], viewerId),
                x.Position,
                x.CreatedAt))
            .ToList();
        return new BookListDetailDto(
            list.Id,
            list.Name,
            list.Description,
            list.Visibility,
            _mapper.User(list.OwnerId),
            viewerId == list.OwnerId,
            items,
            list.CreatedAt,
            list.UpdatedAt);
    }

    private BookList FindViewable(Guid listId, Guid? viewerId)
    {
        var list = db.BookLists.FirstOrDefault(x => x.Id == listId)
            ?? throw BookListNotFound();
        if (list.Visibility == BookListVisibility.PRIVATE && viewerId != list.OwnerId)
        {
            throw BookListNotFound();
        }

        if (viewerId.HasValue &&
            viewerId.Value != list.OwnerId &&
            UserSafetyPolicy.IsBlockedBetween(db, viewerId.Value, list.OwnerId))
        {
            throw BookListNotFound();
        }

        return list;
    }

    private BookList FindOwned(Guid listId, Guid userId) =>
        db.BookLists.FirstOrDefault(x => x.Id == listId && x.OwnerId == userId)
        ?? throw BookListNotFound();

    private User EnsureActiveUser(Guid userId) =>
        db.Users.FirstOrDefault(x => x.Id == userId && !x.IsLocked)
        ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");

    private void EnsureNameAvailable(Guid ownerId, string normalizedName, Guid? excludedListId)
    {
        if (db.BookLists.Any(x =>
                x.OwnerId == ownerId &&
                x.NormalizedName == normalizedName &&
                (!excludedListId.HasValue || x.Id != excludedListId.Value)))
        {
            throw ServiceErrors.Conflict(
                "BOOK_LIST_NAME_EXISTS",
                "Bạn đã có một bộ sưu tập mang tên này.");
        }
    }

    private void NormalizePositions(Guid listId, Guid excludedItemId)
    {
        var remainingItems = db.BookListItems
            .Where(x => x.BookListId == listId && x.Id != excludedItemId)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.CreatedAt)
            .ToList();
        for (var position = 0; position < remainingItems.Count; position++)
        {
            remainingItems[position].MoveTo(position);
        }
    }

    private static UseCaseException BookListNotFound() =>
        ServiceErrors.NotFound("BOOK_LIST_NOT_FOUND", "Không tìm thấy bộ sưu tập.");
}
