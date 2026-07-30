using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;

namespace BookSpace.Application.Services;

public sealed class CatalogService(IBookSpaceDbContext db) : ICatalogService
{
    private readonly ServiceMapper _mapper = new(db);

    public PageResult<BookSummary> GetBooks(
        string? search,
        Guid? authorId,
        Guid? categoryId,
        string? sort,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        var query = db.Books;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLowerInvariant();
            var authorBookIds = db.BookAuthors
                .Where(x => db.Authors.Any(a => a.Id == x.AuthorId && a.Name.ToLower().Contains(keyword)))
                .Select(x => x.BookId);
            query = query.Where(x =>
                x.Title.ToLower().Contains(keyword) ||
                (x.Isbn != null && x.Isbn.ToLower().Contains(keyword)) ||
                authorBookIds.Contains(x.Id));
        }

        if (authorId.HasValue)
        {
            var ids = db.BookAuthors.Where(x => x.AuthorId == authorId.Value).Select(x => x.BookId);
            query = query.Where(x => ids.Contains(x.Id));
        }

        if (categoryId.HasValue)
        {
            var ids = db.BookCategories.Where(x => x.CategoryId == categoryId.Value).Select(x => x.BookId);
            query = query.Where(x => ids.Contains(x.Id));
        }

        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var ordered = sort?.Trim().ToLowerInvariant() switch
        {
            "popular" => query
                .OrderByDescending(book => db.Reviews.Count(review => review.BookId == book.Id))
                .ThenBy(book => book.Id),
            "rating" => query.OrderByDescending(book =>
                db.Reviews.Where(review => review.BookId == book.Id)
                    .Select(review => (double?)review.Rating)
                    .Average() ?? 0)
                .ThenBy(book => book.Id),
            "title" => query.OrderBy(book => book.Title).ThenBy(book => book.Id),
            "newest" => query
                .OrderByDescending(book => book.PublicationYear ?? 0)
                .ThenByDescending(book => book.CreatedAt)
                .ThenBy(book => book.Id),
            _ => query.OrderByDescending(book => book.CreatedAt).ThenBy(book => book.Id)
        };
        var items = ordered.Skip(skip).Take(size).ToList().Select(x => _mapper.Book(x, viewerId)).ToList();
        return PageResult<BookSummary>.Create(items, normalizedPage, size, total);
    }

    public BookDetail GetBook(Guid bookId, Guid? viewerId)
    {
        var book = FindBook(bookId);
        return _mapper.BookDetail(book, viewerId);
    }

    public PageResult<AuthorDto> GetAuthors(int page, int pageSize)
    {
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = db.Authors.LongCount();
        var items = db.Authors.OrderBy(x => x.Name).Skip(skip).Take(size).ToList().Select(_mapper.Author).ToList();
        return PageResult<AuthorDto>.Create(items, normalizedPage, size, total);
    }

    public PageResult<CategoryDto> GetCategories(int page, int pageSize)
    {
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = db.Categories.LongCount();
        var items = db.Categories.OrderBy(x => x.Name).Skip(skip).Take(size).ToList().Select(_mapper.Category).ToList();
        return PageResult<CategoryDto>.Create(items, normalizedPage, size, total);
    }

    public async Task<BookDetail> CreateBookAsync(SaveBookRequest request, CancellationToken cancellationToken)
    {
        EnsureBookUnique(request.Isbn, null);
        EnsureRelations(request.AuthorId, request.CategoryIds);
        var book = new Book(
            request.Title,
            request.Description,
            request.Isbn,
            request.CoverImageUrl,
            request.PageCount ?? 1,
            request.PublishedYear,
            request.Language ?? "vi");
        db.Add(book);
        db.Add(new BookAuthor(book.Id, request.AuthorId));
        db.AddRange((request.CategoryIds ?? []).Distinct().Select(x => new BookCategory(book.Id, x)));
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.BookDetail(book);
    }

    public async Task<BookDetail> UpdateBookAsync(Guid id, SaveBookRequest request, CancellationToken cancellationToken)
    {
        var book = FindBook(id);
        EnsureBookUnique(request.Isbn, id);
        EnsureRelations(request.AuthorId, request.CategoryIds);
        book.Update(
            request.Title,
            request.Description,
            request.Isbn,
            request.CoverImageUrl,
            request.PageCount ?? 1,
            request.PublishedYear,
            request.Language ?? "vi");
        db.RemoveRange(db.BookAuthors.Where(x => x.BookId == id).ToList());
        db.RemoveRange(db.BookCategories.Where(x => x.BookId == id).ToList());
        db.Add(new BookAuthor(book.Id, request.AuthorId));
        db.AddRange((request.CategoryIds ?? []).Distinct().Select(x => new BookCategory(book.Id, x)));
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.BookDetail(book);
    }

    public async Task DeleteBookAsync(Guid id, CancellationToken cancellationToken)
    {
        FindBook(id).SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthorDto> CreateAuthorAsync(SaveAuthorRequest request, CancellationToken cancellationToken)
    {
        EnsureAuthorUnique(request.Name, null);
        var author = new Author(request.Name, request.Biography, request.AvatarUrl);
        db.Add(author);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.Author(author);
    }

    public async Task<AuthorDto> UpdateAuthorAsync(Guid id, SaveAuthorRequest request, CancellationToken cancellationToken)
    {
        var author = db.Authors.FirstOrDefault(x => x.Id == id)
                     ?? throw ServiceErrors.NotFound("AUTHOR_NOT_FOUND", "Không tìm thấy tác giả.");
        EnsureAuthorUnique(request.Name, id);
        author.Update(request.Name, request.Biography, request.AvatarUrl);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.Author(author);
    }

    public async Task DeleteAuthorAsync(Guid id, CancellationToken cancellationToken)
    {
        var author = db.Authors.FirstOrDefault(x => x.Id == id)
                     ?? throw ServiceErrors.NotFound("AUTHOR_NOT_FOUND", "Không tìm thấy tác giả.");
        if (db.BookAuthors.Any(x => x.AuthorId == id))
        {
            throw ServiceErrors.Conflict("AUTHOR_IN_USE", "Không thể xóa tác giả đang được gắn với sách.");
        }

        author.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        EnsureCategoryUnique(request.Name, null);
        var category = new Category(request.Name, request.Description);
        db.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.Category(category);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = db.Categories.FirstOrDefault(x => x.Id == id)
                       ?? throw ServiceErrors.NotFound("CATEGORY_NOT_FOUND", "Không tìm thấy thể loại.");
        EnsureCategoryUnique(request.Name, id);
        category.Update(request.Name, request.Description);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.Category(category);
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = db.Categories.FirstOrDefault(x => x.Id == id)
                       ?? throw ServiceErrors.NotFound("CATEGORY_NOT_FOUND", "Không tìm thấy thể loại.");
        if (db.BookCategories.Any(x => x.CategoryId == id))
        {
            throw ServiceErrors.Conflict("CATEGORY_IN_USE", "Không thể xóa thể loại đang được gắn với sách.");
        }

        category.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }

    private Book FindBook(Guid id) =>
        db.Books.FirstOrDefault(x => x.Id == id)
        ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");

    private void EnsureBookUnique(string? isbn, Guid? currentId)
    {
        if (!string.IsNullOrWhiteSpace(isbn) &&
            db.Books.Any(x => x.Isbn == isbn.Trim() && (!currentId.HasValue || x.Id != currentId.Value)))
        {
            throw ServiceErrors.Conflict("ISBN_ALREADY_EXISTS", "ISBN đã tồn tại.");
        }
    }

    private void EnsureAuthorUnique(string name, Guid? currentId)
    {
        var normalized = name.Trim().ToLowerInvariant();
        if (db.Authors.Any(x => x.Name.ToLower() == normalized && (!currentId.HasValue || x.Id != currentId.Value)))
        {
            throw ServiceErrors.Conflict("AUTHOR_ALREADY_EXISTS", "Tên tác giả đã tồn tại.");
        }
    }

    private void EnsureCategoryUnique(string name, Guid? currentId)
    {
        var normalized = name.Trim().ToLowerInvariant();
        if (db.Categories.Any(x => x.Name.ToLower() == normalized && (!currentId.HasValue || x.Id != currentId.Value)))
        {
            throw ServiceErrors.Conflict("CATEGORY_ALREADY_EXISTS", "Tên thể loại đã tồn tại.");
        }
    }

    private void EnsureRelations(Guid authorId, IReadOnlyList<Guid>? categoryIds)
    {
        var distinctCategories = (categoryIds ?? []).Distinct().ToList();
        if (authorId == Guid.Empty || !db.Authors.Any(x => x.Id == authorId))
        {
            throw ServiceErrors.NotFound("AUTHOR_NOT_FOUND", "Tác giả không tồn tại.");
        }

        if (db.Categories.Count(x => distinctCategories.Contains(x.Id)) != distinctCategories.Count)
        {
            throw ServiceErrors.NotFound("CATEGORY_NOT_FOUND", "Có thể loại không tồn tại.");
        }
    }
}
