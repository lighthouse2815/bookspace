using System.Data;
using BookSpace.Application.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public sealed class ChallengeMutationBoundary(BookSpaceDbContext db)
    : IChallengeMutationBoundary
{
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                var connection = (SqliteConnection)db.Database.GetDbConnection();
                await using var sqliteTransaction = await BeginImmediateAsync(
                    connection,
                    cancellationToken);
                await using var transaction = await db.Database.UseTransactionAsync(
                    sqliteTransaction,
                    cancellationToken)
                    ?? throw new InvalidOperationException(
                        "Kh\u00F4ng th\u1EC3 kh\u1EDFi t\u1EA1o giao d\u1ECBch th\u1EED th\u00E1ch.");

                var result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
        catch (DbUpdateException exception) when (
            IsChallengeParticipationUniqueViolation(exception))
        {
            throw new DuplicateChallengeParticipationException(exception);
        }
    }

    private static async Task<SqliteTransaction> BeginImmediateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // deferred: false acquires the SQLite write reservation before
                // eligibility is read, so competing challenge mutations serialize.
                return connection.BeginTransaction(
                    IsolationLevel.Serializable,
                    deferred: false);
            }
            catch (SqliteException exception) when (
                IsSqliteBusy(exception) &&
                attempt < maxAttempts)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(25 * attempt),
                    cancellationToken);
            }
        }
    }

    private static bool IsChallengeParticipationUniqueViolation(
        DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        } sqliteException &&
        sqliteException.Message.Contains(
            "challenge_participations.ChallengeId",
            StringComparison.OrdinalIgnoreCase) &&
        sqliteException.Message.Contains(
            "challenge_participations.UserId",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsSqliteBusy(SqliteException exception) =>
        exception.SqliteErrorCode is 5 or 6;
}
