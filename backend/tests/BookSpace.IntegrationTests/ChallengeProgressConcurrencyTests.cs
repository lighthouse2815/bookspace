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

        var eventKey = ChallengeProgressSynchronizer.CompletionEventKey(readerId, challengeId);
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
