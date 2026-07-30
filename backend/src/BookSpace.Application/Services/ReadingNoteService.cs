using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;

namespace BookSpace.Application.Services;

public sealed class ReadingNoteService(IReadingNoteRepository repository) : IReadingNoteService
{
    public async Task<PageResult<ReadingNoteDto>> GetNotesAsync(
        Guid userId,
        Guid? bookId,
        string? tag,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (bookId.HasValue)
        {
            await EnsureBookAsync(bookId.Value, userId, cancellationToken);
        }

        var normalizedTag = NormalizeFilterTag(tag);
        var normalizedSearch = NormalizeSearch(search);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var result = await repository.SearchAsync(
            new ReadingNoteSearchCriteria(userId, bookId, normalizedTag, normalizedSearch, skip, size),
            cancellationToken);

        return PageResult<ReadingNoteDto>.Create(
            result.Items.Select(Map),
            normalizedPage,
            size,
            result.TotalItems);
    }

    public async Task<ReadingNoteDto> GetNoteAsync(Guid userId, Guid noteId, CancellationToken cancellationToken) =>
        Map(await FindOwnedAsync(userId, noteId, cancellationToken));

    public async Task<ReadingNoteDto> CreateAsync(
        Guid userId,
        CreateReadingNoteRequest request,
        CancellationToken cancellationToken)
    {
        var book = await EnsureBookAsync(request.BookId, userId, cancellationToken);
        ValidatePageNumber(request.PageNumber, book.PageCount);

        var note = new ReadingNote(
            userId,
            request.BookId,
            request.PageNumber,
            request.Quote,
            request.Content,
            request.Tags);

        repository.Add(note);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(new ReadingNoteDetails(note, book.Summary));
    }

    public async Task<ReadingNoteDto> UpdateAsync(
        Guid userId,
        Guid noteId,
        UpdateReadingNoteRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await FindOwnedAsync(userId, noteId, cancellationToken);
        var book = await EnsureBookAsync(existing.Note.BookId, userId, cancellationToken);
        ValidatePageNumber(request.PageNumber, book.PageCount);

        existing.Note.Update(request.PageNumber, request.Quote, request.Content, request.Tags);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(new ReadingNoteDetails(existing.Note, book.Summary));
    }

    public async Task DeleteAsync(Guid userId, Guid noteId, CancellationToken cancellationToken)
    {
        var existing = await FindOwnedAsync(userId, noteId, cancellationToken);
        existing.Note.SoftDelete();
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<ReadingNoteDetails> FindOwnedAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken) =>
        await repository.GetOwnedAsync(userId, noteId, cancellationToken)
        ?? throw ServiceErrors.NotFound("READING_NOTE_NOT_FOUND", "Không tìm thấy ghi chú đọc.");

    private async Task<ReadingNoteBook> EnsureBookAsync(
        Guid bookId,
        Guid viewerId,
        CancellationToken cancellationToken) =>
        await repository.GetBookAsync(bookId, viewerId, cancellationToken)
        ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");

    private static void ValidatePageNumber(int? pageNumber, int pageCount)
    {
        if (pageNumber is not null && (pageNumber < 1 || pageNumber > pageCount))
        {
            throw ServiceErrors.BadRequest(
                "INVALID_NOTE_PAGE_NUMBER",
                $"Số trang ghi chú phải từ 1 đến {pageCount}.");
        }
    }

    private static string? NormalizeFilterTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        return ReadingNote.NormalizeTags([tag]).SingleOrDefault();
    }

    private static string? NormalizeSearch(string? search)
    {
        var normalized = search?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > 200)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_READING_NOTE_SEARCH",
                "Từ khóa tìm kiếm không được vượt quá 200 ký tự.");
        }

        return normalized;
    }

    private static ReadingNoteDto Map(ReadingNoteDetails details) =>
        new(
            details.Note.Id,
            details.Note.BookId,
            details.Book,
            details.Note.PageNumber,
            details.Note.Quote,
            details.Note.Content,
            details.Note.Tags,
            details.Note.CreatedAt,
            details.Note.UpdatedAt);
}
