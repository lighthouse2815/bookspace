namespace BookSpace.Application.Abstractions;

public interface IChallengeParticipationReader
{
    Task<bool> AnyPhysicalForChallengeAsync(
        Guid challengeId,
        CancellationToken cancellationToken);
}
