using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public interface IBookListService
{
    PageResult<BookListSummaryDto> GetMine(
        Guid userId,
        BookListVisibility? visibility,
        Guid? bookId,
        int page,
        int pageSize);

    PageResult<BookListSummaryDto> GetPublicByUser(
        Guid ownerId,
        Guid? viewerId,
        int page,
        int pageSize);

    BookListDetailDto Get(Guid listId, Guid? viewerId);
    Task<BookListDetailDto> CreateAsync(Guid userId, CreateBookListRequest request, CancellationToken cancellationToken);
    Task<BookListDetailDto> UpdateAsync(Guid userId, Guid listId, UpdateBookListRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid listId, CancellationToken cancellationToken);
    Task<BookListDetailDto> AddBookAsync(Guid userId, Guid listId, AddBookToListRequest request, CancellationToken cancellationToken);
    Task<BookListDetailDto> RemoveBookAsync(Guid userId, Guid listId, Guid bookId, CancellationToken cancellationToken);
    Task<BookListDetailDto> ReorderAsync(Guid userId, Guid listId, ReorderBookListRequest request, CancellationToken cancellationToken);
}
