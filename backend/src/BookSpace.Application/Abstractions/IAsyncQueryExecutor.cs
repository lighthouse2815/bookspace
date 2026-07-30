namespace BookSpace.Application.Abstractions;

public interface IAsyncQueryExecutor
{
    Task<List<T>> ToListAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken);

    Task<T?> FirstOrDefaultAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken);

    Task<bool> AnyAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken);
}
