using BookSpace.Application.Common;
using BookSpace.Application.Contracts;

namespace BookSpace.Application.Services;

public interface IReadingNoteService
{
    Task<PageResult<ReadingNoteDto>> GetNotesAsync(
        Guid userId,
        Guid? bookId,
        string? tag,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ReadingNoteDto> GetNoteAsync(Guid userId, Guid noteId, CancellationToken cancellationToken);

    Task<ReadingNoteDto> CreateAsync(
        Guid userId,
        CreateReadingNoteRequest request,
        CancellationToken cancellationToken);

    Task<ReadingNoteDto> UpdateAsync(
        Guid userId,
        Guid noteId,
        UpdateReadingNoteRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid noteId, CancellationToken cancellationToken);
}
