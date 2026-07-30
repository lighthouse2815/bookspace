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
    [Range(1, int.MaxValue)] int? PageNumber,
    [MaxLength(500)] string? Quote,
    [MaxLength(5000)] string? Content,
    IReadOnlyList<string>? Tags);

public sealed record UpdateReadingNoteRequest(
    [Range(1, int.MaxValue)] int? PageNumber,
    [MaxLength(500)] string? Quote,
    [MaxLength(5000)] string? Content,
    IReadOnlyList<string>? Tags);
