using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Exceptions;

namespace BookSpace.UnitTests;

public sealed class ReadingNoteTests
{
    [Fact]
    public void Reading_note_requires_quote_or_content()
    {
        var error = Assert.Throws<DomainException>(() =>
            new ReadingNote(Guid.NewGuid(), Guid.NewGuid(), null, " ", null, null));

        Assert.Equal("READING_NOTE_CONTENT_REQUIRED", error.Code);
    }

    [Fact]
    public void Reading_note_normalizes_trimmed_distinct_tags()
    {
        var note = new ReadingNote(
            Guid.NewGuid(),
            Guid.NewGuid(),
            12,
            "Một đoạn đáng nhớ",
            null,
            ["  Văn học ", "văn học", " Ghi chú ", ""]);

        Assert.Equal(["Văn học", "Ghi chú"], note.Tags);
        Assert.Equal("Văn học|Ghi chú", note.TagsCsv);
    }

    [Fact]
    public void Reading_note_rejects_pipe_in_tag()
    {
        var error = Assert.Throws<DomainException>(() =>
            new ReadingNote(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Trích dẫn",
                null,
                ["không|hợp lệ"]));

        Assert.Equal("INVALID_READING_NOTE_TAG", error.Code);
    }

    [Fact]
    public async Task Service_rejects_page_beyond_book_length()
    {
        var repository = new FakeReadingNoteRepository();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        repository.AddBook(bookId, 120);
        var service = new ReadingNoteService(repository);

        var error = await Assert.ThrowsAsync<UseCaseException>(() =>
            service.CreateAsync(
                userId,
                new CreateReadingNoteRequest(bookId, 121, "Trích dẫn", null, null),
                CancellationToken.None));

        Assert.Equal("INVALID_NOTE_PAGE_NUMBER", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Service_does_not_expose_another_users_note()
    {
        var repository = new FakeReadingNoteRepository();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        repository.AddBook(bookId, 300);
        var service = new ReadingNoteService(repository);
        var created = await service.CreateAsync(
            ownerId,
            new CreateReadingNoteRequest(bookId, 10, null, "Nội dung riêng tư", ["riêng tư"]),
            CancellationToken.None);

        var error = await Assert.ThrowsAsync<UseCaseException>(() =>
            service.GetNoteAsync(otherUserId, created.Id, CancellationToken.None));

        Assert.Equal("READING_NOTE_NOT_FOUND", error.Code);
        Assert.Equal(404, error.StatusCode);
    }

    [Fact]
    public async Task Service_filters_notes_by_normalized_tag()
    {
        var repository = new FakeReadingNoteRepository();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        repository.AddBook(bookId, 250);
        var service = new ReadingNoteService(repository);

        await service.CreateAsync(
            userId,
            new CreateReadingNoteRequest(bookId, null, "Đoạn đầu", null, ["Kinh điển"]),
            CancellationToken.None);
        await service.CreateAsync(
            userId,
            new CreateReadingNoteRequest(bookId, null, "Đoạn sau", null, ["Cần đọc lại"]),
            CancellationToken.None);

        var result = await service.GetNotesAsync(
            userId,
            bookId,
            "  kinh điển ",
            null,
            1,
            20,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Đoạn đầu", item.Quote);
        Assert.Equal("Kinh điển", Assert.Single(item.Tags));
    }

    private sealed class FakeReadingNoteRepository : IReadingNoteRepository
    {
        private readonly Dictionary<Guid, ReadingNoteBook> _books = [];
        private readonly List<ReadingNote> _notes = [];

        public void AddBook(Guid bookId, int pageCount) =>
            _books[bookId] = new ReadingNoteBook(pageCount, CreateBookSummary(bookId, pageCount));

        public Task<ReadingNoteSearchResult> SearchAsync(
            ReadingNoteSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            IEnumerable<ReadingNote> query = _notes.Where(x => x.UserId == criteria.UserId && !x.IsDeleted);
            if (criteria.BookId.HasValue)
            {
                query = query.Where(x => x.BookId == criteria.BookId.Value);
            }

            if (!string.IsNullOrWhiteSpace(criteria.Tag))
            {
                query = query.Where(x => x.Tags.Any(tag =>
                    string.Equals(tag, criteria.Tag, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(criteria.Search))
            {
                query = query.Where(x =>
                    (x.Quote ?? string.Empty).Contains(criteria.Search, StringComparison.OrdinalIgnoreCase) ||
                    (x.Content ?? string.Empty).Contains(criteria.Search, StringComparison.OrdinalIgnoreCase) ||
                    (x.TagsCsv ?? string.Empty).Contains(criteria.Search, StringComparison.OrdinalIgnoreCase));
            }

            var total = query.LongCount();
            var items = query
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip(criteria.Skip)
                .Take(criteria.Take)
                .Select(x => new ReadingNoteDetails(x, _books.GetValueOrDefault(x.BookId)?.Summary))
                .ToList();
            return Task.FromResult(new ReadingNoteSearchResult(items, total));
        }

        public Task<ReadingNoteDetails?> GetOwnedAsync(
            Guid userId,
            Guid noteId,
            CancellationToken cancellationToken)
        {
            var note = _notes.FirstOrDefault(x => x.Id == noteId && x.UserId == userId && !x.IsDeleted);
            return Task.FromResult(note is null
                ? null
                : new ReadingNoteDetails(note, _books.GetValueOrDefault(note.BookId)?.Summary));
        }

        public Task<ReadingNoteBook?> GetBookAsync(
            Guid bookId,
            Guid viewerId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_books.GetValueOrDefault(bookId));

        public void Add(ReadingNote note) => _notes.Add(note);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static BookSummary CreateBookSummary(Guid bookId, int pageCount) =>
            new(
                bookId,
                "Sách kiểm thử",
                null,
                null,
                null,
                pageCount,
                null,
                null,
                "vi",
                0,
                0,
                null,
                null,
                Array.Empty<CategoryDto>(),
                null);
    }
}
