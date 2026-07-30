using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;

namespace BookSpace.Application.Abstractions;

public sealed record ReadingNoteSearchCriteria(
    Guid UserId,
    Guid? BookId,
    string? Tag,
    string? Search,
    int Skip,
    int Take);

public sealed record ReadingNoteDetails(ReadingNote Note, BookSummary? Book);

public sealed record ReadingNoteBook(int PageCount, BookSummary Summary);

public sealed record ReadingNoteSearchResult(
    IReadOnlyList<ReadingNoteDetails> Items,
    long TotalItems);

public interface IReadingNoteRepository
{
    Task<ReadingNoteSearchResult> SearchAsync(
        ReadingNoteSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<ReadingNoteDetails?> GetOwnedAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken);

    Task<ReadingNoteBook?> GetBookAsync(
        Guid bookId,
        Guid viewerId,
        CancellationToken cancellationToken);

    void Add(ReadingNote note);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
