using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Application.Abstractions;
using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookSpace.IntegrationTests;

public sealed class ExternalCatalogImportFlowTests
{
    [Fact]
    public async Task Admin_import_creates_owned_catalog_data_and_retry_is_idempotent()
    {
        var provider = new StubExternalBookProvider(
            new ExternalBookResult(
                "provider-book-1",
                "Kiến trúc phần mềm thực chiến",
                ["Nguyễn Minh An"],
                "https://images.example.test/software-architecture.jpg",
                "978-1-23456-789-0",
                "Một cuốn sách được import từ metadata bên ngoài.",
                320,
                2025,
                "vi",
                ["Công nghệ"],
                199000m,
                "https://store.example/books/provider-book-1"));
        using var factory = CreateFactory(provider);
        using var anonymous = factory.CreateClient();
        using var admin = factory.CreateClient();
        using var reader = factory.CreateClient();

        var unauthorized = await anonymous.PostAsJsonAsync(
            "/api/admin/books/import",
            new { provider = "bookstore", externalId = "provider-book-1" });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        await LoginAsync(reader, "reader@bookspace.local", "Reader123!");
        var forbidden = await reader.PostAsJsonAsync(
            "/api/admin/books/import",
            new { provider = "bookstore", externalId = "provider-book-1" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");
        var first = await admin.PostAsJsonAsync(
            "/api/admin/books/import",
            new
            {
                provider = "bookstore",
                externalId = "provider-book-1",
                categoryNames = new[] { "Kiến trúc phần mềm" }
            });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstData = await ReadDataAsync(first);
        Assert.Equal("IMPORTED", firstData.GetProperty("status").GetString());
        Assert.Equal("bookstore", firstData.GetProperty("provider").GetString());
        var firstBook = firstData.GetProperty("book");
        var bookId = firstBook.GetProperty("id").GetGuid();
        Assert.Equal("Kiến trúc phần mềm thực chiến", firstBook.GetProperty("title").GetString());
        Assert.Equal("9781234567890", firstBook.GetProperty("isbn").GetString());
        Assert.Equal("Nguyễn Minh An", firstBook.GetProperty("author").GetProperty("name").GetString());
        Assert.Contains(
            firstBook.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("name").GetString() == "Kiến trúc phần mềm");

        var retry = await admin.PostAsJsonAsync(
            "/api/admin/books/import",
            new { provider = "BOOKSTORE", externalId = "provider-book-1" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryData = await ReadDataAsync(retry);
        Assert.Equal("ALREADY_IMPORTED", retryData.GetProperty("status").GetString());
        Assert.Equal(bookId, retryData.GetProperty("book").GetProperty("id").GetGuid());
        Assert.Equal(1, provider.DetailRequestCount);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        Assert.Equal(1, await db.ExternalBookLinkSet.CountAsync());
        Assert.Equal(1, await db.BookSet.CountAsync(book => book.Id == bookId));
        Assert.Equal(1, await db.AuthorSet.CountAsync(author => author.Name == "Nguyễn Minh An"));
        Assert.Equal(1, await db.CategorySet.CountAsync(category => category.Name == "Kiến trúc phần mềm"));
    }

    [Fact]
    public async Task Import_links_an_existing_isbn_without_duplicating_the_book()
    {
        var provider = new StubExternalBookProvider(
            new ExternalBookResult(
                "provider-existing-isbn",
                "Tên từ provider không ghi đè",
                ["Tác giả provider"],
                null,
                "978 604 123 456 7",
                null,
                240,
                2024,
                "vi",
                [],
                null,
                null));
        using var factory = CreateFactory(provider);
        using var admin = factory.CreateClient();
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");

        var authorId = (await ReadDataAsync(await admin.GetAsync("/api/authors")))
            .GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var categoryId = (await ReadDataAsync(await admin.GetAsync("/api/categories")))
            .GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var create = await admin.PostAsJsonAsync(
            "/api/admin/books",
            new
            {
                title = "Sách nội bộ đã tồn tại",
                authorId,
                categoryIds = new[] { categoryId },
                isbn = "9786041234567",
                pageCount = 240,
                publishedYear = 2024
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var existingBookId = (await ReadDataAsync(create)).GetProperty("id").GetGuid();

        var import = await admin.PostAsJsonAsync(
            "/api/admin/books/import",
            new { provider = "bookstore", externalId = "provider-existing-isbn" });
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var data = await ReadDataAsync(import);
        Assert.Equal("LINKED_EXISTING", data.GetProperty("status").GetString());
        Assert.Equal(existingBookId, data.GetProperty("book").GetProperty("id").GetGuid());
        Assert.Equal("Sách nội bộ đã tồn tại", data.GetProperty("book").GetProperty("title").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        Assert.Equal(1, await db.ExternalBookLinkSet.CountAsync(link => link.BookId == existingBookId));
        Assert.Equal(1, await db.BookSet.CountAsync(book => book.Isbn == "9786041234567"));
    }

    [Fact]
    public async Task Import_returns_controlled_errors_for_missing_metadata_and_unavailable_provider()
    {
        var incompleteProvider = new StubExternalBookProvider(
            new ExternalBookResult(
                "incomplete",
                "Sách thiếu metadata",
                [],
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                null,
                null));
        using (var factory = CreateFactory(incompleteProvider))
        using (var admin = factory.CreateClient())
        {
            await LoginAsync(admin, "admin@bookspace.local", "Admin123!");
            var response = await admin.PostAsJsonAsync(
                "/api/admin/books/import",
                new { provider = "bookstore", externalId = "incomplete" });
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "EXTERNAL_BOOK_AUTHOR_REQUIRED");
        }

        using (var factory = CreateFactory(new StubExternalBookProvider(null, available: false)))
        using (var admin = factory.CreateClient())
        {
            await LoginAsync(admin, "admin@bookspace.local", "Admin123!");
            var response = await admin.PostAsJsonAsync(
                "/api/admin/books/import",
                new { provider = "bookstore", externalId = "unavailable" });
            await AssertErrorAsync(response, HttpStatusCode.ServiceUnavailable, "EXTERNAL_CATALOG_UNAVAILABLE");
        }
    }

    private static BookSpaceApiFactory CreateFactory(StubExternalBookProvider provider) =>
        new(services =>
        {
            services.RemoveAll<IExternalBookProvider>();
            services.AddSingleton<IExternalBookProvider>(provider);
        });

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            data.GetProperty("accessToken").GetString());
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private sealed class StubExternalBookProvider(
        ExternalBookResult? book,
        bool available = true) : IExternalBookProvider
    {
        public int DetailRequestCount { get; private set; }

        public Task<ExternalBookSearchResult> SearchAsync(
            string query,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result());

        public Task<ExternalBookSearchResult> GetByIdAsync(
            string externalId,
            CancellationToken cancellationToken)
        {
            DetailRequestCount++;
            return Task.FromResult(Result());
        }

        private ExternalBookSearchResult Result() =>
            new(
                available,
                "bookstore",
                available ? "Đã tải dữ liệu kiểm thử." : "Provider kiểm thử đang tắt.",
                available && book is not null ? [book] : []);
    }
}
