using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.IntegrationTests;

public sealed class ChallengeProgressConcurrencyTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    [Fact]
    public async Task Mutation_transaction_rolls_back_participation_when_sync_fails()
    {
        Guid challengeId;
        Guid readerId;
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            readerId = await db.UserSet
                .Where(x => x.Email == "reader@bookspace.local")
                .Select(x => x.Id)
                .SingleAsync();
            var adminId = await db.UserSet
                .Where(x => x.Email == "admin@bookspace.local")
                .Select(x => x.Id)
                .SingleAsync();
            var now = DateTimeOffset.UtcNow;
            var challenge = new ReadingChallenge(
                adminId,
                $"Thử thách rollback {Guid.NewGuid():N}",
                "Kiểm tra participation không được commit trước sync.",
                2,
                now.AddHours(-1),
                now.AddHours(1),
                null,
                true);
            challengeId = challenge.Id;
            db.Add(challenge);
            await db.SaveChangesAsync();
        }

        await using (var mutationScope = factory.Services.CreateAsyncScope())
        {
            var db = mutationScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var mutationBoundary = mutationScope.ServiceProvider
                .GetRequiredService<IChallengeMutationBoundary>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => mutationBoundary.ExecuteAsync<bool>(
                    async cancellationToken =>
                    {
                        db.Add(new ChallengeParticipation(challengeId, readerId));
                        await db.SaveChangesAsync(cancellationToken);
                        throw new InvalidOperationException("Lỗi đồng bộ giả lập.");
                    },
                    CancellationToken.None));
        }

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        Assert.False(await assertDb.ChallengeParticipationSet
            .AsNoTracking()
            .AnyAsync(x => x.ChallengeId == challengeId && x.UserId == readerId));
    }

    [Fact]
    public async Task Atomic_high_water_does_not_downgrade_for_a_stale_low_candidate()
    {
        Guid participationId;
        var completedAt = DateTimeOffset.UtcNow;
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var readerId = await db.UserSet
                .Where(x => x.Email == "reader@bookspace.local")
                .Select(x => x.Id)
                .SingleAsync();
            var adminId = await db.UserSet
                .Where(x => x.Email == "admin@bookspace.local")
                .Select(x => x.Id)
                .SingleAsync();
            var challenge = new ReadingChallenge(
                adminId,
                $"Thử thách high-water {Guid.NewGuid():N}",
                "Kiểm tra candidate cũ không thể ghi lùi tiến độ.",
                2,
                completedAt.AddHours(-1),
                completedAt.AddHours(1),
                null,
                true);
            var participation = new ChallengeParticipation(challenge.Id, readerId);
            participationId = participation.Id;
            db.Add(challenge);
            db.Add(participation);
            await db.SaveChangesAsync();
        }

        await using (var highScope = factory.Services.CreateAsyncScope())
        {
            var persistence = highScope.ServiceProvider
                .GetRequiredService<IChallengeProgressPersistence>();
            var highResult = await persistence.AdvanceHighWaterAsync(
                participationId,
                3,
                2,
                completedAt,
                completedAt,
                CancellationToken.None);

            Assert.NotNull(highResult);
            Assert.Equal(2, highResult.CompletedBooks);
            Assert.Equal(completedAt, highResult.CompletedAt);
        }

        await using (var staleScope = factory.Services.CreateAsyncScope())
        {
            var persistence = staleScope.ServiceProvider
                .GetRequiredService<IChallengeProgressPersistence>();
            var staleResult = await persistence.AdvanceHighWaterAsync(
                participationId,
                1,
                2,
                completedAt.AddMinutes(1),
                completedAt.AddMinutes(1),
                CancellationToken.None);

            Assert.NotNull(staleResult);
            Assert.Equal(2, staleResult.CompletedBooks);
            Assert.Equal(completedAt, staleResult.CompletedAt);
        }

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var stored = await assertDb.ChallengeParticipationSet
            .AsNoTracking()
            .SingleAsync(x => x.Id == participationId);
        Assert.Equal(2, stored.CompletedBooks);
        Assert.Equal(completedAt, stored.CompletedAt);
    }

    [Fact]
    public async Task Atomic_high_water_repairs_missing_completion_at_when_database_is_at_target()
    {
        Guid participationId;
        var completedAt = DateTimeOffset.UtcNow;
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var readerId = await db.UserSet
                .Where(x => x.Email == "reader@bookspace.local")
                .Select(x => x.Id)
                .SingleAsync();
            var adminId = await db.UserSet
                .Where(x => x.Email == "admin@bookspace.local")
                .Select(x => x.Id)
                .SingleAsync();
            var challenge = new ReadingChallenge(
                adminId,
                $"Thử thách sửa completion {Guid.NewGuid():N}",
                "Kiểm tra trạng thái đạt mục tiêu nhưng thiếu thời điểm hoàn thành.",
                2,
                completedAt.AddHours(-1),
                completedAt.AddHours(1),
                null,
                true);
            var participation = new ChallengeParticipation(challenge.Id, readerId);
            participationId = participation.Id;
            db.Add(challenge);
            db.Add(participation);
            await db.SaveChangesAsync();
            await db.ChallengeParticipationSet
                .Where(x => x.Id == participationId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.CompletedBooks, 2));
        }

        await using (var repairScope = factory.Services.CreateAsyncScope())
        {
            var persistence = repairScope.ServiceProvider
                .GetRequiredService<IChallengeProgressPersistence>();
            var repaired = await persistence.AdvanceHighWaterAsync(
                participationId,
                1,
                2,
                completedAt,
                completedAt,
                CancellationToken.None);

            Assert.NotNull(repaired);
            Assert.Equal(2, repaired.CompletedBooks);
            Assert.Equal(completedAt, repaired.CompletedAt);
        }

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var stored = await assertDb.ChallengeParticipationSet
            .AsNoTracking()
            .SingleAsync(x => x.Id == participationId);
        Assert.Equal(2, stored.CompletedBooks);
        Assert.Equal(completedAt, stored.CompletedAt);
    }

    [Fact]
    public async Task Notification_deduplication_key_is_enforced_by_database()
    {
        Guid readerId;
        var eventKey = $"challenge-completed:test:{Guid.NewGuid():N}";
        await using (var firstScope = factory.Services.CreateAsyncScope())
        {
            var db = firstScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            readerId = await db.UserSet
                .Where(x => x.Email == "reader@bookspace.local")
                .Select(x => x.Id)
                .SingleAsync();
            db.Add(new Notification(
                readerId,
                NotificationType.CHALLENGE,
                "Hoàn thành thử thách",
                "Thông báo kiểm tra ràng buộc chống trùng.",
                "/challenges/test",
                eventKey));
            await db.SaveChangesAsync();
        }

        await using (var duplicateScope = factory.Services.CreateAsyncScope())
        {
            var db = duplicateScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            db.Add(new Notification(
                readerId,
                NotificationType.CHALLENGE,
                "Hoàn thành thử thách",
                "Thông báo trùng phải bị cơ sở dữ liệu từ chối.",
                "/challenges/test",
                eventKey));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => db.SaveChangesAsync());
        }

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        Assert.Equal(
            1,
            await assertDb.NotificationSet
                .AsNoTracking()
                .CountAsync(x => x.DeduplicationKey == eventKey));
    }

    [Fact]
    public async Task Concurrent_sync_uses_atomic_max_and_creates_one_completion_event()
    {
        Guid challengeId;
        Guid readerId;
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            readerId = await db.UserSet
                .Where(x => x.Email == "reader@bookspace.local")
                .Select(x => x.Id)
                .SingleAsync();
            var adminId = await db.UserSet
                .Where(x => x.Email == "admin@bookspace.local")
                .Select(x => x.Id)
                .SingleAsync();
            var unreadBookId = await db.BookSet
                .Where(book => !db.LibraryItemSet.Any(item =>
                    item.UserId == readerId && item.BookId == book.Id))
                .Select(x => x.Id)
                .FirstAsync();
            var now = DateTimeOffset.UtcNow;
            var challenge = new ReadingChallenge(
                adminId,
                $"Thử thách đồng thời {Guid.NewGuid():N}",
                "Kiểm tra atomic max và event key.",
                1,
                now,
                now.AddDays(1),
                null,
                true);
            challengeId = challenge.Id;
            db.Add(challenge);
            db.Add(new ChallengeParticipation(challengeId, readerId));
            db.Add(new LibraryItem(readerId, unreadBookId, LibraryStatus.READ));
            await db.SaveChangesAsync();
        }

        var waiting = 0;
        var bothReady = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        async Task SyncFromIndependentScopeAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var synchronizer =
                scope.ServiceProvider.GetRequiredService<IChallengeProgressSynchronizer>();
            if (Interlocked.Increment(ref waiting) == 2)
            {
                bothReady.SetResult();
            }

            await bothReady.Task;
            await synchronizer.SyncAsync(readerId, CancellationToken.None);
        }

        await Task.WhenAll(
            SyncFromIndependentScopeAsync(),
            SyncFromIndependentScopeAsync());

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var participation = await assertDb.ChallengeParticipationSet
            .AsNoTracking()
            .SingleAsync(x => x.ChallengeId == challengeId && x.UserId == readerId);
        Assert.Equal(1, participation.CompletedBooks);
        Assert.NotNull(participation.CompletedAt);

        var eventKey = $"challenge-completed:{challengeId:N}:{readerId:N}";
        Assert.Equal(
            1,
            await assertDb.NotificationSet
                .AsNoTracking()
                .CountAsync(x => x.DeduplicationKey == eventKey));
    }
}

public sealed class SeedChallengeProgressTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    [Fact]
    public async Task Fresh_seed_progress_matches_real_read_library_items()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var participation = await db.ChallengeParticipationSet
            .AsNoTracking()
            .Include(x => x.Challenge)
            .SingleAsync(x => x.Challenge.Title == "12 cuốn sách Việt trong năm");

        Assert.Equal(1, participation.CompletedBooks);
    }
}
