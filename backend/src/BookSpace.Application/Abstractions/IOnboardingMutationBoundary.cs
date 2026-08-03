namespace BookSpace.Application.Abstractions;

public interface IOnboardingMutationBoundary
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
