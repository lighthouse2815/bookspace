using System.ComponentModel.DataAnnotations;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Contracts;

public sealed record BookListSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    BookListVisibility Visibility,
    UserSummary Owner,
    int BookCount,
    IReadOnlyList<BookSummary> PreviewBooks,
    bool IsOwner,
    bool? ContainsBook,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record BookListItemDto(
    Guid Id,
    BookSummary Book,
    int Position,
    DateTimeOffset AddedAt);

public sealed record BookListDetailDto(
    Guid Id,
    string Name,
    string? Description,
    BookListVisibility Visibility,
    UserSummary Owner,
    bool IsOwner,
    IReadOnlyList<BookListItemDto> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateBookListRequest(
    [Required(ErrorMessage = "Tên bộ sưu tập là bắt buộc."), MaxLength(120)]
    string Name,
    [MaxLength(1000)] string? Description,
    BookListVisibility Visibility);

public sealed record UpdateBookListRequest(
    [Required(ErrorMessage = "Tên bộ sưu tập là bắt buộc."), MaxLength(120)]
    string Name,
    [MaxLength(1000)] string? Description,
    BookListVisibility Visibility);

public sealed record AddBookToListRequest(
    [Required(ErrorMessage = "Sách là bắt buộc.")]
    Guid BookId);

public sealed record ReorderBookListRequest(
    [Required(ErrorMessage = "Thứ tự sách là bắt buộc.")]
    IReadOnlyList<Guid>? BookIds);
