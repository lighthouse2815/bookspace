using System.Data;
using System.Globalization;
using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public sealed class ChallengeMutationBoundary(BookSpaceDbContext db)
    : IChallengeMutationBoundary, IReadingMutationBoundary, IClubChatMutationBoundary
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
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Any(entry => entry.Entity is ActiveReadingSession))
        {
            throw new UseCaseException(
                "ACTIVE_READING_SESSION_CHANGED",
                "Phiên đọc tập trung vừa thay đổi. Vui lòng thử lại.",
                409);
        }
        catch (DbUpdateException exception) when (
            IsChallengeParticipationUniqueViolation(exception))
        {
            throw new DuplicateChallengeParticipationException(exception);
        }
        catch (DbUpdateException exception) when (
            IsActiveReadingSessionUniqueViolation(exception))
        {
            throw new UseCaseException(
                "ACTIVE_READING_SESSION_EXISTS",
                "Bạn đang có một phiên đọc tập trung chưa hoàn tất.",
                409);
        }
    }

    private static async Task<SqliteTransaction> BeginImmediateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 4;
        const int attemptTimeoutSeconds = 1;
        cancellationToken.ThrowIfCancellationRequested();
        var originalTimeout = connection.DefaultTimeout;
        var originalBusyTimeout = ReadBusyTimeoutMilliseconds(connection);
        SqliteTransaction? acquiredTransaction = null;
        try
        {
            // DefaultTimeout=0 means no timeout in Microsoft.Data.Sqlite.
            // One second is the shortest public provider timeout; clearing the
            // native busy handler prevents it from adding another blocking wait.
            connection.DefaultTimeout = attemptTimeoutSeconds;
            WriteBusyTimeoutMilliseconds(connection, 0);
            for (var attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // Microsoft.Data.Sqlite transaction acquisition is synchronous.
                    // Bound each provider retry window, then use cancellable backoff.
                    var transaction = connection.BeginTransaction(
                        IsolationLevel.Serializable,
                        deferred: false);
                    acquiredTransaction = transaction;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        transaction.Dispose();
                        acquiredTransaction = null;
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    return transaction;
                }
                catch (SqliteException exception) when (IsSqliteBusy(exception))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (attempt >= maxAttempts)
                    {
                        throw;
                    }

                    await Task.Delay(
                        TimeSpan.FromMilliseconds(25 * attempt),
                        cancellationToken);
                }
            }
        }
        finally
        {
            connection.DefaultTimeout = originalTimeout;
            try
            {
                WriteBusyTimeoutMilliseconds(connection, originalBusyTimeout);
            }
            catch
            {
                try
                {
                    acquiredTransaction?.Dispose();
                }
                catch
                {
                    // Preserve the PRAGMA restoration failure.
                }

                // PRAGMA state belongs to the pooled native handle. Do not let a
                // connection with a changed busy handler return to the pool.
                SqliteConnection.ClearPool(connection);
                throw;
            }
        }
    }

    private static int ReadBusyTimeoutMilliseconds(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout;";
        return Convert.ToInt32(
            command.ExecuteScalar(),
            CultureInfo.InvariantCulture);
    }

    private static void WriteBusyTimeoutMilliseconds(
        SqliteConnection connection,
        int milliseconds)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            $"PRAGMA busy_timeout = {milliseconds.ToString(CultureInfo.InvariantCulture)};";
        command.ExecuteNonQuery();
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

    private static bool IsActiveReadingSessionUniqueViolation(
        DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        } sqliteException &&
        sqliteException.Message.Contains(
            "active_reading_sessions.UserId",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsSqliteBusy(SqliteException exception) =>
        exception.SqliteErrorCode is 5 or 6;
}
