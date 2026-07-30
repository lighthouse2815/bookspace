namespace BookSpace.Application.Abstractions;

public sealed class DuplicateChallengeParticipationException(
    Exception innerException)
    : Exception("Challenge participation already exists.", innerException);

public interface IChallengeMutationBoundary
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
