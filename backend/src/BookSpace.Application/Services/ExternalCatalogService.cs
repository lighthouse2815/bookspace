using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;

namespace BookSpace.Application.Services;

public sealed class ExternalCatalogService(
    IExternalBookProvider bookProvider,
    IBookSpaceDbContext db,
    IExternalCatalogMutationBoundary mutationBoundary) : IExternalCatalogService
{
    private readonly ServiceMapper _mapper = new(db);

    public Task<ExternalBookSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(new ExternalBookSearchResult(
                false,
                "none",
                "Vui lòng nhập từ khóa tìm kiếm.",
                []));
        }

        return bookProvider.SearchAsync(query.Trim(), Math.Clamp(limit, 1, 50), cancellationToken);
    }

    public async Task<ExternalBookImportResult> ImportAsync(
        ImportExternalBookRequest request,
        CancellationToken cancellationToken)
    {
        var providerName = request.Provider.Trim().ToLowerInvariant();
        var externalId = request.ExternalId.Trim();
        var importedLink = db.ExternalBookLinks.FirstOrDefault(link =>
            link.Provider == providerName && link.ExternalId == externalId);
        if (importedLink is not null)
        {
            return AlreadyImported(providerName, externalId, importedLink);
        }

        var lookup = await bookProvider.GetByIdAsync(externalId, cancellationToken);
        if (!lookup.Available)
        {
            throw new UseCaseException(
                "EXTERNAL_CATALOG_UNAVAILABLE",
                lookup.Message,
                503);
        }

        if (!string.Equals(lookup.Provider, providerName, StringComparison.OrdinalIgnoreCase))
        {
            throw ServiceErrors.BadRequest(
                "EXTERNAL_PROVIDER_MISMATCH",
                "Nhà cung cấp của sách không khớp với yêu cầu import.");
        }

        var externalBook = lookup.Items.FirstOrDefault(item =>
            string.Equals(item.ExternalId, externalId, StringComparison.Ordinal));
        if (externalBook is null)
        {
            throw ServiceErrors.NotFound(
                "EXTERNAL_BOOK_NOT_FOUND",
                "Không tìm thấy sách từ nhà cung cấp đã chọn.");
        }

        return await mutationBoundary.ExecuteAsync(
            token => ImportInTransactionAsync(
                providerName,
                externalId,
                externalBook,
                request,
                token),
            cancellationToken);
    }

    private async Task<ExternalBookImportResult> ImportInTransactionAsync(
        string providerName,
        string externalId,
        ExternalBookResult externalBook,
        ImportExternalBookRequest request,
        CancellationToken cancellationToken)
    {
        var existingLink = db.ExternalBookLinks.FirstOrDefault(link =>
            link.Provider == providerName && link.ExternalId == externalId);
        if (existingLink is not null)
        {
            return AlreadyImported(providerName, externalId, existingLink);
        }

        var normalizedIsbn = NormalizeIsbn(externalBook.Isbn);
        var isbnMatch = normalizedIsbn is null
            ? null
            : db.Books
                .ToList()
                .FirstOrDefault(book => NormalizeIsbn(book.Isbn) == normalizedIsbn);
        if (isbnMatch is not null)
        {
            db.Add(new ExternalBookLink(providerName, externalId, isbnMatch.Id));
            await db.SaveChangesAsync(cancellationToken);
            return new ExternalBookImportResult(
                "LINKED_EXISTING",
                providerName,
                externalId,
                _mapper.BookDetail(isbnMatch));
        }

        var author = ResolveAuthor(request, externalBook);
        var categories = ResolveCategories(request, externalBook);
        if (categories.Count == 0)
        {
            throw ServiceErrors.BadRequest(
                "EXTERNAL_BOOK_CATEGORY_REQUIRED",
                "Hãy chọn hoặc tạo ít nhất một thể loại trước khi import sách.");
        }

        var pageCount = request.PageCount ?? externalBook.PageCount;
        if (pageCount is null or <= 0)
        {
            throw ServiceErrors.BadRequest(
                "EXTERNAL_BOOK_PAGE_COUNT_REQUIRED",
                "Số trang hợp lệ là bắt buộc trước khi import sách.");
        }

        var book = new Book(
            externalBook.Title,
            request.Description ?? externalBook.Description,
            normalizedIsbn,
            NormalizeCoverUrl(externalBook.CoverImageUrl),
            pageCount.Value,
            request.PublishedYear ?? externalBook.PublishedYear,
            request.Language ?? externalBook.Language ?? "vi");
        db.Add(book);
        db.Add(new BookAuthor(book.Id, author.Id));
        db.AddRange(categories.Select(category => new BookCategory(book.Id, category.Id)));
        db.Add(new ExternalBookLink(providerName, externalId, book.Id));
        CatalogAlertDelivery.AddNewBookAlerts(
            db,
            book,
            author.Id,
            categories.Select(category => category.Id).ToList());
        await db.SaveChangesAsync(cancellationToken);

        return new ExternalBookImportResult(
            "IMPORTED",
            providerName,
            externalId,
            _mapper.BookDetail(book));
    }

    private ExternalBookImportResult AlreadyImported(
        string providerName,
        string externalId,
        ExternalBookLink link)
    {
        var importedBook = db.Books.FirstOrDefault(book => book.Id == link.BookId)
                           ?? throw ServiceErrors.Conflict(
                               "EXTERNAL_BOOK_ARCHIVED",
                               "Sách đã được import trước đây nhưng hiện không còn hoạt động.");
        return new ExternalBookImportResult(
            "ALREADY_IMPORTED",
            providerName,
            externalId,
            _mapper.BookDetail(importedBook));
    }

    private Author ResolveAuthor(
        ImportExternalBookRequest request,
        ExternalBookResult externalBook)
    {
        if (request.AuthorId.HasValue)
        {
            return db.Authors.FirstOrDefault(author => author.Id == request.AuthorId.Value)
                   ?? throw ServiceErrors.NotFound(
                       "AUTHOR_NOT_FOUND",
                       "Tác giả được chọn không tồn tại.");
        }

        var authorName = request.AuthorName?.Trim();
        if (string.IsNullOrWhiteSpace(authorName))
        {
            authorName = externalBook.Authors.FirstOrDefault()?.Trim();
        }

        if (string.IsNullOrWhiteSpace(authorName))
        {
            throw ServiceErrors.BadRequest(
                "EXTERNAL_BOOK_AUTHOR_REQUIRED",
                "Hãy chọn hoặc nhập tên tác giả trước khi import sách.");
        }

        if (authorName.Length > 200)
        {
            throw ServiceErrors.BadRequest(
                "EXTERNAL_BOOK_AUTHOR_INVALID",
                "Tên tác giả không được vượt quá 200 ký tự.");
        }

        var normalizedName = authorName.ToLowerInvariant();
        var existing = db.Authors.FirstOrDefault(author =>
            author.Name.ToLower() == normalizedName);
        if (existing is not null)
        {
            return existing;
        }

        var created = new Author(authorName);
        db.Add(created);
        return created;
    }

    private IReadOnlyList<Category> ResolveCategories(
        ImportExternalBookRequest request,
        ExternalBookResult externalBook)
    {
        var categoryIds = (request.CategoryIds ?? []).Distinct().ToList();
        var categories = db.Categories
            .Where(category => categoryIds.Contains(category.Id))
            .ToList();
        if (categories.Count != categoryIds.Count)
        {
            throw ServiceErrors.NotFound(
                "CATEGORY_NOT_FOUND",
                "Có thể loại được chọn không tồn tại.");
        }

        var categoryNames = request.CategoryNames ?? externalBook.Categories;
        var normalizedNames = categoryNames
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedNames.Count > 10)
        {
            throw ServiceErrors.BadRequest(
                "EXTERNAL_BOOK_CATEGORY_LIMIT_EXCEEDED",
                "Mỗi lần import chỉ được tạo tối đa 10 thể loại.");
        }

        foreach (var name in normalizedNames)
        {
            if (name.Length > 100)
            {
                throw ServiceErrors.BadRequest(
                    "EXTERNAL_BOOK_CATEGORY_INVALID",
                    "Tên thể loại không được vượt quá 100 ký tự.");
            }

            var normalizedName = name.ToLowerInvariant();
            var category = db.Categories.FirstOrDefault(item =>
                item.Name.ToLower() == normalizedName);
            if (category is null)
            {
                category = new Category(name);
                db.Add(category);
            }

            if (categories.All(item => item.Id != category.Id))
            {
                categories.Add(category);
            }
        }

        return categories;
    }

    private static string? NormalizeIsbn(string? isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
        {
            return null;
        }

        var normalized = new string(isbn
            .Where(character => char.IsLetterOrDigit(character))
            .Select(char.ToUpperInvariant)
            .ToArray());
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeCoverUrl(string? coverImageUrl)
    {
        if (string.IsNullOrWhiteSpace(coverImageUrl) ||
            !Uri.TryCreate(coverImageUrl.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }
}
