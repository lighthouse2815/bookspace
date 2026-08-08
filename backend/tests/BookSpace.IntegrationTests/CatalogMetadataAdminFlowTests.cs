using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class CatalogMetadataAdminFlowTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    private readonly BookSpaceApiFactory _factory = factory;

    [Fact]
    public async Task Admin_can_search_update_and_delete_unused_catalog_metadata()
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/admin/authors")).StatusCode);

        using var reader = _factory.CreateClient();
        await LoginAsync(reader, "reader@bookspace.local", "Reader123!");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await reader.GetAsync("/api/admin/categories")).StatusCode);

        using var admin = _factory.CreateClient();
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");
        var suffix = Guid.NewGuid().ToString("N");
        var authorName = $"Tác giả metadata {suffix}";
        var categoryName = $"Thể loại metadata {suffix}";

        var createAuthor = await admin.PostAsJsonAsync(
            "/api/admin/authors",
            new
            {
                name = authorName,
                biography = $"Tiểu sử có từ khóa ĐỘC NHẤT {suffix}",
                avatarUrl = "https://images.example.test/author.jpg"
            });
        Assert.Equal(HttpStatusCode.Created, createAuthor.StatusCode);
        var authorId = (await ReadDataAsync(createAuthor)).GetProperty("id").GetGuid();

        var createCategory = await admin.PostAsJsonAsync(
            "/api/admin/categories",
            new
            {
                name = categoryName,
                description = $"Mô tả có từ khóa ĐẶC BIỆT {suffix}"
            });
        Assert.Equal(HttpStatusCode.Created, createCategory.StatusCode);
        var categoryId = (await ReadDataAsync(createCategory)).GetProperty("id").GetGuid();

        var authorPage = await GetDataAsync(
            admin,
            $"/api/admin/authors?search={Uri.EscapeDataString($"độc nhất {suffix}")}&page=1&pageSize=1");
        Assert.Equal(1, authorPage.GetProperty("page").GetInt32());
        Assert.Equal(1, authorPage.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, authorPage.GetProperty("totalItems").GetInt32());
        var author = Assert.Single(authorPage.GetProperty("items").EnumerateArray());
        Assert.Equal(authorId, author.GetProperty("id").GetGuid());
        Assert.Equal(0, author.GetProperty("bookCount").GetInt32());

        var categoryPage = await GetDataAsync(
            admin,
            $"/api/admin/categories?search={Uri.EscapeDataString($"đặc biệt {suffix}")}&page=1&pageSize=1");
        var category = Assert.Single(categoryPage.GetProperty("items").EnumerateArray());
        Assert.Equal(categoryId, category.GetProperty("id").GetGuid());
        Assert.Equal(0, category.GetProperty("bookCount").GetInt32());

        var updatedAuthorName = $"Tác giả đã sửa {suffix}";
        var updateAuthor = await admin.PatchAsJsonAsync(
            $"/api/admin/authors/{authorId}",
            new { name = updatedAuthorName, biography = "Tiểu sử đã cập nhật", avatarUrl = (string?)null });
        Assert.Equal(HttpStatusCode.OK, updateAuthor.StatusCode);
        Assert.Equal(updatedAuthorName, (await ReadDataAsync(updateAuthor)).GetProperty("name").GetString());

        var updatedCategoryName = $"Thể loại đã sửa {suffix}";
        var updateCategory = await admin.PatchAsJsonAsync(
            $"/api/admin/categories/{categoryId}",
            new { name = updatedCategoryName, description = "Mô tả đã cập nhật" });
        Assert.Equal(HttpStatusCode.OK, updateCategory.StatusCode);
        Assert.Equal(updatedCategoryName, (await ReadDataAsync(updateCategory)).GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/admin/authors/{authorId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/admin/categories/{categoryId}")).StatusCode);

        var deletedAuthors = await GetDataAsync(
            admin,
            $"/api/admin/authors?search={Uri.EscapeDataString(suffix)}");
        var deletedCategories = await GetDataAsync(
            admin,
            $"/api/admin/categories?search={Uri.EscapeDataString(suffix)}");
        Assert.Equal(0, deletedAuthors.GetProperty("totalItems").GetInt32());
        Assert.Equal(0, deletedCategories.GetProperty("totalItems").GetInt32());

        var longSearch = await admin.GetAsync($"/api/admin/authors?search={new string('a', 201)}");
        Assert.Equal(HttpStatusCode.BadRequest, longSearch.StatusCode);
        Assert.Equal(
            "CATALOG_METADATA_SEARCH_TOO_LONG",
            (await ReadEnvelopeAsync(longSearch)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Admin_cannot_delete_author_or_category_that_is_attached_to_a_book()
    {
        using var admin = _factory.CreateClient();
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");
        var suffix = Guid.NewGuid().ToString("N");

        var authorResponse = await admin.PostAsJsonAsync(
            "/api/admin/authors",
            new { name = $"Tác giả đang dùng {suffix}" });
        var authorId = (await ReadDataAsync(authorResponse)).GetProperty("id").GetGuid();
        var categoryResponse = await admin.PostAsJsonAsync(
            "/api/admin/categories",
            new { name = $"Thể loại đang dùng {suffix}" });
        var categoryId = (await ReadDataAsync(categoryResponse)).GetProperty("id").GetGuid();

        var createBook = await admin.PostAsJsonAsync(
            "/api/admin/books",
            new
            {
                title = $"Sách kiểm tra metadata {suffix}",
                authorId,
                categoryIds = new[] { categoryId },
                pageCount = 120,
                publishedYear = 2026
            });
        Assert.Equal(HttpStatusCode.Created, createBook.StatusCode);

        var authorDelete = await admin.DeleteAsync($"/api/admin/authors/{authorId}");
        Assert.Equal(HttpStatusCode.Conflict, authorDelete.StatusCode);
        Assert.Equal(
            "AUTHOR_IN_USE",
            (await ReadEnvelopeAsync(authorDelete)).GetProperty("code").GetString());

        var categoryDelete = await admin.DeleteAsync($"/api/admin/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.Conflict, categoryDelete.StatusCode);
        Assert.Equal(
            "CATEGORY_IN_USE",
            (await ReadEnvelopeAsync(categoryDelete)).GetProperty("code").GetString());
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            data.GetProperty("accessToken").GetString());
    }

    private static async Task<JsonElement> GetDataAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response) =>
        (await ReadEnvelopeAsync(response)).GetProperty("data");

    private static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
