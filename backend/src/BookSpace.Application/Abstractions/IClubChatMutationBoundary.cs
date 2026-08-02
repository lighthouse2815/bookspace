namespace BookSpace.Application.Abstractions;

public interface IClubChatMutationBoundary
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
