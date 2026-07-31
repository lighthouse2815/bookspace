using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using BookSpace.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookSpace.IntegrationTests;

public sealed class ChallengeMutationBoundaryTests
{
    [Fact]
    public async Task Admin_mutations_count_physical_participations_hidden_by_soft_delete_filters()
    {
        await using var factory = new BookSpaceApiFactory();
        using var admin = factory.CreateClient();
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");

        Guid publishedChallengeId;
        Guid draftChallengeId;
        Guid activeParticipationId;
        Guid deletedParticipationId;
        Guid readerId;
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var adminId = await db.UserSet
                .Where(x => x.Email == "admin@bookspace.local")
                .Select(x => x.Id)
                .SingleAsync();
            var reader = await db.UserSet
                .SingleAsync(x => x.Email == "reader@bookspace.local");
            var now = DateTimeOffset.UtcNow;
            var publishedChallenge = new ReadingChallenge(
                adminId,
                $"Thử thách có độc giả đã xóa {Guid.NewGuid():N}",
                "Participation vật lý phải chặn việc chuyển về bản nháp.",
                3,
                now.AddHours(-1),
                now.AddHours(1),
                null,
                true);
            var draftChallenge = new ReadingChallenge(
                adminId,
                $"Bản nháp có participation đã xóa {Guid.NewGuid():N}",
                "Participation vật lý phải chặn việc xóa thử thách.",
                3,
                now.AddHours(-1),
                now.AddHours(1),
                null,
                false);
            var activeParticipation =
                new ChallengeParticipation(publishedChallenge.Id, reader.Id);
            var deletedParticipation =
                new ChallengeParticipation(draftChallenge.Id, reader.Id);
            deletedParticipation.SoftDelete();
            reader.SoftDelete();

            publishedChallengeId = publishedChallenge.Id;
            draftChallengeId = draftChallenge.Id;
            activeParticipationId = activeParticipation.Id;
            deletedParticipationId = deletedParticipation.Id;
            readerId = reader.Id;
            db.Add(publishedChallenge);
            db.Add(draftChallenge);
            db.Add(activeParticipation);
            db.Add(deletedParticipation);
            await db.SaveChangesAsync();
        }

        var unpublish = await admin.PatchAsJsonAsync(
            $"/api/admin/challenges/{publishedChallengeId}/publish",
            new { isPublished = false });
        var delete = await admin.DeleteAsync(
            $"/api/admin/challenges/{draftChallengeId}");

        Assert.Equal(HttpStatusCode.Conflict, unpublish.StatusCode);
        Assert.Equal(
            "CHALLENGE_HAS_PARTICIPANTS",
            (await ReadEnvelopeAsync(unpublish)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal(
            "CHALLENGE_HAS_PARTICIPANTS",
            (await ReadEnvelopeAsync(delete)).GetProperty("code").GetString());

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var challenges = await assertDb.ReadingChallengeSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.Id == publishedChallengeId || x.Id == draftChallengeId)
            .ToDictionaryAsync(x => x.Id);
        Assert.True(challenges[publishedChallengeId].IsPublished);
        Assert.Null(challenges[publishedChallengeId].DeletedAt);
        Assert.False(challenges[draftChallengeId].IsPublished);
        Assert.Null(challenges[draftChallengeId].DeletedAt);

        var participations = await assertDb.ChallengeParticipationSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.Id == activeParticipationId ||
                x.Id == deletedParticipationId)
            .ToDictionaryAsync(x => x.Id);
        Assert.Equal(2, participations.Count);
        Assert.Null(participations[activeParticipationId].DeletedAt);
        Assert.NotNull(participations[deletedParticipationId].DeletedAt);
        Assert.NotNull(
            (await assertDb.UserSet
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(x => x.Id == readerId))
            .DeletedAt);
    }

    [Fact]
    public async Task Mutation_boundary_cancels_quickly_while_another_writer_holds_the_lock()
    {
        await using var factory = new BookSpaceApiFactory();
        using var startupClient = factory.CreateClient();

        Guid challengeId;
        await using var blockingScope = factory.Services.CreateAsyncScope();
        var blockingDb =
            blockingScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var adminId = await blockingDb.UserSet
            .Where(x => x.Email == "admin@bookspace.local")
            .Select(x => x.Id)
            .SingleAsync();
        var now = DateTimeOffset.UtcNow;
        var challenge = new ReadingChallenge(
            adminId,
            $"Thử thách kiểm tra hủy lock {Guid.NewGuid():N}",
            "Mutation bị hủy không được commit thay đổi.",
            3,
            now.AddHours(-1),
            now.AddHours(1),
            null,
            true);
        challengeId = challenge.Id;
        blockingDb.Add(challenge);
        await blockingDb.SaveChangesAsync();
        await blockingDb.Database.OpenConnectionAsync();
        var blockingConnection =
            (SqliteConnection)blockingDb.Database.GetDbConnection();
        await using var blockingTransaction = blockingConnection.BeginTransaction(
            IsolationLevel.Serializable,
            deferred: false);

        await using var mutationScope = factory.Services.CreateAsyncScope();
        var mutationDb =
            mutationScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var mutationBoundary = mutationScope.ServiceProvider
            .GetRequiredService<IChallengeMutationBoundary>();
        await mutationDb.Database.OpenConnectionAsync();
        var mutationConnection =
            (SqliteConnection)mutationDb.Database.GetDbConnection();
        const int sentinelBusyTimeoutMilliseconds = 4321;
        await using (var configureBusyTimeout = mutationConnection.CreateCommand())
        {
            configureBusyTimeout.CommandText =
                $"PRAGMA busy_timeout = {sentinelBusyTimeoutMilliseconds};";
            await configureBusyTimeout.ExecuteNonQueryAsync();
        }

        var originalDefaultTimeout = mutationConnection.DefaultTimeout;
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(100));
        var callbackEntered = 0;
        var stopwatch = Stopwatch.StartNew();
        var mutationTask = Task.Run(
            () => mutationBoundary.ExecuteAsync(
                async transactionCancellationToken =>
                {
                    transactionCancellationToken.ThrowIfCancellationRequested();
                    Interlocked.Exchange(ref callbackEntered, 1);
                    var storedChallenge = await mutationDb.ReadingChallengeSet
                        .SingleAsync(
                            x => x.Id == challengeId,
                            transactionCancellationToken);
                    storedChallenge.Unpublish();
                    await mutationDb.SaveChangesAsync(transactionCancellationToken);
                    return true;
                },
                cancellation.Token));

        Exception? observed;
        try
        {
            observed = await Record.ExceptionAsync(
                () => mutationTask.WaitAsync(TimeSpan.FromSeconds(4)));
            stopwatch.Stop();
        }
        finally
        {
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
            }

            await blockingTransaction.RollbackAsync();
        }

        _ = await Record.ExceptionAsync(() => mutationTask);
        Assert.IsAssignableFrom<OperationCanceledException>(observed);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(3),
            $"Boundary mất {stopwatch.Elapsed} để quan sát cancellation.");
        Assert.Equal(0, Volatile.Read(ref callbackEntered));
        Assert.Equal(originalDefaultTimeout, mutationConnection.DefaultTimeout);
        await using (var readBusyTimeout = mutationConnection.CreateCommand())
        {
            readBusyTimeout.CommandText = "PRAGMA busy_timeout;";
            Assert.Equal(
                sentinelBusyTimeoutMilliseconds,
                Convert.ToInt32(await readBusyTimeout.ExecuteScalarAsync()));
        }

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var persisted = await assertDb.ReadingChallengeSet
            .AsNoTracking()
            .SingleAsync(x => x.Id == challengeId);
        Assert.True(persisted.IsPublished);
        Assert.Null(persisted.DeletedAt);
    }

    [Fact]
    public async Task Join_rechecks_eligibility_after_admin_unpublishes_and_deletes()
    {
        var gate = new FirstMutationGate();
        await using var factory = CreateFactory(
            inner => new GatedChallengeMutationBoundary(inner, gate));
        using var admin = factory.CreateClient();
        using var reader = factory.CreateClient();
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");
        await LoginAsync(reader, "reader@bookspace.local", "Reader123!");
        var challengeId = await CreatePublishedChallengeAsync(
            admin,
            "Kiểm tra join sau khi quản trị viên xóa");

        gate.Arm();
        var joinTask = reader.PostAsync($"/api/challenges/{challengeId}/join", null);
        await gate.WaitUntilEnteredAsync();

        HttpResponseMessage? unpublish = null;
        HttpResponseMessage? delete = null;
        try
        {
            unpublish = await admin.PatchAsJsonAsync(
                $"/api/admin/challenges/{challengeId}/publish",
                new { isPublished = false });
            delete = await admin.DeleteAsync($"/api/admin/challenges/{challengeId}");
        }
        finally
        {
            gate.Release();
        }

        var join = await joinTask;
        Assert.NotNull(unpublish);
        Assert.NotNull(delete);
        Assert.Equal(HttpStatusCode.OK, unpublish.StatusCode);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var storedChallenge = await db.ReadingChallengeSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.Id == challengeId);
        Assert.False(storedChallenge.IsPublished);
        Assert.NotNull(storedChallenge.DeletedAt);
        Assert.False(await db.ChallengeParticipationSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x => x.ChallengeId == challengeId));
    }

    [Fact]
    public async Task Admin_unpublish_rechecks_participants_after_join_commits()
    {
        var gate = new FirstMutationGate();
        await using var factory = CreateFactory(
            inner => new GatedChallengeMutationBoundary(inner, gate));
        using var admin = factory.CreateClient();
        using var reader = factory.CreateClient();
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");
        await LoginAsync(reader, "reader@bookspace.local", "Reader123!");
        var challengeId = await CreatePublishedChallengeAsync(
            admin,
            "Kiểm tra quản trị viên sau khi độc giả tham gia");

        gate.Arm();
        var unpublishTask = admin.PatchAsJsonAsync(
            $"/api/admin/challenges/{challengeId}/publish",
            new { isPublished = false });
        await gate.WaitUntilEnteredAsync();

        HttpResponseMessage? join = null;
        try
        {
            join = await reader.PostAsync($"/api/challenges/{challengeId}/join", null);
        }
        finally
        {
            gate.Release();
        }

        var unpublish = await unpublishTask;
        Assert.NotNull(join);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, unpublish.StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await admin.DeleteAsync($"/api/admin/challenges/{challengeId}")).StatusCode);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var readerId = await db.UserSet
            .AsNoTracking()
            .Where(x => x.Email == "reader@bookspace.local")
            .Select(x => x.Id)
            .SingleAsync();
        var storedChallenge = await db.ReadingChallengeSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.Id == challengeId);
        Assert.True(storedChallenge.IsPublished);
        Assert.Null(storedChallenge.DeletedAt);
        Assert.True(await db.ChallengeParticipationSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x => x.ChallengeId == challengeId && x.UserId == readerId));
    }

    [Fact]
    public async Task Join_rolls_back_participation_when_the_use_case_fails_before_commit()
    {
        var failure = new NextMutationFailure();
        await using var factory = CreateFactory(
            inner => new FailingChallengeMutationBoundary(inner, failure));
        using var admin = factory.CreateClient();
        using var reader = factory.CreateClient();
        await LoginAsync(admin, "admin@bookspace.local", "Admin123!");
        await LoginAsync(reader, "reader@bookspace.local", "Reader123!");
        var challengeId = await CreatePublishedChallengeAsync(
            admin,
            "Kiểm tra rollback use case tham gia");

        failure.Arm();
        var response = await reader.PostAsync($"/api/challenges/{challengeId}/join", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var readerId = await db.UserSet
            .AsNoTracking()
            .Where(x => x.Email == "reader@bookspace.local")
            .Select(x => x.Id)
            .SingleAsync();
        Assert.False(await db.ChallengeParticipationSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x => x.ChallengeId == challengeId && x.UserId == readerId));
    }

    private static BookSpaceApiFactory CreateFactory(
        Func<IChallengeMutationBoundary, IChallengeMutationBoundary> decorate)
    {
        return new BookSpaceApiFactory(services =>
        {
            services.RemoveAll<IChallengeMutationBoundary>();
            services.AddScoped<IChallengeMutationBoundary>(provider =>
                decorate(
                    new ChallengeMutationBoundary(
                        provider.GetRequiredService<BookSpaceDbContext>())));
        });
    }

    private static async Task<Guid> CreatePublishedChallengeAsync(
        HttpClient admin,
        string title)
    {
        var create = await admin.PostAsJsonAsync("/api/admin/challenges", new
        {
            title = $"{title} {Guid.NewGuid():N}",
            description = "Kiểm tra invariant transaction của Challenge v2.",
            startDate = DateTimeOffset.UtcNow.AddHours(-1),
            endDate = DateTimeOffset.UtcNow.AddHours(1),
            goalBooks = 3
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var challengeId = (await ReadDataAsync(create)).GetProperty("id").GetGuid();
        var publish = await admin.PatchAsJsonAsync(
            $"/api/admin/challenges/{challengeId}/publish",
            new { isPublished = true });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        return challengeId;
    }

    private static async Task LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            data.GetProperty("accessToken").GetString());
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed class FirstMutationGate
    {
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _armed;

        public void Arm() => Assert.Equal(0, Interlocked.Exchange(ref _armed, 1));

        public async Task WaitUntilEnteredAsync() =>
            await _entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void Release() => _released.TrySetResult();

        public async Task EnterIfArmedAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _armed, 0, 1) != 1)
            {
                return;
            }

            _entered.TrySetResult();
            await _released.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class GatedChallengeMutationBoundary(
        IChallengeMutationBoundary inner,
        FirstMutationGate gate) : IChallengeMutationBoundary
    {
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            await gate.EnterIfArmedAsync(cancellationToken);
            return await inner.ExecuteAsync(operation, cancellationToken);
        }
    }

    private sealed class NextMutationFailure
    {
        private int _armed;

        public void Arm() => Assert.Equal(0, Interlocked.Exchange(ref _armed, 1));

        public bool Consume() => Interlocked.CompareExchange(ref _armed, 0, 1) == 1;
    }

    private sealed class FailingChallengeMutationBoundary(
        IChallengeMutationBoundary inner,
        NextMutationFailure failure) : IChallengeMutationBoundary
    {
        public Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken) =>
            inner.ExecuteAsync(
                async transactionCancellationToken =>
                {
                    var result = await operation(transactionCancellationToken);
                    if (failure.Consume())
                    {
                        throw new InvalidOperationException(
                            "Lỗi giả lập sau use case và trước commit.");
                    }

                    return result;
                },
                cancellationToken);
    }
}
