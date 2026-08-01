using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class PublicReaderProfileFlowTests
{
    [Fact]
    public async Task Public_profile_sections_respect_privacy_without_leaking_private_reading_data()
    {
        using var factory = new BookSpaceApiFactory();
        using var ownerClient = factory.CreateClient();
        using var guestClient = factory.CreateClient();

        var register = await ownerClient.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "profile-v2@bookspace.local",
                password = "Reader123!",
                displayName = "Độc giả Profile V2"
            });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registration = await ReadDataAsync(register);
        var userId = registration.GetProperty("user").GetProperty("id").GetGuid();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            registration.GetProperty("accessToken").GetString());

        var books = await ReadDataAsync(await guestClient.GetAsync("/api/books?pageSize=1"));
        var bookId = books.GetProperty("items")[0].GetProperty("id").GetGuid();

        var addLibrary = await ownerClient.PostAsJsonAsync(
            "/api/library",
            new { bookId, shelf = "READING" });
        Assert.Equal(HttpStatusCode.Created, addLibrary.StatusCode);

        const string privateSessionNote = "PRIVATE_SESSION_MARKER";
        var addSession = await ownerClient.PostAsJsonAsync(
            "/api/reading-sessions",
            new
            {
                bookId,
                startedAt = DateTimeOffset.UtcNow.AddHours(-1),
                endedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                durationMinutes = 30,
                pagesRead = 12,
                note = privateSessionNote
            });
        Assert.Equal(HttpStatusCode.Created, addSession.StatusCode);

        const string privateReadingNote = "PRIVATE_READING_NOTE_MARKER";
        var addNote = await ownerClient.PostAsJsonAsync(
            "/api/reading-notes",
            new
            {
                bookId,
                pageNumber = 12,
                content = privateReadingNote,
                tags = new[] { "riêng tư" }
            });
        Assert.Equal(HttpStatusCode.Created, addNote.StatusCode);

        var addReview = await ownerClient.PostAsJsonAsync(
            "/api/reviews",
            new
            {
                bookId,
                rating = 5,
                content = "Một đánh giá công khai đủ dài để kiểm tra hồ sơ độc giả.",
                containsSpoilers = false
            });
        Assert.Equal(HttpStatusCode.Created, addReview.StatusCode);

        var guestProfile = await ReadDataAsync(
            await guestClient.GetAsync($"/api/users/{userId}"));
        Assert.Equal(JsonValueKind.Null, guestProfile.GetProperty("email").ValueKind);
        Assert.False(guestProfile.GetProperty("privacy").GetProperty("isReadingShelfPublic").GetBoolean());
        Assert.False(guestProfile.GetProperty("privacy").GetProperty("isReadingActivityPublic").GetBoolean());
        Assert.False(guestProfile.GetProperty("followsYou").GetBoolean());
        Assert.Equal(0, guestProfile.GetProperty("mutualFollowCount").GetInt32());

        await AssertPrivateSectionAsync(
            await guestClient.GetAsync($"/api/users/{userId}/library"));
        await AssertPrivateSectionAsync(
            await guestClient.GetAsync($"/api/users/{userId}/activity"));

        var publicReviewsResponse = await guestClient.GetAsync($"/api/users/{userId}/reviews");
        var publicReviews = await ReadDataAsync(publicReviewsResponse);
        Assert.Single(publicReviews.GetProperty("items").EnumerateArray());
        Assert.DoesNotContain(privateSessionNote, await publicReviewsResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain(privateReadingNote, await publicReviewsResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            HttpStatusCode.OK,
            (await ownerClient.GetAsync($"/api/users/{userId}/library")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ownerClient.GetAsync($"/api/users/{userId}/activity")).StatusCode);

        var updatePrivacy = await ownerClient.PatchAsJsonAsync(
            "/api/users/me/privacy",
            new { isReadingShelfPublic = true, isReadingActivityPublic = true });
        var updatedProfile = await ReadDataAsync(updatePrivacy);
        Assert.True(updatedProfile.GetProperty("privacy").GetProperty("isReadingShelfPublic").GetBoolean());
        Assert.True(updatedProfile.GetProperty("privacy").GetProperty("isReadingActivityPublic").GetBoolean());

        var publicLibraryResponse = await guestClient.GetAsync($"/api/users/{userId}/library?shelf=READING");
        var publicLibrary = await ReadDataAsync(publicLibraryResponse);
        var publicLibraryItem = Assert.Single(publicLibrary.GetProperty("items").EnumerateArray());
        Assert.Equal("READING", publicLibraryItem.GetProperty("shelf").GetString());
        Assert.False(publicLibraryItem.TryGetProperty("currentPage", out _));

        var publicActivityResponse = await guestClient.GetAsync($"/api/users/{userId}/activity");
        var publicActivity = await ReadDataAsync(publicActivityResponse);
        Assert.NotEmpty(publicActivity.GetProperty("items").EnumerateArray());
        var combinedPublicJson =
            await publicLibraryResponse.Content.ReadAsStringAsync() +
            await publicActivityResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(privateSessionNote, combinedPublicJson);
        Assert.DoesNotContain(privateReadingNote, combinedPublicJson);
        Assert.DoesNotContain("email", combinedPublicJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Seeded_public_reader_profile_exposes_real_paginated_sections()
    {
        using var factory = new BookSpaceApiFactory();
        using var client = factory.CreateClient();

        var search = await ReadDataAsync(
            await client.GetAsync("/api/users?search=Minh%20Anh"));
        var reader = Assert.Single(search.GetProperty("items").EnumerateArray());
        var readerId = reader.GetProperty("id").GetGuid();

        var profile = await ReadDataAsync(await client.GetAsync($"/api/users/{readerId}"));
        Assert.True(profile.GetProperty("privacy").GetProperty("isReadingShelfPublic").GetBoolean());
        Assert.True(profile.GetProperty("privacy").GetProperty("isReadingActivityPublic").GetBoolean());

        var library = await ReadDataAsync(
            await client.GetAsync($"/api/users/{readerId}/library?page=1&pageSize=2"));
        Assert.Equal(2, library.GetProperty("items").GetArrayLength());
        Assert.True(library.GetProperty("totalItems").GetInt32() >= 3);

        var reviews = await ReadDataAsync(
            await client.GetAsync($"/api/users/{readerId}/reviews?page=1&pageSize=1"));
        Assert.Single(reviews.GetProperty("items").EnumerateArray());
        Assert.True(reviews.GetProperty("totalItems").GetInt32() >= 2);

        var activity = await ReadDataAsync(
            await client.GetAsync($"/api/users/{readerId}/activity?page=1&pageSize=2"));
        Assert.NotEmpty(activity.GetProperty("items").EnumerateArray());
        Assert.True(activity.GetProperty("totalItems").GetInt32() >= 2);
    }

    private static async Task AssertPrivateSectionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PROFILE_SECTION_PRIVATE", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
}
