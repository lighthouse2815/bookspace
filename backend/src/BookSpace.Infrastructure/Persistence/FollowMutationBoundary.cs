using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public sealed class FollowMutationBoundary(BookSpaceDbContext db) : IFollowMutationBoundary
{
    private const string FollowUniqueConstraint =
        "UNIQUE constraint failed: follows.FollowerId, follows.FollowingId";

    public async Task<bool> TryCreateAsync(
        Follow follow,
        Notification? notification,
        CancellationToken cancellationToken)
    {
        db.Add(follow);
        if (notification is not null)
        {
            db.Add(notification);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsDuplicateFollow(exception))
        {
            return false;
        }
    }

    private static bool IsDuplicateFollow(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        } sqliteException &&
        sqliteException.Message.Contains(FollowUniqueConstraint, StringComparison.Ordinal);
}
