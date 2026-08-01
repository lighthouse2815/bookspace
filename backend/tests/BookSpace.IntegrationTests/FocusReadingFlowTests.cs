using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using BookSpace.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookSpace.IntegrationTests;

public sealed class FocusReadingFlowTests
{
    [Fact]
    public async Task Focus_lifecycle_recovers_server_time_and_atomically_syncs_reading_features()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateFactory(clock);
        var auth = await RegisterAsync(factory, "focus-lifecycle");
        using var client = auth.Client;
        var bookId = await AddBookAndChallengeAsync(factory, auth.UserId, clock.GetUtcNow());
        var goalId = await CreatePageGoalAsync(client, clock.GetUtcNow(), 100);

        var initial = await GetDataAsync(client, "/api/reading-sessions/active");
        Assert.Equal(JsonValueKind.Null, initial.ValueKind);

        var startResponse = await client.PostAsJsonAsync(
            "/api/reading-sessions/active",
            new { bookId });
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        var started = await ReadDataAsync(startResponse);
        Assert.Equal("RUNNING", started.GetProperty("status").GetString());
        Assert.Equal(0, started.GetProperty("startPage").GetInt32());
        Assert.Equal(0, started.GetProperty("elapsedSeconds").GetInt64());
        Assert.Equal(bookId, started.GetProperty("book").GetProperty("id").GetGuid());

        var duplicate = await client.PostAsJsonAsync(
            "/api/reading-sessions/active",
            new { bookId });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(
            "ACTIVE_READING_SESSION_EXISTS",
            (await ReadEnvelopeAsync(duplicate)).GetProperty("code").GetString());

        clock.Advance(TimeSpan.FromSeconds(125));
        var running = await GetDataAsync(client, "/api/reading-sessions/active");
        Assert.Equal(125, running.GetProperty("elapsedSeconds").GetInt64());

        var pauseResponse = await client.PostAsync(
            "/api/reading-sessions/active/pause",
            content: null);
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);
        var paused = await ReadDataAsync(pauseResponse);
        var pausedUpdatedAt = paused.GetProperty("updatedAt").GetDateTimeOffset();
        Assert.Equal("PAUSED", paused.GetProperty("status").GetString());
        Assert.Equal(125, paused.GetProperty("elapsedSeconds").GetInt64());

        clock.Advance(TimeSpan.FromMinutes(10));
        var repeatedPause = await ReadDataAsync(await client.PostAsync(
            "/api/reading-sessions/active/pause",
            content: null));
        Assert.Equal(125, repeatedPause.GetProperty("elapsedSeconds").GetInt64());
        AssertSameInstant(
            pausedUpdatedAt,
            repeatedPause.GetProperty("updatedAt").GetDateTimeOffset());

        var resumeResponse = await client.PostAsync(
            "/api/reading-sessions/active/resume",
            content: null);
        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);
        var resumed = await ReadDataAsync(resumeResponse);
        var resumedUpdatedAt = resumed.GetProperty("updatedAt").GetDateTimeOffset();
        Assert.Equal("RUNNING", resumed.GetProperty("status").GetString());

        clock.Advance(TimeSpan.FromSeconds(30));
        var repeatedResume = await ReadDataAsync(await client.PostAsync(
            "/api/reading-sessions/active/resume",
            content: null));
        AssertSameInstant(
            resumedUpdatedAt,
            repeatedResume.GetProperty("updatedAt").GetDateTimeOffset());
        clock.Advance(TimeSpan.FromSeconds(35));

        const string privateNote = "FOCUS_PRIVATE_NOTE_MUST_NOT_ENTER_FEED";
        var finishResponse = await client.PostAsJsonAsync(
            "/api/reading-sessions/active/finish",
            new { endingPage = 100, note = privateNote });
        Assert.Equal(HttpStatusCode.OK, finishResponse.StatusCode);
        var completed = await ReadDataAsync(finishResponse);
        Assert.Equal(100, completed.GetProperty("pagesRead").GetInt32());
        Assert.Equal(3, completed.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(privateNote, completed.GetProperty("note").GetString());
        AssertSameInstant(clock.GetUtcNow(), completed.GetProperty("endedAt").GetDateTimeOffset());

        Assert.Equal(
            JsonValueKind.Null,
            (await GetDataAsync(client, "/api/reading-sessions/active")).ValueKind);
        var library = await GetDataAsync(client, "/api/library?pageSize=100");
        var libraryItem = Assert.Single(
            library.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("bookId").GetGuid() == bookId);
        Assert.Equal("READ", libraryItem.GetProperty("shelf").GetString());
        Assert.Equal(100, libraryItem.GetProperty("currentPage").GetInt32());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var goal = db.Set<ReadingGoal>().Single(x => x.Id == goalId);
            var participation = db.ChallengeParticipationSet.Single(x => x.UserId == auth.UserId);
            Assert.NotNull(goal.CompletedAt);
            Assert.Equal(1, participation.CompletedBooks);
            Assert.NotNull(participation.CompletedAt);
            Assert.Contains(
                db.NotificationSet,
                notification =>
                    notification.UserId == auth.UserId &&
                    notification.Title == "Hoàn thành mục tiêu đọc");
        }

        var feedResponse = await client.GetAsync("/api/feed?type=READING&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, feedResponse.StatusCode);
        Assert.DoesNotContain(privateNote, await feedResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Focus_validates_duration_pages_library_high_water_and_cancel()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateFactory(clock);
        var auth = await RegisterAsync(factory, "focus-validation");
        using var client = auth.Client;
        var seeded = await AddBookWithProgressAsync(factory, auth.UserId, 50, 10);

        var started = await ReadDataAsync(await client.PostAsJsonAsync(
            "/api/reading-sessions/active",
            new { bookId = seeded.BookId }));
        Assert.Equal(10, started.GetProperty("startPage").GetInt32());

        var manualForActiveBook = await client.PostAsJsonAsync("/api/reading-sessions", new
        {
            bookId = seeded.BookId,
            startedAt = clock.GetUtcNow().AddMinutes(-10),
            durationMinutes = 5,
            pagesRead = 5,
            note = "Không được ghi trùng phiên Focus"
        });
        Assert.Equal(HttpStatusCode.Conflict, manualForActiveBook.StatusCode);
        Assert.Equal(
            "ACTIVE_READING_SESSION_EXISTS",
            (await ReadEnvelopeAsync(manualForActiveBook)).GetProperty("code").GetString());
        Assert.Equal(
            0,
            (await GetDataAsync(client, "/api/reading-sessions")).GetProperty("totalItems").GetInt32());
        Assert.Equal(10, await GetLibraryPageAsync(client, seeded.BookId));
        var otherBookId = await AddBookAsync(factory, 80);
        var manualForOtherBook = await client.PostAsJsonAsync("/api/reading-sessions", new
        {
            bookId = otherBookId,
            startedAt = clock.GetUtcNow().AddMinutes(-10),
            durationMinutes = 5,
            pagesRead = 5,
            note = "Sách khác vẫn được phép"
        });
        Assert.Equal(HttpStatusCode.Created, manualForOtherBook.StatusCode);
        Assert.Equal(5, await GetLibraryPageAsync(client, otherBookId));
        Assert.Equal(
            seeded.BookId,
            (await GetDataAsync(client, "/api/reading-sessions/active"))
            .GetProperty("bookId")
            .GetGuid());

        clock.Advance(TimeSpan.FromSeconds(30));
        var tooShort = await client.PostAsJsonAsync(
            "/api/reading-sessions/active/finish",
            new { endingPage = 20 });
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        var tooShortEnvelope = await ReadEnvelopeAsync(tooShort);
        Assert.Equal("FOCUS_READING_TOO_SHORT", tooShortEnvelope.GetProperty("code").GetString());
        Assert.Contains("ít nhất 1 phút", tooShortEnvelope.GetProperty("message").GetString());

        clock.Advance(TimeSpan.FromSeconds(31));
        foreach (var invalidPage in new[] { 10, 51 })
        {
            var invalid = await client.PostAsJsonAsync(
                "/api/reading-sessions/active/finish",
                new { endingPage = invalidPage });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal(
                "INVALID_FOCUS_END_PAGE",
                (await ReadEnvelopeAsync(invalid)).GetProperty("code").GetString());
        }

        var progress = await client.PatchAsJsonAsync(
            $"/api/library/{seeded.LibraryItemId}/progress",
            new { currentPage = 20 });
        Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
        var regressingFinish = await client.PostAsJsonAsync(
            "/api/reading-sessions/active/finish",
            new { endingPage = 15 });
        Assert.Equal(HttpStatusCode.Conflict, regressingFinish.StatusCode);
        Assert.Equal(
            "READING_PROGRESS_CANNOT_DECREASE",
            (await ReadEnvelopeAsync(regressingFinish)).GetProperty("code").GetString());

        var shelfChange = await client.PatchAsJsonAsync(
            $"/api/library/{seeded.LibraryItemId}",
            new { shelf = "WANT_TO_READ" });
        Assert.Equal(HttpStatusCode.Conflict, shelfChange.StatusCode);
        Assert.Equal(
            "ACTIVE_READING_SESSION_EXISTS",
            (await ReadEnvelopeAsync(shelfChange)).GetProperty("code").GetString());
        var removeBook = await client.DeleteAsync($"/api/library/{seeded.LibraryItemId}");
        Assert.Equal(HttpStatusCode.Conflict, removeBook.StatusCode);

        var cancel = await client.DeleteAsync("/api/reading-sessions/active");
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(
            JsonValueKind.Null,
            (await GetDataAsync(client, "/api/reading-sessions/active")).ValueKind);

        var removeAfterCancel = await client.DeleteAsync($"/api/library/{seeded.LibraryItemId}");
        Assert.Equal(HttpStatusCode.OK, removeAfterCancel.StatusCode);
        var restart = await client.PostAsJsonAsync(
            "/api/reading-sessions/active",
            new { bookId = seeded.BookId });
        Assert.Equal(HttpStatusCode.Created, restart.StatusCode);
        Assert.Equal(20, (await ReadDataAsync(restart)).GetProperty("startPage").GetInt32());
        var restoredLibrary = await GetDataAsync(client, "/api/library?pageSize=100");
        var restoredItem = Assert.Single(
            restoredLibrary.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("bookId").GetGuid() == seeded.BookId);
        Assert.Equal(seeded.LibraryItemId, restoredItem.GetProperty("id").GetGuid());
        Assert.Equal("READING", restoredItem.GetProperty("shelf").GetString());
        Assert.Equal(20, restoredItem.GetProperty("currentPage").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync("/api/reading-sessions/active")).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.DeleteAsync($"/api/library/{seeded.LibraryItemId}")).StatusCode);
        var reAdd = await client.PostAsJsonAsync("/api/library", new
        {
            bookId = seeded.BookId,
            shelf = "READING"
        });
        Assert.Equal(HttpStatusCode.Created, reAdd.StatusCode);
        var reAddedItem = await ReadDataAsync(reAdd);
        Assert.Equal(seeded.LibraryItemId, reAddedItem.GetProperty("id").GetGuid());
        Assert.Equal(20, reAddedItem.GetProperty("currentPage").GetInt32());
        var startAfterReAdd = await client.PostAsJsonAsync(
            "/api/reading-sessions/active",
            new { bookId = seeded.BookId });
        Assert.Equal(HttpStatusCode.Created, startAfterReAdd.StatusCode);
        Assert.Equal(20, (await ReadDataAsync(startAfterReAdd)).GetProperty("startPage").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync("/api/reading-sessions/active")).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.DeleteAsync($"/api/library/{seeded.LibraryItemId}")).StatusCode);
        var reAddAsWant = await client.PostAsJsonAsync("/api/library", new
        {
            bookId = seeded.BookId,
            shelf = "WANT_TO_READ"
        });
        Assert.Equal(HttpStatusCode.Created, reAddAsWant.StatusCode);
        var wantedItem = await ReadDataAsync(reAddAsWant);
        Assert.Equal(seeded.LibraryItemId, wantedItem.GetProperty("id").GetGuid());
        Assert.Equal("WANT_TO_READ", wantedItem.GetProperty("shelf").GetString());
        Assert.Equal(0, wantedItem.GetProperty("currentPage").GetInt32());

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.DeleteAsync($"/api/library/{seeded.LibraryItemId}")).StatusCode);
        var reAddAsRead = await client.PostAsJsonAsync("/api/library", new
        {
            bookId = seeded.BookId,
            shelf = "READ"
        });
        Assert.Equal(HttpStatusCode.Created, reAddAsRead.StatusCode);
        var readItem = await ReadDataAsync(reAddAsRead);
        Assert.Equal(seeded.LibraryItemId, readItem.GetProperty("id").GetGuid());
        Assert.Equal("READ", readItem.GetProperty("shelf").GetString());
        Assert.Equal(50, readItem.GetProperty("currentPage").GetInt32());

        var repeatedCancel = await client.DeleteAsync("/api/reading-sessions/active");
        Assert.Equal(HttpStatusCode.NotFound, repeatedCancel.StatusCode);
        Assert.Equal(
            "ACTIVE_READING_SESSION_NOT_FOUND",
            (await ReadEnvelopeAsync(repeatedCancel)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Completed_session_correction_is_owner_only_and_never_reapplies_old_pages()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateFactory(clock);
        var ownerAuth = await RegisterAsync(factory, "focus-correction-owner");
        using var owner = ownerAuth.Client;
        var otherAuth = await RegisterAsync(factory, "focus-correction-other");
        using var other = otherAuth.Client;
        var bookId = await AddBookAsync(factory, 100);
        var goalId = await CreatePageGoalAsync(owner, clock.GetUtcNow(), 12);
        var startedAt = clock.GetUtcNow().AddMinutes(-30);

        var createResponse = await owner.PostAsJsonAsync("/api/reading-sessions", new
        {
            bookId,
            startedAt,
            durationMinutes = 20,
            pagesRead = 10,
            note = "Ghi chú ban đầu"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var sessionId = (await ReadDataAsync(createResponse)).GetProperty("id").GetGuid();
        Assert.Equal(10, await GetLibraryPageAsync(owner, bookId));

        var active = await owner.PostAsJsonAsync(
            "/api/reading-sessions/active",
            new { bookId });
        Assert.Equal(HttpStatusCode.Created, active.StatusCode);
        var blockedIncrease = await owner.PatchAsJsonAsync($"/api/reading-sessions/{sessionId}", new
        {
            startedAt,
            durationMinutes = 20,
            pagesRead = 12,
            note = "Không được tăng khi Focus đang chạy"
        });
        Assert.Equal(HttpStatusCode.Conflict, blockedIncrease.StatusCode);
        Assert.Equal(
            "ACTIVE_READING_SESSION_EXISTS",
            (await ReadEnvelopeAsync(blockedIncrease)).GetProperty("code").GetString());
        var unchangedSession = Assert.Single(
            (await GetDataAsync(owner, "/api/reading-sessions"))
            .GetProperty("items")
            .EnumerateArray());
        Assert.Equal(10, unchangedSession.GetProperty("pagesRead").GetInt32());
        Assert.Equal("Ghi chú ban đầu", unchangedSession.GetProperty("note").GetString());
        Assert.Equal(10, await GetLibraryPageAsync(owner, bookId));

        await CorrectAndAssertPageAsync(owner, sessionId, startedAt, 5, "Đã sửa xuống", bookId, 10);
        await CorrectAndAssertPageAsync(owner, sessionId, startedAt, 8, "Vẫn dưới đỉnh cũ", bookId, 10);
        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync("/api/reading-sessions/active")).StatusCode);
        const string privateCorrection = "CORRECTED_PRIVATE_NOTE_MUST_NOT_ENTER_FEED";
        await CorrectAndAssertPageAsync(owner, sessionId, startedAt, 12, privateCorrection, bookId, 12);

        var forbidden = await other.PatchAsJsonAsync($"/api/reading-sessions/{sessionId}", new
        {
            startedAt,
            durationMinutes = 20,
            pagesRead = 20,
            note = "Không được phép"
        });
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        Assert.Equal(
            "READING_SESSION_NOT_FOUND",
            (await ReadEnvelopeAsync(forbidden)).GetProperty("code").GetString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            var session = db.ReadingSessionSet.Single(x => x.Id == sessionId);
            var goal = db.Set<ReadingGoal>().Single(x => x.Id == goalId);
            Assert.Equal(12, session.AppliedPagesHighWater);
            Assert.NotNull(goal.CompletedAt);
        }

        var feed = await owner.GetAsync("/api/feed?type=READING&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
        Assert.DoesNotContain(privateCorrection, await feed.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrent_double_start_creates_exactly_one_active_session()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateFactory(clock);
        var auth = await RegisterAsync(factory, "focus-race");
        using var client = auth.Client;
        var bookId = await AddBookAsync(factory, 100);

        var starts = await Task.WhenAll(
            client.PostAsJsonAsync("/api/reading-sessions/active", new { bookId }),
            client.PostAsJsonAsync("/api/reading-sessions/active", new { bookId }));

        Assert.Equal(1, starts.Count(response => response.StatusCode == HttpStatusCode.Created));
        var conflict = Assert.Single(starts, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(
            "ACTIVE_READING_SESSION_EXISTS",
            (await ReadEnvelopeAsync(conflict)).GetProperty("code").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        Assert.Equal(1, db.ActiveReadingSessionSet.Count(x => x.UserId == auth.UserId));
    }

    [Fact]
    public async Task Deleted_book_keeps_active_timer_pause_resume_and_cancel_recoverable()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateFactory(clock);
        var auth = await RegisterAsync(factory, "focus-deleted-book");
        using var client = auth.Client;
        var bookId = await AddBookAsync(factory, 100);
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync(
                "/api/reading-sessions/active",
                new { bookId })).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
            db.BookSet.Single(x => x.Id == bookId).SoftDelete();
            await db.SaveChangesAsync();
        }

        clock.Advance(TimeSpan.FromSeconds(75));
        var active = await GetDataAsync(client, "/api/reading-sessions/active");
        Assert.Equal(bookId, active.GetProperty("bookId").GetGuid());
        Assert.Equal(JsonValueKind.Null, active.GetProperty("book").ValueKind);
        Assert.Equal(75, active.GetProperty("elapsedSeconds").GetInt64());

        var paused = await client.PostAsync("/api/reading-sessions/active/pause", content: null);
        Assert.Equal(HttpStatusCode.OK, paused.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadDataAsync(paused)).GetProperty("book").ValueKind);
        var resumed = await client.PostAsync("/api/reading-sessions/active/resume", content: null);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadDataAsync(resumed)).GetProperty("book").ValueKind);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.DeleteAsync("/api/reading-sessions/active")).StatusCode);
        Assert.Equal(
            JsonValueKind.Null,
            (await GetDataAsync(client, "/api/reading-sessions/active")).ValueKind);
    }

    [Fact]
    public async Task Concurrent_start_and_same_book_manual_session_never_use_stale_progress()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateFactory(clock);
        var auth = await RegisterAsync(factory, "focus-manual-race");
        using var client = auth.Client;
        var seeded = await AddBookWithProgressAsync(factory, auth.UserId, 100, 10);

        var start = client.PostAsJsonAsync(
            "/api/reading-sessions/active",
            new { bookId = seeded.BookId });
        var manual = client.PostAsJsonAsync("/api/reading-sessions", new
        {
            bookId = seeded.BookId,
            startedAt = clock.GetUtcNow().AddMinutes(-10),
            durationMinutes = 5,
            pagesRead = 5,
            note = "Phiên thủ công cạnh tranh"
        });
        await Task.WhenAll(start, manual);
        var startResponse = await start;
        var manualResponse = await manual;

        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        Assert.Contains(
            manualResponse.StatusCode,
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });

        var active = await GetDataAsync(client, "/api/reading-sessions/active");
        if (manualResponse.StatusCode == HttpStatusCode.Created)
        {
            Assert.Equal(15, active.GetProperty("startPage").GetInt32());
            Assert.Equal(15, await GetLibraryPageAsync(client, seeded.BookId));
            Assert.Equal(
                1,
                (await GetDataAsync(client, "/api/reading-sessions"))
                .GetProperty("totalItems")
                .GetInt32());
        }
        else
        {
            Assert.Equal(
                "ACTIVE_READING_SESSION_EXISTS",
                (await ReadEnvelopeAsync(manualResponse)).GetProperty("code").GetString());
            Assert.Equal(10, active.GetProperty("startPage").GetInt32());
            Assert.Equal(10, await GetLibraryPageAsync(client, seeded.BookId));
            Assert.Equal(
                0,
                (await GetDataAsync(client, "/api/reading-sessions"))
                .GetProperty("totalItems")
                .GetInt32());
        }
    }

    [Fact]
    public async Task Concurrent_start_and_library_mutations_never_orphan_the_active_session()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateFactory(clock);
        var auth = await RegisterAsync(factory, "focus-library-race");
        using var client = auth.Client;
        var removeCase = await AddBookWithProgressAsync(factory, auth.UserId, 100, 20);

        var startAgainstRemove = client.PostAsJsonAsync(
            "/api/reading-sessions/active",
            new { bookId = removeCase.BookId });
        var concurrentRemove = client.DeleteAsync($"/api/library/{removeCase.LibraryItemId}");
        await Task.WhenAll(startAgainstRemove, concurrentRemove);
        var startAgainstRemoveResponse = await startAgainstRemove;
        var concurrentRemoveResponse = await concurrentRemove;

        Assert.Equal(HttpStatusCode.Created, startAgainstRemoveResponse.StatusCode);
        Assert.Contains(
            concurrentRemoveResponse.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        var activeAfterRemoveRace = await GetDataAsync(client, "/api/reading-sessions/active");
        Assert.Equal(removeCase.BookId, activeAfterRemoveRace.GetProperty("bookId").GetGuid());
        Assert.Equal(20, activeAfterRemoveRace.GetProperty("startPage").GetInt32());
        Assert.Equal(20, await GetLibraryPageAsync(client, removeCase.BookId));
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync("/api/reading-sessions/active")).StatusCode);

        var shelfCase = await AddBookWithProgressAsync(factory, auth.UserId, 100, 30);
        var startAgainstShelf = client.PostAsJsonAsync(
            "/api/reading-sessions/active",
            new { bookId = shelfCase.BookId });
        var concurrentShelfChange = client.PatchAsJsonAsync(
            $"/api/library/{shelfCase.LibraryItemId}",
            new { shelf = "WANT_TO_READ" });
        await Task.WhenAll(startAgainstShelf, concurrentShelfChange);
        var startAgainstShelfResponse = await startAgainstShelf;
        var concurrentShelfChangeResponse = await concurrentShelfChange;

        Assert.Equal(HttpStatusCode.Created, startAgainstShelfResponse.StatusCode);
        Assert.Contains(
            concurrentShelfChangeResponse.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        var activeAfterShelfRace = await GetDataAsync(client, "/api/reading-sessions/active");
        Assert.Equal(shelfCase.BookId, activeAfterShelfRace.GetProperty("bookId").GetGuid());
        var library = await GetDataAsync(client, "/api/library?pageSize=100");
        var item = Assert.Single(
            library.GetProperty("items").EnumerateArray(),
            candidate => candidate.GetProperty("bookId").GetGuid() == shelfCase.BookId);
        Assert.Equal("READING", item.GetProperty("shelf").GetString());
    }

    [Fact]
    public async Task Concurrent_focus_finish_and_progress_update_never_regress_library_progress()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = CreateFactory(clock);
        var auth = await RegisterAsync(factory, "focus-progress-race");
        using var client = auth.Client;
        var seeded = await AddBookWithProgressAsync(factory, auth.UserId, 100, 10);
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync(
                "/api/reading-sessions/active",
                new { bookId = seeded.BookId })).StatusCode);
        clock.Advance(TimeSpan.FromMinutes(2));

        var finish = client.PostAsJsonAsync(
            "/api/reading-sessions/active/finish",
            new { endingPage = 30 });
        var progress = client.PatchAsJsonAsync(
            $"/api/library/{seeded.LibraryItemId}/progress",
            new { currentPage = 20 });
        await Task.WhenAll(finish, progress);
        var finishResponse = await finish;
        var progressResponse = await progress;

        Assert.Equal(HttpStatusCode.OK, finishResponse.StatusCode);
        Assert.Contains(
            progressResponse.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        if (progressResponse.StatusCode == HttpStatusCode.Conflict)
        {
            Assert.Equal(
                "READING_PROGRESS_CANNOT_DECREASE",
                (await ReadEnvelopeAsync(progressResponse)).GetProperty("code").GetString());
        }

        Assert.Equal(30, await GetLibraryPageAsync(client, seeded.BookId));
    }

    private static BookSpaceApiFactory CreateFactory(TimeProvider clock) =>
        new(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(clock);
        });

    private static async Task<AuthenticatedClient> RegisterAsync(
        BookSpaceApiFactory factory,
        string prefix)
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"{prefix}-{suffix}@bookspace.local",
            password = "Reader123!",
            displayName = $"Độc giả Focus {suffix[..8]}"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return new AuthenticatedClient(
            client,
            data.GetProperty("user").GetProperty("id").GetGuid());
    }

    private static async Task<Guid> AddBookAndChallengeAsync(
        BookSpaceApiFactory factory,
        Guid userId,
        DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var book = CreateBook(100);
        var challenge = new ReadingChallenge(
            userId,
            "Thử thách Focus",
            null,
            1,
            now.AddDays(-1),
            now.AddDays(1),
            null,
            true);
        db.AddRange(book, challenge);
        db.Add(new ChallengeParticipation(challenge.Id, userId));
        await db.SaveChangesAsync();
        return book.Id;
    }

    private static async Task<Guid> AddBookAsync(BookSpaceApiFactory factory, int pageCount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var book = CreateBook(pageCount);
        db.Add(book);
        await db.SaveChangesAsync();
        return book.Id;
    }

    private static async Task<(Guid BookId, Guid LibraryItemId)> AddBookWithProgressAsync(
        BookSpaceApiFactory factory,
        Guid userId,
        int pageCount,
        int currentPage)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
        var book = CreateBook(pageCount);
        var item = new LibraryItem(userId, book.Id, LibraryStatus.READING);
        item.UpdateProgress(currentPage, pageCount);
        db.AddRange(book, item);
        await db.SaveChangesAsync();
        return (book.Id, item.Id);
    }

    private static Book CreateBook(int pageCount) =>
        new(
            $"Sách Focus {Guid.NewGuid():N}",
            null,
            null,
            null,
            pageCount,
            2026);

    private static async Task<Guid> CreatePageGoalAsync(
        HttpClient client,
        DateTimeOffset now,
        int target)
    {
        var response = await client.PostAsJsonAsync("/api/reading-goals", new
        {
            metric = "PAGES",
            period = "CUSTOM",
            targetValue = target,
            startDate = now.AddHours(-1),
            endDate = now.AddDays(1)
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadDataAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task CorrectAndAssertPageAsync(
        HttpClient client,
        Guid sessionId,
        DateTimeOffset startedAt,
        int pagesRead,
        string note,
        Guid bookId,
        int expectedLibraryPage)
    {
        var response = await client.PatchAsJsonAsync($"/api/reading-sessions/{sessionId}", new
        {
            startedAt,
            durationMinutes = 20,
            pagesRead,
            note
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var corrected = await ReadDataAsync(response);
        Assert.Equal(pagesRead, corrected.GetProperty("pagesRead").GetInt32());
        Assert.Equal(note, corrected.GetProperty("note").GetString());
        Assert.Equal(expectedLibraryPage, await GetLibraryPageAsync(client, bookId));
    }

    private static async Task<int> GetLibraryPageAsync(HttpClient client, Guid bookId)
    {
        var library = await GetDataAsync(client, "/api/library?pageSize=100");
        return Assert.Single(
                library.GetProperty("items").EnumerateArray(),
                item => item.GetProperty("bookId").GetGuid() == bookId)
            .GetProperty("currentPage")
            .GetInt32();
    }

    private static async Task<JsonElement> GetDataAsync(HttpClient client, string endpoint)
    {
        var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
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

    private static void AssertSameInstant(DateTimeOffset expected, DateTimeOffset actual) =>
        Assert.InRange(
            Math.Abs((expected - actual).Ticks),
            0,
            TimeSpan.FromMilliseconds(1).Ticks);

    private sealed record AuthenticatedClient(HttpClient Client, Guid UserId);

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private readonly object _lock = new();
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_lock)
            {
                return _now;
            }
        }

        public void Advance(TimeSpan duration)
        {
            lock (_lock)
            {
                _now = _now.Add(duration);
            }
        }
    }
}
