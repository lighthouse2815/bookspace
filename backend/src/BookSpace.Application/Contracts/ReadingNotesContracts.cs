using System.ComponentModel.DataAnnotations;

namespace BookSpace.Application.Contracts;

public sealed record ReadingNoteDto(
    Guid Id,
    Guid BookId,
    BookSummary? Book,
    int? PageNumber,
    string? Quote,
    string? Content,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateReadingNoteRequest(
    Guid BookId,
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải từ 1 trở lên.")]
    int? PageNumber,
    [MaxLength(500, ErrorMessage = "Trích dẫn không được vượt quá 500 ký tự.")]
    string? Quote,
    [MaxLength(5000, ErrorMessage = "Nội dung ghi chú không được vượt quá 5.000 ký tự.")]
    string? Content,
    IReadOnlyList<string>? Tags);

public sealed record UpdateReadingNoteRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải từ 1 trở lên.")]
    int? PageNumber,
    [MaxLength(500, ErrorMessage = "Trích dẫn không được vượt quá 500 ký tự.")]
    string? Quote,
    [MaxLength(5000, ErrorMessage = "Nội dung ghi chú không được vượt quá 5.000 ký tự.")]
    string? Content,
    IReadOnlyList<string>? Tags);
