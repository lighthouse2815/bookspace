using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

/// <summary>
/// EF Core adapter for the private reading-notes aggregate.
/// The model configuration and service registration are intentionally exposed
/// separately so the feature can be composed by the application's root.
/// </summary>
public sealed class ReadingNoteRepository(BookSpaceDbContext db) : IReadingNoteRepository
{
    private DbSet<ReadingNote> Notes => db.Set<ReadingNote>();

    public async Task<ReadingNoteSearchResult> SearchAsync(
        ReadingNoteSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var query = Notes
            .AsNoTracking()
            .Where(x => x.UserId == criteria.UserId && x.DeletedAt == null);

        if (criteria.BookId.HasValue)
        {
            query = query.Where(x => x.BookId == criteria.BookId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Tag))
        {
            var tag = criteria.Tag.ToLowerInvariant();
            query = query.Where(x =>
                x.TagsCsv != null &&
                (x.TagsCsv.ToLower() == tag ||
                 x.TagsCsv.ToLower().StartsWith($"{tag}|") ||
                 x.TagsCsv.ToLower().EndsWith($"|{tag}") ||
                 x.TagsCsv.ToLower().Contains($"|{tag}|")));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.ToLowerInvariant();
            query = query.Where(x =>
                (x.Quote != null && x.Quote.ToLower().Contains(search)) ||
                (x.Content != null && x.Content.ToLower().Contains(search)) ||
                (x.TagsCsv != null && x.TagsCsv.ToLower().Contains(search)));
        }

        var total = await query.LongCountAsync(cancellationToken);
        var notes = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(criteria.Skip)
            .Take(criteria.Take)
            .ToListAsync(cancellationToken);

        var items = new List<ReadingNoteDetails>(notes.Count);
        foreach (var note in notes)
        {
            var book = await GetBookAsync(note.BookId, criteria.UserId, cancellationToken);
            items.Add(new ReadingNoteDetails(note, book?.Summary));
        }

        return new ReadingNoteSearchResult(items, total);
    }

    public async Task<ReadingNoteDetails?> GetOwnedAsync(
        Guid userId,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var note = await Notes
            .FirstOrDefaultAsync(x => x.Id == noteId && x.UserId == userId && x.DeletedAt == null, cancellationToken);
        if (note is null)
        {
            return null;
        }

        var book = await GetBookAsync(note.BookId, userId, cancellationToken);
        return new ReadingNoteDetails(note, book?.Summary);
    }

    public async Task<ReadingNoteBook?> GetBookAsync(
        Guid bookId,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        var book = await db.BookSet
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == bookId, cancellationToken);
        if (book is null)
        {
            return null;
        }

        return new ReadingNoteBook(book.PageCount, await ToBookSummaryAsync(book, viewerId, cancellationToken));
    }

    public void Add(ReadingNote note) => Notes.Add(note);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private async Task<BookSummary> ToBookSummaryAsync(
        Book book,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        var author = await (
                from bookAuthor in db.BookAuthorSet.AsNoTracking()
                join candidate in db.AuthorSet.AsNoTracking() on bookAuthor.AuthorId equals candidate.Id
                where bookAuthor.BookId == book.Id
                orderby candidate.Name
                select candidate)
            .FirstOrDefaultAsync(cancellationToken);

        var authorSummary = author is null
            ? null
            : new AuthorDto(
                author.Id,
                author.Name,
                author.Biography,
                author.AvatarUrl,
                await db.BookAuthorSet.AsNoTracking().CountAsync(x => x.AuthorId == author.Id, cancellationToken));

        var categories = await (
                from bookCategory in db.BookCategorySet.AsNoTracking()
                join category in db.CategorySet.AsNoTracking() on bookCategory.CategoryId equals category.Id
                where bookCategory.BookId == book.Id
                orderby category.Name
                select category)
            .ToListAsync(cancellationToken);

        var categoryDtos = new List<CategoryDto>(categories.Count);
        foreach (var category in categories)
        {
            var bookCount = await db.BookCategorySet
                .AsNoTracking()
                .CountAsync(x => x.CategoryId == category.Id, cancellationToken);
            categoryDtos.Add(new CategoryDto(category.Id, category.Name, category.Description, bookCount));
        }

        var ratings = await db.ReviewSet
            .AsNoTracking()
            .Where(x => x.BookId == book.Id)
            .Select(x => x.Rating)
            .ToListAsync(cancellationToken);

        var shelf = await db.LibraryItemSet
            .AsNoTracking()
            .Where(x => x.UserId == viewerId && x.BookId == book.Id)
            .Select(x => (LibraryStatus?)x.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return new BookSummary(
            book.Id,
            book.Title,
            book.Description,
            book.Isbn,
            book.CoverUrl,
            book.PageCount,
            book.PublicationYear,
            null,
            book.Language,
            ratings.Count == 0 ? 0 : Math.Round(ratings.Average(), 1),
            ratings.Count,
            authorSummary,
            authorSummary?.Id,
            categoryDtos,
            shelf);
    }
}
