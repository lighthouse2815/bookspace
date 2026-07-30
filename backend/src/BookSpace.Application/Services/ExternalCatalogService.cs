using BookSpace.Application.Abstractions;

namespace BookSpace.Application.Services;

public sealed class ExternalCatalogService(IExternalBookProvider provider) : IExternalCatalogService
{
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

        return provider.SearchAsync(query.Trim(), Math.Clamp(limit, 1, 50), cancellationToken);
    }
}
