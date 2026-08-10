using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using BookSpace.Infrastructure.External;
using Microsoft.Extensions.Options;

namespace BookSpace.IntegrationTests;

public sealed class ExternalBookProviderTests
{
    [Fact]
    public async Task Disabled_provider_returns_a_controlled_result_without_outbound_request()
    {
        var handler = new RecordingHandler("{}");
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bookstore.test/api/")
        };
        var provider = new ExternalBookProvider(
            client,
            Options.Create(new BookstoreIntegrationOptions { Enabled = false }));

        var result = await provider.SearchAsync("clean code", 5, CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("bookstore", result.Provider);
        Assert.Empty(result.Items);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Enabled_provider_maps_the_bookstore_search_envelope_to_the_public_contract()
    {
        const string payload = """
            {
              "success": true,
              "data": [
                {
                  "id": "bookstore-book-1",
                  "title": "Clean Code",
                  "authors": [{ "name": "Robert C. Martin" }],
                  "categories": [{ "name": "Software Engineering" }],
                  "imageUrl": "https://images.example.test/clean-code.jpg",
                  "isbn": "9780132350884",
                  "description": "A handbook of agile software craftsmanship.",
                  "pageCount": 464,
                  "publishedYear": 2008,
                  "language": "en",
                  "price": 180000
                }
              ]
            }
            """;
        var handler = new RecordingHandler(payload);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bookstore.test/api/")
        };
        var provider = new ExternalBookProvider(
            client,
            Options.Create(new BookstoreIntegrationOptions
            {
                Enabled = true,
                StorefrontUrl = "https://store.example"
            }));

        var result = await provider.SearchAsync("clean code", 5, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(result.Available);
        Assert.Equal("/api/books/search?keyword=clean%20code&page=0&size=5", handler.LastRequestUri?.PathAndQuery);
        Assert.Equal("bookstore-book-1", item.ExternalId);
        Assert.Equal("Clean Code", item.Title);
        Assert.Equal(["Robert C. Martin"], item.Authors);
        Assert.Equal("https://images.example.test/clean-code.jpg", item.CoverImageUrl);
        Assert.Equal("9780132350884", item.Isbn);
        Assert.Equal("A handbook of agile software craftsmanship.", item.Description);
        Assert.Equal(464, item.PageCount);
        Assert.Equal(2008, item.PublishedYear);
        Assert.Equal("en", item.Language);
        Assert.Equal(["Software Engineering"], item.Categories);
        Assert.Equal(180000m, item.Price);
        Assert.Equal("https://store.example/books/bookstore-book-1", item.PurchaseUrl);
    }

    [Fact]
    public async Task Enabled_provider_loads_a_single_book_detail_by_external_id()
    {
        const string payload = """
            {
              "success": true,
              "data": {
                "id": "book/detail 1",
                "title": "Refactoring",
                "author": { "name": "Martin Fowler" },
                "pageCount": "448"
              }
            }
            """;
        var handler = new RecordingHandler(payload);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bookstore.test/api/")
        };
        var provider = new ExternalBookProvider(
            client,
            Options.Create(new BookstoreIntegrationOptions { Enabled = true }));

        var result = await provider.GetByIdAsync("book/detail 1", CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(result.Available);
        Assert.Equal("/api/books/book%2Fdetail%201", handler.LastRequestUri?.PathAndQuery);
        Assert.Equal("Refactoring", item.Title);
        Assert.Equal(["Martin Fowler"], item.Authors);
        Assert.Equal(448, item.PageCount);
    }

    [Fact]
    public async Task Provider_timeout_returns_a_bounded_unavailable_result()
    {
        using var client = new HttpClient(new NeverCompletingHandler())
        {
            BaseAddress = new Uri("https://bookstore.test/api/"),
            Timeout = TimeSpan.FromMilliseconds(100)
        };
        var provider = new ExternalBookProvider(
            client,
            Options.Create(new BookstoreIntegrationOptions { Enabled = true }));
        var stopwatch = Stopwatch.StartNew();

        var result = await provider.SearchAsync("slow book", 5, CancellationToken.None);

        Assert.False(result.Available);
        Assert.Empty(result.Items);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Provider_timeout_also_bounds_a_body_that_stalls_after_headers()
    {
        using var client = new HttpClient(new HeadersThenStallingHandler())
        {
            BaseAddress = new Uri("https://bookstore.test/api/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        var provider = new ExternalBookProvider(
            client,
            Options.Create(new BookstoreIntegrationOptions
            {
                Enabled = true,
                TimeoutSeconds = 1
            }));
        var stopwatch = Stopwatch.StartNew();

        var result = await provider.SearchAsync("slow body", 5, CancellationToken.None);

        Assert.False(result.Available);
        Assert.Empty(result.Items);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3));
    }

    [Theory]
    [InlineData("not-json", false)]
    [InlineData("42", true)]
    [InlineData("{\"data\":\"unexpected\"}", true)]
    [InlineData("{\"data\":{\"items\":[null,7,\"bad\",{\"id\":\"missing-title\"}]}}", true)]
    public async Task Invalid_or_unrecognized_payload_never_leaks_garbage(
        string payload,
        bool expectedAvailable)
    {
        var handler = new RecordingHandler(payload);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bookstore.test/api/")
        };
        var provider = new ExternalBookProvider(
            client,
            Options.Create(new BookstoreIntegrationOptions { Enabled = true }));

        var result = await provider.SearchAsync("unknown shape", 3, CancellationToken.None);

        Assert.Equal(expectedAvailable, result.Available);
        Assert.Empty(result.Items);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Required_fields_are_trimmed_and_invalid_required_values_are_rejected()
    {
        var payload = JsonSerializer.Serialize(new
        {
            data = new object[]
            {
                new { id = "   ", title = "Valid title" },
                new { id = "valid-id", title = new string('t', 301) },
                new { id = "  trimmed-id  ", title = "  Trimmed title  " }
            }
        });
        var handler = new RecordingHandler(payload);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bookstore.test/api/")
        };
        var provider = new ExternalBookProvider(
            client,
            Options.Create(new BookstoreIntegrationOptions { Enabled = true }));

        var result = await provider.SearchAsync("bounded", 99, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("trimmed-id", item.ExternalId);
        Assert.Equal("Trimmed title", item.Title);
        Assert.EndsWith("size=20", handler.LastRequestUri?.Query);
    }

    [Fact]
    public async Task Optional_fields_and_related_arrays_are_bounded_before_mapping()
    {
        var payload = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new
                {
                    id = "bounded-book",
                    title = "Bounded Book",
                    authors = Enumerable.Range(1, 30).Select(index => new { name = $"Author {index}" }),
                    categories = Enumerable.Range(1, 30).Select(index => new { name = $"Category {index}" }),
                    description = new string('d', 5001),
                    imageUrl = "javascript:alert(1)",
                    isbn = new string('1', 21),
                    language = new string('l', 21),
                    pageCount = -1,
                    publishedYear = 999,
                    price = -1
                }
            }
        });
        var handler = new RecordingHandler(payload);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bookstore.test/api/")
        };
        var provider = new ExternalBookProvider(
            client,
            Options.Create(new BookstoreIntegrationOptions { Enabled = true }));

        var result = await provider.SearchAsync("bounded", 5, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(20, item.Authors.Count);
        Assert.Equal(20, item.Categories.Count);
        Assert.Null(item.Description);
        Assert.Null(item.CoverImageUrl);
        Assert.Null(item.Isbn);
        Assert.Null(item.Language);
        Assert.Null(item.PageCount);
        Assert.Null(item.PublishedYear);
        Assert.Null(item.Price);
    }

    [Fact]
    public async Task Oversized_response_is_rejected_without_parsing_or_partial_items()
    {
        var payload = JsonSerializer.Serialize(new
        {
            data = Array.Empty<object>(),
            padding = new string('x', 600_000)
        });
        var handler = new RecordingHandler(payload);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://bookstore.test/api/")
        };
        var provider = new ExternalBookProvider(
            client,
            Options.Create(new BookstoreIntegrationOptions { Enabled = true }));

        var result = await provider.SearchAsync("oversized", 5, CancellationToken.None);

        Assert.False(result.Available);
        Assert.Empty(result.Items);
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class NeverCompletingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }
    }

    private sealed class HeadersThenStallingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new StallingStream())
            });
    }

    private sealed class StallingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }
    }
}
