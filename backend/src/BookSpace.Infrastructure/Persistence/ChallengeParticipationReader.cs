using BookSpace.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public sealed class ChallengeParticipationReader(BookSpaceDbContext db)
    : IChallengeParticipationReader
{
    public Task<bool> AnyPhysicalForChallengeAsync(
        Guid challengeId,
        CancellationToken cancellationToken) =>
        db.ChallengeParticipationSet
            .IgnoreQueryFilters()
            .AnyAsync(
                x => x.ChallengeId == challengeId,
                cancellationToken);
}
