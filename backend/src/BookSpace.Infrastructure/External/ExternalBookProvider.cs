using System.Globalization;
using System.Text.Json;
using BookSpace.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace BookSpace.Infrastructure.External;

public sealed class BookstoreIntegrationOptions
{
    public const string SectionName = "BookstoreIntegration";
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = "http://localhost:8080/api";
    public string StorefrontUrl { get; init; } = "http://localhost:5173";
    public int TimeoutSeconds { get; init; } = 5;
}

public sealed class ExternalBookProvider(
    HttpClient httpClient,
    IOptions<BookstoreIntegrationOptions> options) : IExternalBookProvider
{
    private readonly BookstoreIntegrationOptions _options = options.Value;

    public async Task<ExternalBookSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new ExternalBookSearchResult(
                false,
                "bookstore",
                "Kết nối Bookstore đang tắt. BookSpace vẫn hoạt động độc lập.",
                []);
        }

        try
        {
            using var response = await httpClient.GetAsync(
                $"books/search?keyword={Uri.EscapeDataString(query)}&page=0&size={limit}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Unavailable("Bookstore hiện không phản hồi thành công.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var itemsElement = FindItems(document.RootElement);
            if (itemsElement is null)
            {
                return new ExternalBookSearchResult(true, "bookstore", "Không tìm thấy sách phù hợp.", []);
            }

            var items = itemsElement.Value.EnumerateArray()
                .Take(limit)
                .Select(ParseBook)
                .Where(x => x is not null)
                .Cast<ExternalBookResult>()
                .ToList();
            return new ExternalBookSearchResult(true, "bookstore", "Đã tải dữ liệu từ Bookstore.", items);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Unavailable("Không thể kết nối Bookstore. Bạn vẫn có thể dùng đầy đủ BookSpace.");
        }
    }

    public async Task<ExternalBookSearchResult> GetByIdAsync(
        string externalId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Disabled();
        }

        try
        {
            using var response = await httpClient.GetAsync(
                $"books/{Uri.EscapeDataString(externalId)}",
                cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new ExternalBookSearchResult(
                    true,
                    "bookstore",
                    "Không tìm thấy sách từ Bookstore.",
                    []);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Unavailable("Bookstore hiện không phản hồi thành công.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var itemElement = FindSingleItem(document.RootElement);
            var item = itemElement.HasValue ? ParseBook(itemElement.Value) : null;
            return item is null
                ? new ExternalBookSearchResult(true, "bookstore", "Không tìm thấy sách từ Bookstore.", [])
                : new ExternalBookSearchResult(true, "bookstore", "Đã tải chi tiết sách từ Bookstore.", [item]);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Unavailable("Không thể kết nối Bookstore. Bạn vẫn có thể dùng đầy đủ BookSpace.");
        }
    }

    private ExternalBookSearchResult Disabled() =>
        new(
            false,
            "bookstore",
            "Kết nối Bookstore đang tắt. BookSpace vẫn hoạt động độc lập.",
            []);

    private ExternalBookSearchResult Unavailable(string message) =>
        new(false, "bookstore", message, []);

    private static JsonElement? FindItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
            {
                return data;
            }

            if (data.TryGetProperty("items", out var dataItems))
            {
                return dataItems;
            }

            if (data.TryGetProperty("content", out var content))
            {
                return content;
            }
        }

        if (root.TryGetProperty("items", out var items))
        {
            return items;
        }

        if (root.TryGetProperty("content", out var rootContent))
        {
            return rootContent;
        }

        return null;
    }

    private static JsonElement? FindSingleItem(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Object)
            {
                return data;
            }

            if (data.ValueKind == JsonValueKind.Array)
            {
                return data.EnumerateArray().Select(item => (JsonElement?)item).FirstOrDefault();
            }
        }

        return root.TryGetProperty("id", out _) ? root : null;
    }

    private ExternalBookResult? ParseBook(JsonElement item)
    {
        var id = GetString(item, "id");
        var title = GetString(item, "title") ?? GetString(item, "name");
        if (id is null || title is null)
        {
            return null;
        }

        var authors = new List<string>();
        if (item.TryGetProperty("authors", out var authorsElement) && authorsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var author in authorsElement.EnumerateArray())
            {
                var name = author.ValueKind == JsonValueKind.String
                    ? author.GetString()
                    : GetString(author, "name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    authors.Add(name);
                }
            }
        }

        var singleAuthor = item.TryGetProperty("author", out var authorElement)
            ? authorElement.ValueKind == JsonValueKind.String
                ? authorElement.GetString()
                : GetString(authorElement, "name")
            : null;
        if (!string.IsNullOrWhiteSpace(singleAuthor) && authors.Count == 0)
        {
            authors.Add(singleAuthor);
        }

        var categories = new List<string>();
        if (item.TryGetProperty("categories", out var categoriesElement) &&
            categoriesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var category in categoriesElement.EnumerateArray())
            {
                var name = category.ValueKind == JsonValueKind.String
                    ? category.GetString()
                    : GetString(category, "name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    categories.Add(name);
                }
            }
        }

        return new ExternalBookResult(
            id,
            title,
            authors,
            GetString(item, "primaryImageUrl") ??
            GetString(item, "coverImageUrl") ??
            GetString(item, "coverUrl") ??
            GetString(item, "imageUrl"),
            GetString(item, "isbn"),
            GetString(item, "description"),
            GetInt32(item, "pageCount") ??
            GetInt32(item, "pages") ??
            GetInt32(item, "numberOfPages"),
            GetInt32(item, "publishedYear") ?? GetInt32(item, "publicationYear"),
            GetString(item, "language"),
            categories,
            GetDecimal(item, "price"),
            $"{_options.StorefrontUrl.TrimEnd('/')}/books/{id}");
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static decimal? GetDecimal(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int? GetInt32(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
