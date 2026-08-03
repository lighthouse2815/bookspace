using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class BookListFlowTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    [Fact]
    public async Task Owner_can_manage_order_and_privacy_while_non_owner_is_cloaked()
    {
        using var owner = await RegisterAsync("list-owner");
        using var outsider = await RegisterAsync("list-outsider");
        using var anonymous = factory.CreateClient();
        var books = (await GetDataAsync(anonymous, "/api/books?pageSize=2"))
            .GetProperty("items")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToList();
        Assert.Equal(2, books.Count);

        var createdResponse = await owner.Client.PostAsJsonAsync(
            "/api/book-lists",
            new
            {
                name = "  Tủ sách mùa mưa  ",
                description = "Đọc chậm vào cuối tuần",
                visibility = "PRIVATE"
            });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await ReadDataAsync(createdResponse);
        var listId = created.GetProperty("id").GetGuid();
        Assert.Equal("Tủ sách mùa mưa", created.GetProperty("name").GetString());

        await AssertFailureAsync(
            await anonymous.GetAsync($"/api/book-lists/{listId}"),
            HttpStatusCode.NotFound,
            "BOOK_LIST_NOT_FOUND");
        await AssertFailureAsync(
            await outsider.Client.GetAsync($"/api/book-lists/{listId}"),
            HttpStatusCode.NotFound,
            "BOOK_LIST_NOT_FOUND");
        await AssertFailureAsync(
            await outsider.Client.PatchAsJsonAsync(
                $"/api/book-lists/{listId}",
                new { name = "Chiếm quyền", description = "", visibility = "PUBLIC" }),
            HttpStatusCode.NotFound,
            "BOOK_LIST_NOT_FOUND");

        foreach (var bookId in books)
        {
            var add = await owner.Client.PostAsJsonAsync(
                $"/api/book-lists/{listId}/books",
                new { bookId });
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        }

        await AssertFailureAsync(
            await owner.Client.PostAsJsonAsync(
                $"/api/book-lists/{listId}/books",
                new { bookId = books[0] }),
            HttpStatusCode.Conflict,
            "BOOK_ALREADY_IN_LIST");

        var reorder = await owner.Client.PutAsJsonAsync(
            $"/api/book-lists/{listId}/books/reorder",
            new { bookIds = books.AsEnumerable().Reverse().ToArray() });
        Assert.Equal(HttpStatusCode.OK, reorder.StatusCode);
        var reordered = await ReadDataAsync(reorder);
        Assert.Equal(
            books.AsEnumerable().Reverse(),
            reordered.GetProperty("items").EnumerateArray().Select(x =>
                x.GetProperty("book").GetProperty("id").GetGuid()));

        var remove = await owner.Client.DeleteAsync(
            $"/api/book-lists/{listId}/books/{books[0]}");
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        var restore = await owner.Client.PostAsJsonAsync(
            $"/api/book-lists/{listId}/books",
            new { bookId = books[0] });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var makePublic = await owner.Client.PatchAsJsonAsync(
            $"/api/book-lists/{listId}",
            new
            {
                name = "Tủ sách mùa mưa",
                description = "Đã công khai",
                visibility = "PUBLIC"
            });
        Assert.Equal(HttpStatusCode.OK, makePublic.StatusCode);
        Assert.Equal(
            listId,
            (await GetDataAsync(anonymous, $"/api/book-lists/{listId}"))
            .GetProperty("id")
            .GetGuid());
        var publicLists = await GetDataAsync(
            anonymous,
            $"/api/users/{owner.Id}/book-lists");
        Assert.Contains(
            publicLists.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == listId);

        var duplicate = await owner.Client.PostAsJsonAsync(
            "/api/book-lists",
            new { name = "tủ SÁCH mùa MƯA", description = "", visibility = "PUBLIC" });
        await AssertFailureAsync(
            duplicate,
            HttpStatusCode.Conflict,
            "BOOK_LIST_NAME_EXISTS");

        var delete = await owner.Client.DeleteAsync($"/api/book-lists/{listId}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        await AssertFailureAsync(
            await owner.Client.GetAsync($"/api/book-lists/{listId}"),
            HttpStatusCode.NotFound,
            "BOOK_LIST_NOT_FOUND");
    }

    [Fact]
    public async Task Block_in_either_direction_cloaks_public_lists_but_guests_still_see_them()
    {
        using var owner = await RegisterAsync("list-block-owner");
        using var viewer = await RegisterAsync("list-block-viewer");
        using var anonymous = factory.CreateClient();
        var create = await owner.Client.PostAsJsonAsync(
            "/api/book-lists",
            new { name = "Danh sách công khai", description = "", visibility = "PUBLIC" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var listId = (await ReadDataAsync(create)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await viewer.Client.GetAsync($"/api/book-lists/{listId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await owner.Client.PostAsync($"/api/users/{viewer.Id}/block", null)).StatusCode);

        await AssertFailureAsync(
            await viewer.Client.GetAsync($"/api/book-lists/{listId}"),
            HttpStatusCode.NotFound,
            "BOOK_LIST_NOT_FOUND");
        await AssertFailureAsync(
            await viewer.Client.GetAsync($"/api/users/{owner.Id}/book-lists"),
            HttpStatusCode.NotFound,
            "USER_NOT_FOUND");
        Assert.Equal(
            HttpStatusCode.OK,
            (await anonymous.GetAsync($"/api/book-lists/{listId}")).StatusCode);
    }

    private async Task<RegisteredUser> RegisterAsync(string prefix)
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = $"{prefix}-{suffix}@bookspace.local",
                password = "Reader123!",
                displayName = $"{prefix} {suffix[..8]}"
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        var token = data.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Đăng ký không trả access token.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new RegisteredUser(client, data.GetProperty("user").GetProperty("id").GetGuid());
    }

    private static async Task<JsonElement> GetDataAsync(HttpClient client, string endpoint)
    {
        var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task AssertFailureAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private sealed record RegisteredUser(HttpClient Client, Guid Id) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
