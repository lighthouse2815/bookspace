namespace BookSpace.Application.Abstractions;

public interface IDirectMessageMutationBoundary
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
