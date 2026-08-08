using System.Net;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class CatalogMetadataPublicFlowTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Guest_can_open_author_and_category_details_with_their_books()
    {
        var authors = await GetDataAsync("/api/authors?page=1&pageSize=100");
        var authorSummary = authors.GetProperty("items")
            .EnumerateArray()
            .First(item => item.GetProperty("bookCount").GetInt32() > 0);
        var authorId = authorSummary.GetProperty("id").GetGuid();

        var author = await GetDataAsync($"/api/authors/{authorId}");
        Assert.Equal(authorId, author.GetProperty("id").GetGuid());
        Assert.Equal(authorSummary.GetProperty("name").GetString(), author.GetProperty("name").GetString());
        Assert.Equal(authorSummary.GetProperty("bookCount").GetInt32(), author.GetProperty("bookCount").GetInt32());
        Assert.True(author.TryGetProperty("biography", out _));
        Assert.True(author.TryGetProperty("avatarUrl", out _));

        var authorBooks = await GetDataAsync(
            $"/api/books?authorId={authorId}&page=1&pageSize=100&sort=title");
        var authorBookItems = authorBooks.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(author.GetProperty("bookCount").GetInt32(), authorBooks.GetProperty("totalItems").GetInt32());
        Assert.NotEmpty(authorBookItems);
        Assert.All(
            authorBookItems,
            item => Assert.Equal(authorId, item.GetProperty("author").GetProperty("id").GetGuid()));

        var categories = await GetDataAsync("/api/categories?page=1&pageSize=100");
        var categorySummary = categories.GetProperty("items")
            .EnumerateArray()
            .First(item => item.GetProperty("bookCount").GetInt32() > 0);
        var categoryId = categorySummary.GetProperty("id").GetGuid();

        var category = await GetDataAsync($"/api/categories/{categoryId}");
        Assert.Equal(categoryId, category.GetProperty("id").GetGuid());
        Assert.Equal(categorySummary.GetProperty("name").GetString(), category.GetProperty("name").GetString());
        Assert.Equal(categorySummary.GetProperty("bookCount").GetInt32(), category.GetProperty("bookCount").GetInt32());
        Assert.True(category.TryGetProperty("description", out _));

        var categoryBooks = await GetDataAsync(
            $"/api/books?categoryId={categoryId}&page=1&pageSize=100&sort=title");
        var categoryBookItems = categoryBooks.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(category.GetProperty("bookCount").GetInt32(), categoryBooks.GetProperty("totalItems").GetInt32());
        Assert.NotEmpty(categoryBookItems);
        Assert.All(
            categoryBookItems,
            item => Assert.Contains(
                item.GetProperty("categories").EnumerateArray(),
                value => value.GetProperty("id").GetGuid() == categoryId));
    }

    [Fact]
    public async Task Missing_public_catalog_metadata_uses_the_stable_not_found_contract()
    {
        var missingId = Guid.NewGuid();

        await AssertNotFoundAsync($"/api/authors/{missingId}", "AUTHOR_NOT_FOUND");
        await AssertNotFoundAsync($"/api/categories/{missingId}", "CATEGORY_NOT_FOUND");
    }

    [Fact]
    public async Task Guest_can_search_sort_and_page_public_metadata_directories()
    {
        var authors = await GetDataAsync("/api/authors?sort=bookCount&page=1&pageSize=5");
        var authorItems = authors.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(5, authorItems.Count);
        Assert.True(authors.GetProperty("totalItems").GetInt32() > authorItems.Count);
        Assert.Equal(
            authorItems.Select(item => item.GetProperty("bookCount").GetInt32()).OrderByDescending(value => value),
            authorItems.Select(item => item.GetProperty("bookCount").GetInt32()));

        var authorName = authorItems[0].GetProperty("name").GetString()!;
        var authorSearch = await GetDataAsync(
            $"/api/authors?search={Uri.EscapeDataString(authorName)}&sort=name&page=1&pageSize=12");
        Assert.Contains(
            authorSearch.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("name").GetString() == authorName);

        var categories = await GetDataAsync("/api/categories?sort=bookCount&page=1&pageSize=3");
        var categoryItems = categories.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, categoryItems.Count);
        Assert.True(categories.GetProperty("totalPages").GetInt32() > 1);
        Assert.Equal(
            categoryItems.Select(item => item.GetProperty("bookCount").GetInt32()).OrderByDescending(value => value),
            categoryItems.Select(item => item.GetProperty("bookCount").GetInt32()));

        var categoryName = categoryItems[0].GetProperty("name").GetString()!;
        var categorySearch = await GetDataAsync(
            $"/api/categories?search={Uri.EscapeDataString(categoryName)}&sort=name&page=1&pageSize=12");
        Assert.Contains(
            categorySearch.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("name").GetString() == categoryName);
    }

    [Fact]
    public async Task Guest_gets_ranked_related_books_without_the_current_book()
    {
        var authors = await GetDataAsync("/api/authors?sort=bookCount&page=1&pageSize=100");
        var author = authors.GetProperty("items")
            .EnumerateArray()
            .First(item => item.GetProperty("bookCount").GetInt32() > 1);
        var authorId = author.GetProperty("id").GetGuid();
        var authorBooks = await GetDataAsync(
            $"/api/books?authorId={authorId}&sort=title&page=1&pageSize=100");
        var currentBook = authorBooks.GetProperty("items").EnumerateArray().First();
        var currentBookId = currentBook.GetProperty("id").GetGuid();

        var related = await GetDataAsync($"/api/books/{currentBookId}/related?limit=3");
        var relatedItems = related.EnumerateArray().ToList();
        Assert.NotEmpty(relatedItems);
        Assert.True(relatedItems.Count <= 3);
        Assert.DoesNotContain(relatedItems, item => item.GetProperty("id").GetGuid() == currentBookId);
        Assert.Contains(
            relatedItems,
            item => item.GetProperty("author").GetProperty("id").GetGuid() == authorId);

        await AssertNotFoundAsync($"/api/books/{Guid.NewGuid()}/related", "BOOK_NOT_FOUND");
    }

    private async Task<JsonElement> GetDataAsync(string path)
    {
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var envelope = document.RootElement;
        Assert.True(envelope.GetProperty("success").GetBoolean());
        return envelope.GetProperty("data").Clone();
    }

    private async Task AssertNotFoundAsync(string path, string code)
    {
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var envelope = document.RootElement;
        Assert.False(envelope.GetProperty("success").GetBoolean());
        Assert.Equal(code, envelope.GetProperty("code").GetString());
    }
}
