using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Domain.Entities;
using BookSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.IntegrationTests;

public sealed class OnboardingFlowTests
{
    [Fact]
    public async Task Onboarding_requires_authentication_and_validates_stable_preference_errors()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();

        var unauthorized = await client.GetAsync("/api/users/me/onboarding");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await PutPreferencesAsync(client, [], [])).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsync("/api/users/me/onboarding/complete", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsync("/api/users/me/onboarding/skip", null)).StatusCode);

        var seed = await SeedCatalogAsync(factory);
        await RegisterAndAuthorizeAsync(client, "validation");

        await AssertValidationErrorAsync(await client.PutAsJsonAsync(
            "/api/users/me/onboarding",
            new { }));
        await AssertValidationErrorAsync(await client.PutAsJsonAsync(
            "/api/users/me/onboarding",
            new
            {
                preferredCategoryIds = (Guid[]?)null,
                referenceBookIds = (Guid[]?)null
            }));

        var initial = await ReadDataAsync(await client.GetAsync("/api/users/me/onboarding"));
        Assert.Equal("PENDING", initial.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, initial.GetProperty("finishedAt").ValueKind);
        Assert.Empty(initial.GetProperty("preferredCategoryIds").EnumerateArray());
        Assert.Empty(initial.GetProperty("referenceBookIds").EnumerateArray());

        await AssertErrorCodeAsync(
            await PutPreferencesAsync(client, seed.CategoryIds, []),
            HttpStatusCode.BadRequest,
            "ONBOARDING_PREFERRED_CATEGORY_LIMIT_EXCEEDED");
        await AssertErrorCodeAsync(
            await PutPreferencesAsync(client, [], seed.ReferenceBookIds),
            HttpStatusCode.BadRequest,
            "ONBOARDING_REFERENCE_BOOK_LIMIT_EXCEEDED");
        await AssertErrorCodeAsync(
            await PutPreferencesAsync(client, [Guid.NewGuid()], []),
            HttpStatusCode.NotFound,
            "ONBOARDING_PREFERRED_CATEGORY_NOT_FOUND");
        await AssertErrorCodeAsync(
            await PutPreferencesAsync(client, [], [Guid.NewGuid()]),
            HttpStatusCode.NotFound,
            "ONBOARDING_REFERENCE_BOOK_NOT_FOUND");

        var partial = await ReadDataAsync(await PutPreferencesAsync(
            client,
            [seed.CategoryIds[0], seed.CategoryIds[0], seed.CategoryIds[1]],
            [seed.ReferenceBookIds[0], seed.ReferenceBookIds[0]]));
        Assert.Equal(2, partial.GetProperty("preferredCategoryIds").GetArrayLength());
        Assert.Single(partial.GetProperty("referenceBookIds").EnumerateArray());

        await AssertErrorCodeAsync(
            await PutPreferencesAsync(
                client,
                [seed.CategoryIds[2], Guid.NewGuid()],
                [seed.ReferenceBookIds[2]]),
            HttpStatusCode.NotFound,
            "ONBOARDING_PREFERRED_CATEGORY_NOT_FOUND");
        var afterRejectedReplace = await ReadDataAsync(
            await client.GetAsync("/api/users/me/onboarding"));
        Assert.True(
            new[] { seed.CategoryIds[0], seed.CategoryIds[1] }.ToHashSet()
                .SetEquals(ReadIds(afterRejectedReplace, "preferredCategoryIds")));
        Assert.True(
            new[] { seed.ReferenceBookIds[0] }.ToHashSet()
                .SetEquals(ReadIds(afterRejectedReplace, "referenceBookIds")));

        await AssertErrorCodeAsync(
            await client.PostAsync("/api/users/me/onboarding/complete", null),
            HttpStatusCode.BadRequest,
            "ONBOARDING_INCOMPLETE");
    }

    [Fact]
    public async Task Complete_and_skip_transitions_are_idempotent_and_preferences_full_replace()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedCatalogAsync(factory);
        var firstUserId = await RegisterAndAuthorizeAsync(client, "complete");

        _ = await ReadDataAsync(await PutPreferencesAsync(
            client,
            seed.CategoryIds[..4],
            seed.ReferenceBookIds[..4]));
        var replaced = await ReadDataAsync(await PutPreferencesAsync(
            client,
            seed.CategoryIds[..3],
            seed.ReferenceBookIds[..3]));
        Assert.Equal(3, replaced.GetProperty("preferredCategoryIds").GetArrayLength());
        Assert.Equal(3, replaced.GetProperty("referenceBookIds").GetArrayLength());

        var completed = await ReadDataAsync(
            await client.PostAsync("/api/users/me/onboarding/complete", null));
        var completedAt = completed.GetProperty("finishedAt").GetDateTimeOffset();
        Assert.Equal("COMPLETED", completed.GetProperty("status").GetString());

        var repeated = await ReadDataAsync(
            await client.PostAsync("/api/users/me/onboarding/complete", null));
        Assert.Equal(completedAt, repeated.GetProperty("finishedAt").GetDateTimeOffset());
        var afterSkip = await ReadDataAsync(
            await client.PostAsync("/api/users/me/onboarding/skip", null));
        Assert.Equal("COMPLETED", afterSkip.GetProperty("status").GetString());
        Assert.Equal(completedAt, afterSkip.GetProperty("finishedAt").GetDateTimeOffset());

        await AssertErrorCodeAsync(
            await PutPreferencesAsync(
                client,
                seed.CategoryIds[..2],
                seed.ReferenceBookIds[..2]),
            HttpStatusCode.BadRequest,
            "ONBOARDING_INCOMPLETE");
        var afterRejectedEdit = await ReadDataAsync(
            await client.GetAsync("/api/users/me/onboarding"));
        Assert.Equal(3, afterRejectedEdit.GetProperty("preferredCategoryIds").GetArrayLength());
        Assert.Equal(3, afterRejectedEdit.GetProperty("referenceBookIds").GetArrayLength());

        var validEdit = await ReadDataAsync(await PutPreferencesAsync(
            client,
            seed.CategoryIds[3..6],
            seed.ReferenceBookIds[3..6]));
        Assert.Equal("COMPLETED", validEdit.GetProperty("status").GetString());
        Assert.Equal(completedAt, validEdit.GetProperty("finishedAt").GetDateTimeOffset());
        Assert.True(seed.CategoryIds[3..6].ToHashSet()
            .SetEquals(ReadIds(validEdit, "preferredCategoryIds")));
        Assert.True(seed.ReferenceBookIds[3..6].ToHashSet()
            .SetEquals(ReadIds(validEdit, "referenceBookIds")));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            Assert.Equal(3, await db.UserPreferredCategorySet.CountAsync(x => x.UserId == firstUserId));
            Assert.Equal(3, await db.UserReferenceBookSet.CountAsync(x => x.UserId == firstUserId));
        }

        await RegisterAndAuthorizeAsync(client, "skip");
        _ = await ReadDataAsync(await PutPreferencesAsync(
            client,
            seed.CategoryIds[..2],
            seed.ReferenceBookIds[..2]));
        var skipped = await ReadDataAsync(
            await client.PostAsync("/api/users/me/onboarding/skip", null));
        var skippedAt = skipped.GetProperty("finishedAt").GetDateTimeOffset();
        Assert.Equal("SKIPPED", skipped.GetProperty("status").GetString());
        Assert.Equal(2, ReadIds(skipped, "preferredCategoryIds").Count);
        Assert.Equal(2, ReadIds(skipped, "referenceBookIds").Count);
        var repeatedSkip = await ReadDataAsync(
            await client.PostAsync("/api/users/me/onboarding/skip", null));
        Assert.Equal(skippedAt, repeatedSkip.GetProperty("finishedAt").GetDateTimeOffset());
        Assert.Equal(2, ReadIds(repeatedSkip, "preferredCategoryIds").Count);
        Assert.Equal(2, ReadIds(repeatedSkip, "referenceBookIds").Count);

        _ = await ReadDataAsync(await PutPreferencesAsync(
            client,
            seed.CategoryIds[..3],
            seed.ReferenceBookIds[..3]));
        var completedAfterSkip = await ReadDataAsync(
            await client.PostAsync("/api/users/me/onboarding/complete", null));
        Assert.Equal("COMPLETED", completedAfterSkip.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Onboarding_signals_exclude_reference_books_and_keep_existing_recommendation_reasons()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedCatalogAsync(factory);
        await RegisterAndAuthorizeAsync(client, "recommendation");

        _ = await ReadDataAsync(await PutPreferencesAsync(
            client,
            seed.CategoryIds[..3],
            seed.ReferenceBookIds[..3]));
        _ = await ReadDataAsync(
            await client.PostAsync("/api/users/me/onboarding/complete", null));

        var page = await ReadDataAsync(
            await client.GetAsync("/api/books/recommendations?page=1&pageSize=100"));
        var recommendations = page.GetProperty("items").EnumerateArray().ToList();

        Assert.DoesNotContain(
            recommendations,
            item => seed.ReferenceBookIds[..3].Contains(
                item.GetProperty("book").GetProperty("id").GetGuid()));
        Assert.Equal(
            "MATCHED_AUTHOR",
            Assert.Single(
                    recommendations,
                    item => item.GetProperty("book").GetProperty("id").GetGuid() ==
                            seed.AuthorMatchBookId)
                .GetProperty("reasonCode")
                .GetString());
        Assert.Equal(
            "MATCHED_CATEGORY",
            Assert.Single(
                    recommendations,
                    item => item.GetProperty("book").GetProperty("id").GetGuid() ==
                            seed.CategoryMatchBookId)
                .GetProperty("reasonCode")
                .GetString());
    }

    [Fact]
    public async Task Full_replace_removes_links_hidden_by_soft_deleted_targets_before_restore()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedCatalogAsync(factory);
        var userId = await RegisterAndAuthorizeAsync(client, "stale-links");
        var originalCategoryIds = seed.CategoryIds[..3];
        var originalBookIds = seed.ReferenceBookIds[..3];
        _ = await ReadDataAsync(await PutPreferencesAsync(
            client,
            originalCategoryIds,
            originalBookIds));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            (await db.CategorySet.SingleAsync(category => category.Id == originalCategoryIds[0]))
                .SoftDelete();
            (await db.BookSet.SingleAsync(book => book.Id == originalBookIds[0]))
                .SoftDelete();
            await db.SaveChangesAsync();
        }

        var whileInactive = await ReadDataAsync(
            await client.GetAsync("/api/users/me/onboarding"));
        Assert.Equal(2, ReadIds(whileInactive, "preferredCategoryIds").Count);
        Assert.DoesNotContain(originalCategoryIds[0], ReadIds(whileInactive, "preferredCategoryIds"));
        Assert.Equal(2, ReadIds(whileInactive, "referenceBookIds").Count);
        Assert.DoesNotContain(originalBookIds[0], ReadIds(whileInactive, "referenceBookIds"));

        await AssertErrorCodeAsync(
            await client.PostAsync("/api/users/me/onboarding/complete", null),
            HttpStatusCode.BadRequest,
            "ONBOARDING_INCOMPLETE");
        var afterRejectedComplete = await ReadDataAsync(
            await client.GetAsync("/api/users/me/onboarding"));
        Assert.Equal("PENDING", afterRejectedComplete.GetProperty("status").GetString());
        Assert.Equal(2, ReadIds(afterRejectedComplete, "preferredCategoryIds").Count);
        Assert.Equal(2, ReadIds(afterRejectedComplete, "referenceBookIds").Count);

        var inactiveRecommendations = await ReadDataAsync(
            await client.GetAsync("/api/books/recommendations?page=1&pageSize=100"));
        Assert.Equal(
            "POPULAR_FALLBACK",
            RecommendationFor(inactiveRecommendations, seed.AuthorMatchBookId)
                .GetProperty("reasonCode")
                .GetString());
        Assert.Equal(
            "POPULAR_FALLBACK",
            RecommendationFor(inactiveRecommendations, seed.CategoryMatchBookId)
                .GetProperty("reasonCode")
                .GetString());

        var replacementCategoryIds = seed.CategoryIds[5..10];
        var replacementBookIds = seed.ReferenceBookIds[5..10];
        _ = await ReadDataAsync(await PutPreferencesAsync(
            client,
            replacementCategoryIds,
            replacementBookIds));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE categories SET DeletedAt = NULL WHERE Id = {originalCategoryIds[0]}");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE books SET DeletedAt = NULL WHERE Id = {originalBookIds[0]}");
        }

        var afterRestore = await ReadDataAsync(
            await client.GetAsync("/api/users/me/onboarding"));
        Assert.Equal(5, ReadIds(afterRestore, "preferredCategoryIds").Count);
        Assert.True(replacementCategoryIds.ToHashSet()
            .SetEquals(ReadIds(afterRestore, "preferredCategoryIds")));
        Assert.Equal(5, ReadIds(afterRestore, "referenceBookIds").Count);
        Assert.True(replacementBookIds.ToHashSet()
            .SetEquals(ReadIds(afterRestore, "referenceBookIds")));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            Assert.Equal(
                5,
                await db.UserPreferredCategorySet.IgnoreQueryFilters()
                    .CountAsync(link => link.UserId == userId));
            Assert.Equal(
                5,
                await db.UserReferenceBookSet.IgnoreQueryFilters()
                    .CountAsync(link => link.UserId == userId));
            Assert.DoesNotContain(
                await db.UserPreferredCategorySet.IgnoreQueryFilters()
                    .Where(link => link.UserId == userId)
                    .Select(link => link.CategoryId)
                    .ToListAsync(),
                categoryId => categoryId == originalCategoryIds[0]);
            Assert.DoesNotContain(
                await db.UserReferenceBookSet.IgnoreQueryFilters()
                    .Where(link => link.UserId == userId)
                    .Select(link => link.BookId)
                    .ToListAsync(),
                bookId => bookId == originalBookIds[0]);
        }

        var completed = await ReadDataAsync(
            await client.PostAsync("/api/users/me/onboarding/complete", null));
        Assert.Equal("COMPLETED", completed.GetProperty("status").GetString());
        var restoredRecommendations = await ReadDataAsync(
            await client.GetAsync("/api/books/recommendations?page=1&pageSize=100"));
        Assert.Equal(
            "POPULAR_FALLBACK",
            RecommendationFor(restoredRecommendations, originalBookIds[0])
                .GetProperty("reasonCode")
                .GetString());
        Assert.Equal(
            "POPULAR_FALLBACK",
            RecommendationFor(restoredRecommendations, seed.AuthorMatchBookId)
                .GetProperty("reasonCode")
                .GetString());
        Assert.Equal(
            "POPULAR_FALLBACK",
            RecommendationFor(restoredRecommendations, seed.CategoryMatchBookId)
                .GetProperty("reasonCode")
                .GetString());
    }

    [Fact]
    public async Task Onboarding_preferences_are_isolated_by_owner()
    {
        using var factory = new BookSpaceApiFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var seed = await SeedCatalogAsync(factory);
        await RegisterAndAuthorizeAsync(firstClient, "owner-one");
        await RegisterAndAuthorizeAsync(secondClient, "owner-two");

        _ = await ReadDataAsync(await PutPreferencesAsync(
            firstClient,
            seed.CategoryIds[..2],
            seed.ReferenceBookIds[..2]));
        var secondInitial = await ReadDataAsync(
            await secondClient.GetAsync("/api/users/me/onboarding"));
        Assert.Empty(ReadIds(secondInitial, "preferredCategoryIds"));
        Assert.Empty(ReadIds(secondInitial, "referenceBookIds"));

        _ = await ReadDataAsync(await PutPreferencesAsync(
            secondClient,
            seed.CategoryIds[2..4],
            seed.ReferenceBookIds[2..4]));
        var firstAfterSecondUpdate = await ReadDataAsync(
            await firstClient.GetAsync("/api/users/me/onboarding"));
        Assert.True(seed.CategoryIds[..2].ToHashSet()
            .SetEquals(ReadIds(firstAfterSecondUpdate, "preferredCategoryIds")));
        Assert.True(seed.ReferenceBookIds[..2].ToHashSet()
            .SetEquals(ReadIds(firstAfterSecondUpdate, "referenceBookIds")));
    }

    [Fact]
    public async Task Concurrent_terminal_mutations_keep_completed_state_and_complete_preferences()
    {
        using var factory = new BookSpaceApiFactory();
        using var completeClient = factory.CreateClient();
        using var competingClient = factory.CreateClient();
        var seed = await SeedCatalogAsync(factory);
        await RegisterAndAuthorizeAsync(completeClient, "concurrent");
        competingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            completeClient.DefaultRequestHeaders.Authorization!.Scheme,
            completeClient.DefaultRequestHeaders.Authorization.Parameter);
        _ = await ReadDataAsync(await PutPreferencesAsync(
            completeClient,
            seed.CategoryIds[..3],
            seed.ReferenceBookIds[..3]));

        var completeAndSkip = await Task.WhenAll(
            completeClient.PostAsync("/api/users/me/onboarding/complete", null),
            competingClient.PostAsync("/api/users/me/onboarding/skip", null));
        Assert.All(completeAndSkip, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var afterCompleteAndSkip = await ReadDataAsync(
            await completeClient.GetAsync("/api/users/me/onboarding"));
        Assert.Equal("COMPLETED", afterCompleteAndSkip.GetProperty("status").GetString());
        Assert.Equal(3, ReadIds(afterCompleteAndSkip, "preferredCategoryIds").Count);
        Assert.Equal(3, ReadIds(afterCompleteAndSkip, "referenceBookIds").Count);

        var completeAndDraft = await Task.WhenAll(
            completeClient.PostAsync("/api/users/me/onboarding/complete", null),
            PutPreferencesAsync(
                competingClient,
                seed.CategoryIds[..2],
                seed.ReferenceBookIds[..2]));
        Assert.Equal(HttpStatusCode.OK, completeAndDraft[0].StatusCode);
        await AssertErrorCodeAsync(
            completeAndDraft[1],
            HttpStatusCode.BadRequest,
            "ONBOARDING_INCOMPLETE");

        var finalState = await ReadDataAsync(
            await completeClient.GetAsync("/api/users/me/onboarding"));
        Assert.Equal("COMPLETED", finalState.GetProperty("status").GetString());
        Assert.Equal(3, ReadIds(finalState, "preferredCategoryIds").Count);
        Assert.Equal(3, ReadIds(finalState, "referenceBookIds").Count);
    }

    [Fact]
    public async Task Preference_links_have_unique_owner_keys_and_cascade_from_user()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();
        _ = await client.GetAsync("/health");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        AssertPreferenceModel<UserPreferredCategory>(db, nameof(UserPreferredCategory.CategoryId));
        AssertPreferenceModel<UserReferenceBook>(db, nameof(UserReferenceBook.BookId));
    }

    private static void AssertPreferenceModel<T>(BookSpaceDbContext db, string targetProperty)
    {
        var entityType = db.Model.FindEntityType(typeof(T));
        Assert.NotNull(entityType);
        Assert.Contains(
            entityType.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Select(property => property.Name)
                         .SequenceEqual(["UserId", targetProperty]));
        var ownerForeignKey = Assert.Single(
            entityType.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Single().Name == "UserId");
        Assert.Equal(DeleteBehavior.Cascade, ownerForeignKey.DeleteBehavior);
    }

    private static async Task<OnboardingCatalogSeed> SeedCatalogAsync(BookSpaceApiFactory factory)
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var categories = Enumerable.Range(1, 10)
            .Select(index => new Category($"Onboarding {suffix} {index}"))
            .ToArray();
        var author = new Author($"Tác giả onboarding {suffix}");
        var referenceBooks = Enumerable.Range(1, 10)
            .Select(index => new Book(
                $"Sách tham chiếu {suffix} {index}",
                null,
                null,
                null,
                200 + index,
                2026))
            .ToArray();
        var authorMatch = new Book(
            $"Sách cùng tác giả {suffix}", null, null, null, 240, 2026);
        var categoryMatch = new Book(
            $"Sách cùng thể loại {suffix}", null, null, null, 260, 2026);
        db.AddRange(categories);
        db.Add(author);
        db.AddRange(referenceBooks);
        db.AddRange(authorMatch, categoryMatch);
        db.Add(new BookAuthor(referenceBooks[0].Id, author.Id));
        db.Add(new BookAuthor(authorMatch.Id, author.Id));
        db.Add(new BookCategory(categoryMatch.Id, categories[0].Id));
        await db.SaveChangesAsync();

        return new OnboardingCatalogSeed(
            categories.Select(category => category.Id).ToArray(),
            referenceBooks.Select(book => book.Id).ToArray(),
            authorMatch.Id,
            categoryMatch.Id);
    }

    private static async Task<Guid> RegisterAndAuthorizeAsync(HttpClient client, string marker)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"onboarding-{marker}-{suffix}@bookspace.local",
            password = "Reader123!",
            displayName = $"Độc giả onboarding {marker}"
        });
        var auth = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            auth.GetProperty("accessToken").GetString());
        return auth.GetProperty("user").GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> PutPreferencesAsync(
        HttpClient client,
        IReadOnlyList<Guid> categoryIds,
        IReadOnlyList<Guid> bookIds) =>
        client.PutAsJsonAsync("/api/users/me/onboarding", new
        {
            preferredCategoryIds = categoryIds,
            referenceBookIds = bookIds
        });

    private static async Task AssertErrorCodeAsync(
        HttpResponseMessage response,
        HttpStatusCode statusCode,
        string code)
    {
        Assert.Equal(statusCode, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.False(envelope.GetProperty("success").GetBoolean());
        Assert.Equal(code, envelope.GetProperty("code").GetString());
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.False(envelope.GetProperty("success").GetBoolean());
        Assert.Equal("VALIDATION_ERROR", envelope.GetProperty("code").GetString());
        Assert.NotEmpty(
            envelope.GetProperty("data").GetProperty("errors").EnumerateObject());
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        Assert.True(
            response.IsSuccessStatusCode,
            $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await ReadEnvelopeAsync(response)).GetProperty("data").Clone();
    }

    private static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static HashSet<Guid> ReadIds(JsonElement state, string propertyName) =>
        state.GetProperty(propertyName)
            .EnumerateArray()
            .Select(item => item.GetGuid())
            .ToHashSet();

    private static JsonElement RecommendationFor(JsonElement page, Guid bookId) =>
        Assert.Single(
            page.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("book").GetProperty("id").GetGuid() == bookId);

    private sealed record OnboardingCatalogSeed(
        Guid[] CategoryIds,
        Guid[] ReferenceBookIds,
        Guid AuthorMatchBookId,
        Guid CategoryMatchBookId);
}
