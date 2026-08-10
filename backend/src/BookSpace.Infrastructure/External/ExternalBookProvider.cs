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
    private const int MaxResponseBytes = 512 * 1024;
    private const int MaxItems = 20;
    private const int MaxRelatedNames = 20;
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

        using var requestTimeout = CreateRequestTimeout(cancellationToken);
        try
        {
            var boundedLimit = Math.Clamp(limit, 1, MaxItems);
            using var response = await httpClient.GetAsync(
                $"books/search?keyword={Uri.EscapeDataString(query)}&page=0&size={boundedLimit}",
                HttpCompletionOption.ResponseHeadersRead,
                requestTimeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Unavailable("Bookstore hiện không phản hồi thành công.");
            }

            using var document = await ReadDocumentAsync(response.Content, requestTimeout.Token);
            var itemsElement = FindItems(document.RootElement);
            if (itemsElement is null)
            {
                return new ExternalBookSearchResult(true, "bookstore", "Không tìm thấy sách phù hợp.", []);
            }

            var items = itemsElement.Value.EnumerateArray()
                .Take(boundedLimit)
                .Select(ParseBook)
                .Where(x => x is not null)
                .Cast<ExternalBookResult>()
                .ToList();
            return new ExternalBookSearchResult(true, "bookstore", "Đã tải dữ liệu từ Bookstore.", items);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable("Không thể kết nối Bookstore. Bạn vẫn có thể dùng đầy đủ BookSpace.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or IOException or InvalidDataException)
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

        using var requestTimeout = CreateRequestTimeout(cancellationToken);
        try
        {
            using var response = await httpClient.GetAsync(
                $"books/{Uri.EscapeDataString(externalId)}",
                HttpCompletionOption.ResponseHeadersRead,
                requestTimeout.Token);
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

            using var document = await ReadDocumentAsync(response.Content, requestTimeout.Token);
            var itemElement = FindSingleItem(document.RootElement);
            var item = itemElement.HasValue ? ParseBook(itemElement.Value) : null;
            return item is null
                ? new ExternalBookSearchResult(true, "bookstore", "Không tìm thấy sách từ Bookstore.", [])
                : new ExternalBookSearchResult(true, "bookstore", "Đã tải chi tiết sách từ Bookstore.", [item]);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable("Không thể kết nối Bookstore. Bạn vẫn có thể dùng đầy đủ BookSpace.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or IOException or InvalidDataException)
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

    private CancellationTokenSource CreateRequestTimeout(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 30)));
        return source;
    }

    private static JsonElement? FindItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
            {
                return data;
            }

            if (data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("items", out var dataItems) &&
                dataItems.ValueKind == JsonValueKind.Array)
            {
                return dataItems;
            }

            if (data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.Array)
            {
                return content;
            }
        }

        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            return items;
        }

        if (root.TryGetProperty("content", out var rootContent) &&
            rootContent.ValueKind == JsonValueKind.Array)
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
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = GetBoundedString(item, "id", 200);
        var title = GetBoundedString(item, "title", 300) ??
                    GetBoundedString(item, "name", 300);
        if (id is null || title is null)
        {
            return null;
        }

        var authors = new List<string>();
        if (item.TryGetProperty("authors", out var authorsElement) && authorsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var author in authorsElement.EnumerateArray())
            {
                if (authors.Count >= MaxRelatedNames)
                {
                    break;
                }

                var rawName = author.ValueKind == JsonValueKind.String
                    ? author.GetString()
                    : GetString(author, "name");
                var name = NormalizeBounded(rawName, 200);
                if (name is not null && !authors.Contains(name, StringComparer.OrdinalIgnoreCase))
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
        var boundedSingleAuthor = NormalizeBounded(singleAuthor, 200);
        if (boundedSingleAuthor is not null && authors.Count == 0)
        {
            authors.Add(boundedSingleAuthor);
        }

        var categories = new List<string>();
        if (item.TryGetProperty("categories", out var categoriesElement) &&
            categoriesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var category in categoriesElement.EnumerateArray())
            {
                if (categories.Count >= MaxRelatedNames)
                {
                    break;
                }

                var rawName = category.ValueKind == JsonValueKind.String
                    ? category.GetString()
                    : GetString(category, "name");
                var name = NormalizeBounded(rawName, 200);
                if (name is not null && !categories.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    categories.Add(name);
                }
            }
        }

        var pageCount = GetInt32(item, "pageCount") ??
                        GetInt32(item, "pages") ??
                        GetInt32(item, "numberOfPages");
        if (pageCount <= 0)
        {
            pageCount = null;
        }

        var publishedYear = GetInt32(item, "publishedYear") ??
                            GetInt32(item, "publicationYear");
        if (publishedYear is < 1000 or > 2200)
        {
            publishedYear = null;
        }

        var price = GetDecimal(item, "price");
        if (price is < 0 or > 1_000_000_000)
        {
            price = null;
        }

        var purchaseUrl = NormalizeHttpUrl(
            $"{_options.StorefrontUrl.TrimEnd('/')}/books/{Uri.EscapeDataString(id)}",
            1000);

        return new ExternalBookResult(
            id,
            title,
            authors,
            GetBoundedHttpUrl(item, "primaryImageUrl", 1000) ??
            GetBoundedHttpUrl(item, "coverImageUrl", 1000) ??
            GetBoundedHttpUrl(item, "coverUrl", 1000) ??
            GetBoundedHttpUrl(item, "imageUrl", 1000),
            GetBoundedString(item, "isbn", 20),
            GetBoundedString(item, "description", 5000),
            pageCount,
            publishedYear,
            GetBoundedString(item, "language", 20),
            categories,
            price,
            purchaseUrl);
    }

    private static async Task<JsonDocument> ReadDocumentAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaxResponseBytes)
        {
            throw new InvalidDataException("External provider response exceeded the byte limit.");
        }

        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxResponseBytes)
            {
                throw new InvalidDataException("External provider response exceeded the byte limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken);
    }

    private static string? GetBoundedString(
        JsonElement element,
        string property,
        int maxLength) =>
        NormalizeBounded(GetString(element, property), maxLength);

    private static string? GetBoundedHttpUrl(
        JsonElement element,
        string property,
        int maxLength) =>
        NormalizeHttpUrl(GetString(element, property), maxLength);

    private static string? NormalizeBounded(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength
            ? null
            : normalized;
    }

    private static string? NormalizeHttpUrl(string? value, int maxLength)
    {
        var normalized = NormalizeBounded(value, maxLength);
        return normalized is not null &&
               Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? normalized
            : null;
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out var value))
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
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out var value))
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
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out var value))
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
