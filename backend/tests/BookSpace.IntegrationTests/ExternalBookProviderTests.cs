using System.Net;
using System.Text;
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
}
