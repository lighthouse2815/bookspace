using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.IntegrationTests;

public sealed class PeopleDiscoveryFlowTests
{
    [Fact]
    public async Task Public_search_uses_display_name_only_and_hides_unavailable_users_and_private_fields()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var emailOnly = new User(
                "private-search-marker@bookspace.local",
                "hash",
                "Tên hiển thị bình thường");
            var locked = new User("locked@bookspace.local", "hash", "Locked Reader");
            locked.Lock();
            var deleted = new User("deleted@bookspace.local", "hash", "Deleted Reader");
            deleted.SoftDelete();
            db.AddRange(emailOnly, locked, deleted);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/users?search=%20mINH%20aNH%20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        var result = Assert.Single(data.GetProperty("items").EnumerateArray());
        Assert.Equal("Minh Anh", result.GetProperty("displayName").GetString());
        Assert.False(result.TryGetProperty("email", out _));
        Assert.False(result.TryGetProperty("passwordHash", out _));
        Assert.False(result.GetProperty("isFollowing").GetBoolean());
        Assert.False(result.GetProperty("followsYou").GetBoolean());
        Assert.Equal(0, result.GetProperty("mutualFollowCount").GetInt32());
        Assert.Equal("SEARCH_MATCH", result.GetProperty("reason").GetString());
        Assert.Equal(
            "Phù hợp với tên hiển thị bạn đang tìm.",
            result.GetProperty("reasonText").GetString());

        var emailSearch = await ReadDataAsync(
            await client.GetAsync("/api/users?search=private-search-marker"));
        Assert.Empty(emailSearch.GetProperty("items").EnumerateArray());

        var directory = await ReadDataAsync(await client.GetAsync("/api/users?pageSize=100"));
        var names = directory.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("displayName").GetString())
            .ToList();
        Assert.DoesNotContain("Locked Reader", names);
        Assert.DoesNotContain("Deleted Reader", names);
        Assert.All(
            directory.GetProperty("items").EnumerateArray(),
            item =>
            {
                Assert.Equal("DIRECTORY", item.GetProperty("reason").GetString());
                Assert.Equal(
                    "Độc giả đang hoạt động trên BookSpace.",
                    item.GetProperty("reasonText").GetString());
            });

        foreach (var invalidSearch in new[] { "a", new string('x', 101) })
        {
            var invalid = await client.GetAsync(
                $"/api/users?search={Uri.EscapeDataString(invalidSearch)}");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var envelope = await ReadEnvelopeAsync(invalid);
            Assert.Equal("INVALID_USER_SEARCH", envelope.GetProperty("code").GetString());
            Assert.Contains(
                "2 đến 100",
                envelope.GetProperty("message").GetString(),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Discovery_follower_count_matches_the_public_profile_observable_count()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        Guid adminId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var admin = db.UserSet.Single(user => user.Email == "admin@bookspace.local");
            var lockedFollower = new User(
                "locked-follower@bookspace.local",
                "hash",
                "Locked Follower");
            lockedFollower.Lock();
            db.Add(lockedFollower);
            db.Add(new Follow(lockedFollower.Id, admin.Id));
            await db.SaveChangesAsync();
            adminId = admin.Id;
        }

        var profile = await ReadDataAsync(await client.GetAsync($"/api/users/{adminId}"));
        var search = await ReadDataAsync(
            await client.GetAsync("/api/users?search=Qu%E1%BA%A3n%20tr%E1%BB%8B"));
        var discoveryItem = Assert.Single(search.GetProperty("items").EnumerateArray());

        Assert.Equal(
            profile.GetProperty("followerCount").GetInt32(),
            discoveryItem.GetProperty("followerCount").GetInt32());
        Assert.Equal(2, discoveryItem.GetProperty("followerCount").GetInt32());
    }

    [Fact]
    public async Task Authenticated_search_returns_relationship_state_and_database_pagination()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "reader@bookspace.local", "Reader123!");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            db.AddRange(
                Enumerable.Range(1, 25)
                    .Select(index => new User(
                        $"paged-{index:00}@bookspace.local",
                        "hash",
                        $"Paged Reader {index:00}")));
            await db.SaveChangesAsync();
        }

        var followed = await ReadDataAsync(
            await client.GetAsync("/api/users?search=Quản%20trị"));
        var admin = Assert.Single(followed.GetProperty("items").EnumerateArray());
        Assert.True(admin.GetProperty("isFollowing").GetBoolean());

        var followsYou = await ReadDataAsync(
            await client.GetAsync("/api/users?search=Hà%20Linh"));
        var demo = Assert.Single(followsYou.GetProperty("items").EnumerateArray());
        Assert.True(demo.GetProperty("followsYou").GetBoolean());
        Assert.Equal(1, demo.GetProperty("mutualFollowCount").GetInt32());

        var page = await ReadDataAsync(
            await client.GetAsync("/api/users?search=Paged%20Reader&page=2&pageSize=10"));
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(10, page.GetProperty("items").GetArrayLength());
        Assert.Equal(25, page.GetProperty("totalItems").GetInt32());
        Assert.Equal(
            "Paged Reader 11",
            page.GetProperty("items")[0].GetProperty("displayName").GetString());
        Assert.Equal(
            "Paged Reader 20",
            page.GetProperty("items")[9].GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Suggestions_rank_mutuals_first_exclude_invalid_candidates_and_keep_fallbacks_deterministic()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "reader@bookspace.local", "Reader123!");
        Guid leaderId;
        Guid alreadyFollowedId;
        Guid lockedId;
        Guid deletedId;
        Guid firstTieId;
        Guid secondTieId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var reader = db.UserSet.Single(user => user.Email == "reader@bookspace.local");
            var connectorOne = new User("connector-one@bookspace.local", "hash", "Connector One");
            var connectorTwo = new User("connector-two@bookspace.local", "hash", "Connector Two");
            var leader = new User("leader@bookspace.local", "hash", "Mutual Leader");
            var alreadyFollowed = new User("followed@bookspace.local", "hash", "Already Followed");
            var firstTie = new User("tie-one@bookspace.local", "hash", "Zulu Fallback");
            var secondTie = new User("tie-two@bookspace.local", "hash", "Zulu Fallback");
            var locked = new User("locked-suggestion@bookspace.local", "hash", "Locked Suggestion");
            locked.Lock();
            var deleted = new User("deleted-suggestion@bookspace.local", "hash", "Deleted Suggestion");
            deleted.SoftDelete();
            db.AddRange(
                connectorOne,
                connectorTwo,
                leader,
                alreadyFollowed,
                firstTie,
                secondTie,
                locked,
                deleted);
            db.AddRange(
                new Follow(reader.Id, connectorOne.Id),
                new Follow(reader.Id, connectorTwo.Id),
                new Follow(reader.Id, alreadyFollowed.Id),
                new Follow(connectorOne.Id, leader.Id),
                new Follow(connectorTwo.Id, leader.Id));
            await db.SaveChangesAsync();
            leaderId = leader.Id;
            alreadyFollowedId = alreadyFollowed.Id;
            lockedId = locked.Id;
            deletedId = deleted.Id;
            (firstTieId, secondTieId) = firstTie.Id.CompareTo(secondTie.Id) < 0
                ? (firstTie.Id, secondTie.Id)
                : (secondTie.Id, firstTie.Id);
        }

        var suggestions = await ReadDataAsync(
            await client.GetAsync("/api/users/suggestions?page=1&pageSize=100"));
        var items = suggestions.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(leaderId, items[0].GetProperty("id").GetGuid());
        Assert.Equal(2, items[0].GetProperty("mutualFollowCount").GetInt32());
        Assert.Equal("MUTUAL_FOLLOWS", items[0].GetProperty("reason").GetString());
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetGuid() == alreadyFollowedId);
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetGuid() == lockedId);
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetGuid() == deletedId);

        var firstTieIndex = items.FindIndex(item => item.GetProperty("id").GetGuid() == firstTieId);
        var secondTieIndex = items.FindIndex(item => item.GetProperty("id").GetGuid() == secondTieId);
        Assert.True(firstTieIndex >= 0);
        Assert.Equal(firstTieIndex + 1, secondTieIndex);
        Assert.Equal(0, items[firstTieIndex].GetProperty("mutualFollowCount").GetInt32());

        var firstPage = await ReadDataAsync(
            await client.GetAsync("/api/users/suggestions?page=1&pageSize=2"));
        var secondPage = await ReadDataAsync(
            await client.GetAsync("/api/users/suggestions?page=2&pageSize=2"));
        Assert.Equal(2, firstPage.GetProperty("items").GetArrayLength());
        Assert.Equal(2, secondPage.GetProperty("items").GetArrayLength());
        Assert.NotEqual(
            firstPage.GetProperty("items")[1].GetProperty("id").GetGuid(),
            secondPage.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Following_a_suggestion_removes_it_and_activates_its_public_feed_activity()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "reader@bookspace.local", "Reader123!");

        var suggestions = await ReadDataAsync(await client.GetAsync("/api/users/suggestions"));
        var demo = suggestions.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("displayName").GetString() == "Hà Linh");
        var demoId = demo.GetProperty("id").GetGuid();
        var beforeFeed = await ReadDataAsync(await client.GetAsync("/api/feed?pageSize=100"));
        Assert.DoesNotContain(
            beforeFeed.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("actor").GetProperty("id").GetGuid() == demoId);

        var follow = await client.PostAsync($"/api/users/{demoId}/follow", null);
        Assert.Equal(HttpStatusCode.OK, follow.StatusCode);
        Assert.True((await ReadDataAsync(follow)).GetProperty("isFollowing").GetBoolean());

        var afterSuggestions = await ReadDataAsync(await client.GetAsync("/api/users/suggestions"));
        Assert.DoesNotContain(
            afterSuggestions.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == demoId);
        var search = await ReadDataAsync(await client.GetAsync("/api/users?search=Hà%20Linh"));
        Assert.True(
            Assert.Single(search.GetProperty("items").EnumerateArray())
                .GetProperty("isFollowing")
                .GetBoolean());
        var afterFeed = await ReadDataAsync(await client.GetAsync("/api/feed?pageSize=100"));
        Assert.Contains(
            afterFeed.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("actor").GetProperty("id").GetGuid() == demoId);
    }

    [Fact]
    public async Task Concurrent_duplicate_follow_returns_stable_conflict_and_writes_once()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client, "reader@bookspace.local", "Reader123!");
        var suggestions = await ReadDataAsync(await client.GetAsync("/api/users/suggestions"));
        var targetId = suggestions.GetProperty("items")[0].GetProperty("id").GetGuid();
        Guid readerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            readerId = db.UserSet.Single(user => user.Email == "reader@bookspace.local").Id;
        }

        var responses = await Task.WhenAll(
            client.PostAsync($"/api/users/{targetId}/follow", null),
            client.PostAsync($"/api/users/{targetId}/follow", null));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflict = Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);
        var conflictEnvelope = await ReadEnvelopeAsync(conflict);
        Assert.Equal("ALREADY_FOLLOWING", conflictEnvelope.GetProperty("code").GetString());

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        Assert.Equal(
            1,
            await verificationDb.FollowSet.IgnoreQueryFilters().CountAsync(
                follow => follow.FollowerId == readerId && follow.FollowingId == targetId));
        Assert.Equal(
            1,
            await verificationDb.NotificationSet.IgnoreQueryFilters().CountAsync(
                notification =>
                    notification.UserId == targetId &&
                    notification.Type == NotificationType.FOLLOW &&
                    notification.Link == $"/users/{readerId}"));
    }

    [Fact]
    public async Task Follow_boundary_does_not_swallow_a_different_unique_constraint()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        _ = await client.GetAsync("/health");
        Guid readerId;
        Guid targetId;
        const string duplicateKey = "follow-boundary-other-constraint";

        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            readerId = db.UserSet.Single(user => user.Email == "reader@bookspace.local").Id;
            var target = new User("boundary-target@bookspace.local", "hash", "Boundary Target");
            db.Add(target);
            db.Add(new Notification(
                readerId,
                NotificationType.SYSTEM,
                "Thông báo kiểm thử",
                "Khóa này dùng để tạo unique conflict ngoài follow.",
                deduplicationKey: duplicateKey));
            await db.SaveChangesAsync();
            targetId = target.Id;
        }

        await using (var mutationScope = factory.Services.CreateAsyncScope())
        {
            var boundary = mutationScope.ServiceProvider.GetRequiredService<IFollowMutationBoundary>();
            await Assert.ThrowsAsync<DbUpdateException>(() => boundary.TryCreateAsync(
                new Follow(readerId, targetId),
                new Notification(
                    targetId,
                    NotificationType.FOLLOW,
                    "Thông báo trùng khóa",
                    "Boundary không được nuốt conflict này.",
                    deduplicationKey: duplicateKey),
                CancellationToken.None));
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        Assert.False(await verificationDb.FollowSet.IgnoreQueryFilters().AnyAsync(
            follow => follow.FollowerId == readerId && follow.FollowingId == targetId));
    }

    [Fact]
    public async Task Development_seed_skips_a_soft_deleted_discovery_reader_without_duplication_or_restore()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        _ = await client.GetAsync("/health");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var discoveryReader = db.UserSet.Single(
                user => user.Email == "ha.linh.discovery@bookspace.local");
            discoveryReader.SoftDelete();
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync();
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var matchingUsers = await verificationDb.UserSet
            .IgnoreQueryFilters()
            .Where(user => user.Email == "ha.linh.discovery@bookspace.local")
            .ToListAsync();
        var discoveryReaderAfterRestart = Assert.Single(matchingUsers);
        Assert.True(discoveryReaderAfterRestart.IsDeleted);
    }

    [Fact]
    public async Task Development_seed_keeps_the_discovery_book_fixture_stable_when_catalog_order_changes()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        _ = await client.GetAsync("/health");
        Guid discoveryReaderId;
        Guid originalBookId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            discoveryReaderId = db.UserSet.Single(
                user => user.Email == "ha.linh.discovery@bookspace.local").Id;
            originalBookId = Assert.Single(
                db.LibraryItemSet.Where(item => item.UserId == discoveryReaderId)).BookId;
            db.Add(new Book(
                "! Sách đứng đầu danh mục",
                "Dữ liệu kiểm thử tính ổn định của seed.",
                "9780000000001",
                null,
                100,
                2026));
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync();
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var items = await verificationDb.LibraryItemSet
            .Where(item => item.UserId == discoveryReaderId)
            .ToListAsync();
        Assert.Equal(originalBookId, Assert.Single(items).BookId);
        Assert.Equal(
            "9786043458168",
            await verificationDb.BookSet
                .Where(book => book.Id == originalBookId)
                .Select(book => book.Isbn)
                .SingleAsync());
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                data.GetProperty("accessToken").GetString());
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
