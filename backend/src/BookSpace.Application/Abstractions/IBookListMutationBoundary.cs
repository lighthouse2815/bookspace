namespace BookSpace.Application.Abstractions;

public interface IBookListMutationBoundary
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
