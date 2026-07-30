using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Application.Abstractions;
using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookSpace.IntegrationTests;

public sealed class ChallengeMutationBoundaryTests
{
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
