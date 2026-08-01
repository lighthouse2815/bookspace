using BookSpace.Domain.Entities;

namespace BookSpace.Application.Abstractions;

public interface IFollowMutationBoundary
{
    Task<bool> TryCreateAsync(
        Follow follow,
        Notification? notification,
        CancellationToken cancellationToken);
}
