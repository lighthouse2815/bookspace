using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Domain.Common;
using BookSpace.Domain.Entities;
using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.IntegrationTests;

public sealed class ChallengeLeaderboardFlowTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    [Fact]
    public async Task Leaderboard_requires_authentication()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync(
            $"/api/challenges/{Guid.NewGuid()}/leaderboard");

        await AssertFailureAsync(response, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Leaderboard_orders_stored_progress_and_keeps_absolute_ranks_across_pages()
    {
        using var viewer = await RegisterAsync("leaderboard-rank-viewer");
        using var earlyFinisher = await RegisterAsync("leaderboard-rank-early");
        using var lateFinisher = await RegisterAsync("leaderboard-rank-late");
        using var fourBooks = await RegisterAsync("leaderboard-rank-four");
        using var tiedLater = await RegisterAsync("leaderboard-rank-tie");
        var baseline = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var challengeId = await AddChallengeAsync(
            viewer.Id,
            isPublished: true,
            [
                new ParticipantSeed(
                    earlyFinisher.Id,
                    5,
                    baseline.AddHours(5),
                    baseline.AddHours(1)),
                new ParticipantSeed(
                    lateFinisher.Id,
                    5,
                    baseline.AddHours(1),
                    baseline.AddHours(2)),
                new ParticipantSeed(fourBooks.Id, 4, baseline.AddHours(2)),
                new ParticipantSeed(
                    viewer.Id,
                    3,
                    baseline.AddHours(3),
                    IsPublic: false),
                new ParticipantSeed(tiedLater.Id, 3, baseline.AddHours(4))
            ]);

        var firstPage = await GetDataAsync(
            viewer.Client,
            $"/api/challenges/{challengeId}/leaderboard?page=1&pageSize=2");
        var firstItems = firstPage.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(5, firstPage.GetProperty("totalItems").GetInt32());
        Assert.Equal(3, firstPage.GetProperty("totalPages").GetInt32());
        Assert.Collection(
            firstItems,
            item =>
            {
                Assert.Equal(1, item.GetProperty("rank").GetInt32());
                Assert.Equal(
                    earlyFinisher.Id,
                    item.GetProperty("user").GetProperty("id").GetGuid());
                Assert.Equal(5, item.GetProperty("currentBooks").GetInt32());
                Assert.Equal(100, item.GetProperty("progressPercent").GetInt32());
                Assert.Equal(
                    baseline.AddHours(1),
                    item.GetProperty("completedAt").GetDateTimeOffset());
            },
            item =>
            {
                Assert.Equal(2, item.GetProperty("rank").GetInt32());
                Assert.Equal(
                    lateFinisher.Id,
                    item.GetProperty("user").GetProperty("id").GetGuid());
            });

        var secondPage = await GetDataAsync(
            viewer.Client,
            $"/api/challenges/{challengeId}/leaderboard?page=2&pageSize=2");
        var secondItems = secondPage.GetProperty("items").EnumerateArray().ToList();
        Assert.Collection(
            secondItems,
            item =>
            {
                Assert.Equal(3, item.GetProperty("rank").GetInt32());
                Assert.Equal(
                    fourBooks.Id,
                    item.GetProperty("user").GetProperty("id").GetGuid());
                Assert.Equal(4, item.GetProperty("currentBooks").GetInt32());
                Assert.Equal(5, item.GetProperty("targetBooks").GetInt32());
                Assert.Equal(80, item.GetProperty("progressPercent").GetInt32());
                Assert.False(item.GetProperty("isCurrentUser").GetBoolean());
            },
            item =>
            {
                Assert.Equal(4, item.GetProperty("rank").GetInt32());
                Assert.Equal(
                    viewer.Id,
                    item.GetProperty("user").GetProperty("id").GetGuid());
                Assert.Equal(3, item.GetProperty("currentBooks").GetInt32());
                Assert.Equal(60, item.GetProperty("progressPercent").GetInt32());
                Assert.True(item.GetProperty("isCurrentUser").GetBoolean());
            });

        var thirdPage = await GetDataAsync(
            viewer.Client,
            $"/api/challenges/{challengeId}/leaderboard?page=3&pageSize=2");
        var finalItem = Assert.Single(thirdPage.GetProperty("items").EnumerateArray());
        Assert.Equal(5, finalItem.GetProperty("rank").GetInt32());
        Assert.Equal(
            tiedLater.Id,
            finalItem.GetProperty("user").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Leaderboard_keeps_private_viewer_self_and_filters_private_locked_and_deleted_users()
    {
        using var viewer = await RegisterAsync("leaderboard-privacy-viewer");
        using var privateUser = await RegisterAsync("leaderboard-privacy-private");
        using var publicUser = await RegisterAsync("leaderboard-privacy-public");
        using var lockedUser = await RegisterAsync("leaderboard-privacy-locked");
        using var deletedUser = await RegisterAsync("leaderboard-privacy-deleted");
        var baseline = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var challengeId = await AddChallengeAsync(
            viewer.Id,
            isPublished: true,
            [
                new ParticipantSeed(viewer.Id, 1, baseline, IsPublic: false),
                new ParticipantSeed(privateUser.Id, 5, baseline.AddMinutes(1), IsPublic: false),
                new ParticipantSeed(publicUser.Id, 2, baseline.AddMinutes(2)),
                new ParticipantSeed(lockedUser.Id, 4, baseline.AddMinutes(3), IsLocked: true),
                new ParticipantSeed(deletedUser.Id, 3, baseline.AddMinutes(4), IsDeleted: true)
            ]);

        var leaderboard = await GetDataAsync(
            viewer.Client,
            $"/api/challenges/{challengeId}/leaderboard");
        var items = leaderboard.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, leaderboard.GetProperty("totalItems").GetInt32());
        Assert.Collection(
            items,
            item =>
            {
                Assert.Equal(1, item.GetProperty("rank").GetInt32());
                Assert.Equal(publicUser.Id, item.GetProperty("user").GetProperty("id").GetGuid());
                Assert.False(item.GetProperty("isCurrentUser").GetBoolean());
            },
            item =>
            {
                Assert.Equal(2, item.GetProperty("rank").GetInt32());
                Assert.Equal(viewer.Id, item.GetProperty("user").GetProperty("id").GetGuid());
                Assert.True(item.GetProperty("isCurrentUser").GetBoolean());
            });
    }

    [Fact]
    public async Task Leaderboard_applies_two_way_blocks_and_only_the_viewers_mutes()
    {
        using var viewer = await RegisterAsync("leaderboard-safety-viewer");
        using var blockedByViewer = await RegisterAsync("leaderboard-safety-blocked");
        using var blocksViewer = await RegisterAsync("leaderboard-safety-blocker");
        using var mutedByViewer = await RegisterAsync("leaderboard-safety-muted");
        using var mutesViewer = await RegisterAsync("leaderboard-safety-reverse-mute");
        using var normal = await RegisterAsync("leaderboard-safety-normal");
        var baseline = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var challengeId = await AddChallengeAsync(
            viewer.Id,
            isPublished: true,
            [
                new ParticipantSeed(viewer.Id, 1, baseline, IsPublic: false),
                new ParticipantSeed(blockedByViewer.Id, 5, baseline.AddMinutes(1)),
                new ParticipantSeed(blocksViewer.Id, 5, baseline.AddMinutes(2)),
                new ParticipantSeed(mutedByViewer.Id, 5, baseline.AddMinutes(3)),
                new ParticipantSeed(mutesViewer.Id, 5, baseline.AddMinutes(4)),
                new ParticipantSeed(normal.Id, 4, baseline.AddMinutes(5))
            ]);
        await AddSafetyRelationsAsync(
            new UserBlock(viewer.Id, blockedByViewer.Id),
            new UserBlock(blocksViewer.Id, viewer.Id),
            new UserMute(viewer.Id, mutedByViewer.Id),
            new UserMute(mutesViewer.Id, viewer.Id));

        var leaderboard = await GetDataAsync(
            viewer.Client,
            $"/api/challenges/{challengeId}/leaderboard");
        var items = leaderboard.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(3, leaderboard.GetProperty("totalItems").GetInt32());
        Assert.Equal(
            [mutesViewer.Id, normal.Id, viewer.Id],
            items.Select(item => item.GetProperty("user").GetProperty("id").GetGuid()).ToList());
        Assert.Equal([1, 2, 3], items.Select(item => item.GetProperty("rank").GetInt32()).ToList());
    }

    [Fact]
    public async Task Leaderboard_cloaks_draft_deleted_and_unknown_challenges_as_not_found()
    {
        using var viewer = await RegisterAsync("leaderboard-not-found-viewer");
        var draftId = await AddChallengeAsync(viewer.Id, isPublished: false, []);
        var deletedId = await AddChallengeAsync(
            viewer.Id,
            isPublished: true,
            [],
            isDeleted: true);

        foreach (var challengeId in new[] { draftId, deletedId, Guid.NewGuid() })
        {
            var response = await viewer.Client.GetAsync(
                $"/api/challenges/{challengeId}/leaderboard");
            await AssertFailureAsync(response, HttpStatusCode.NotFound, "CHALLENGE_NOT_FOUND");
        }
    }

    [Fact]
    public async Task Leaderboard_returns_an_empty_page_for_an_extreme_page_number()
    {
        using var viewer = await RegisterAsync("leaderboard-extreme-page");
        var baseline = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var challengeId = await AddChallengeAsync(
            viewer.Id,
            isPublished: true,
            [new ParticipantSeed(viewer.Id, 1, baseline)]);

        var leaderboard = await GetDataAsync(
            viewer.Client,
            $"/api/challenges/{challengeId}/leaderboard?page={int.MaxValue}&pageSize=100");

        Assert.Equal(int.MaxValue, leaderboard.GetProperty("page").GetInt32());
        Assert.Equal(100, leaderboard.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, leaderboard.GetProperty("totalItems").GetInt32());
        Assert.Empty(leaderboard.GetProperty("items").EnumerateArray());
    }

    private async Task<RegisteredUser> RegisterAsync(string prefix)
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = $"{prefix}-{suffix}@bookspace.local",
                password = "Reader123!",
                displayName = $"{prefix} {suffix[..8]}"
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        var token = data.GetProperty("accessToken").GetString()
                    ?? throw new InvalidOperationException("Register did not return an access token.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new RegisteredUser(
            client,
            data.GetProperty("user").GetProperty("id").GetGuid());
    }

    private async Task<Guid> AddChallengeAsync(
        Guid creatorId,
        bool isPublished,
        IReadOnlyList<ParticipantSeed> participants,
        bool isDeleted = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var challenge = new ReadingChallenge(
            creatorId,
            $"Leaderboard {Guid.NewGuid():N}",
            "Challenge leaderboard integration test.",
            5,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            null,
            isPublished);
        if (isDeleted)
        {
            challenge.SoftDelete();
        }

        db.Add(challenge);
        foreach (var seed in participants)
        {
            var user = await db.UserSet
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == seed.UserId);
            user.UpdatePublicReadingVisibility(false, seed.IsPublic);
            if (seed.IsLocked)
            {
                user.Lock();
            }

            if (seed.IsDeleted)
            {
                user.SoftDelete();
            }

            var participation = new ChallengeParticipation(challenge.Id, user.Id);
            participation.UpdateProgress(seed.CurrentBooks, challenge.TargetBooks);
            SetProperty(participation, nameof(Entity.CreatedAt), seed.JoinedAt);
            if (seed.CompletedAt.HasValue)
            {
                SetProperty(
                    participation,
                    nameof(ChallengeParticipation.CompletedAt),
                    seed.CompletedAt.Value);
            }

            db.Add(participation);
        }

        await db.SaveChangesAsync();
        return challenge.Id;
    }

    private async Task AddSafetyRelationsAsync(params Entity[] relations)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        foreach (var relation in relations)
        {
            db.Add(relation);
        }

        await db.SaveChangesAsync();
    }

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName)
                       ?? throw new InvalidOperationException($"Missing property {propertyName}.");
        property.SetValue(target, value);
    }

    private static async Task<JsonElement> GetDataAsync(HttpClient client, string endpoint)
    {
        var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task AssertFailureAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private sealed record RegisteredUser(HttpClient Client, Guid Id) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record ParticipantSeed(
        Guid UserId,
        int CurrentBooks,
        DateTimeOffset JoinedAt,
        DateTimeOffset? CompletedAt = null,
        bool IsPublic = true,
        bool IsLocked = false,
        bool IsDeleted = false);
}
