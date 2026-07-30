namespace BookSpace.Application.Common;

public sealed class UseCaseException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages)
{
    public static PageResult<T> Create(IEnumerable<T> source, int page, int pageSize, long totalItems)
    {
        var safeSize = Math.Clamp(pageSize, 1, 100);
        return new PageResult<T>(
            source.ToList(),
            Math.Max(page, 1),
            safeSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)safeSize));
    }
}

public static class Paging
{
    public static (int Page, int Size, int Skip) Normalize(int page, int pageSize)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);
        return (normalizedPage, normalizedSize, (normalizedPage - 1) * normalizedSize);
    }
}
