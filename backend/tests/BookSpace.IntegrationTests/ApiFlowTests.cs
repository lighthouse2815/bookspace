using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class ApiFlowTests(BookSpaceApiFactory factory) : IClassFixture<BookSpaceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_and_openapi_are_available()
    {
        var health = await _client.GetAsync("/health");
        var openApi = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("Healthy", await health.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
    }

    [Fact]
    public async Task Api_envelope_keeps_null_fields_validation_details_and_not_found_shape()
    {
        var success = await _client.GetAsync("/api/books");
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        var successEnvelope = await ReadEnvelopeAsync(success);
        Assert.True(successEnvelope.GetProperty("success").GetBoolean());
        Assert.True(successEnvelope.TryGetProperty("data", out _));
        Assert.Equal(JsonValueKind.Null, successEnvelope.GetProperty("code").ValueKind);

        var validation = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "weak",
            displayName = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, validation.StatusCode);
        var validationEnvelope = await ReadEnvelopeAsync(validation);
        Assert.False(validationEnvelope.GetProperty("success").GetBoolean());
        Assert.Equal("VALIDATION_ERROR", validationEnvelope.GetProperty("code").GetString());
        var validationErrors = validationEnvelope.GetProperty("data").GetProperty("errors");
        Assert.True(validationErrors.EnumerateObject().Any());
        Assert.True(validationErrors.TryGetProperty("email", out _));

        var missing = await _client.GetAsync("/api/route-that-does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var missingEnvelope = await ReadEnvelopeAsync(missing);
        Assert.False(missingEnvelope.GetProperty("success").GetBoolean());
        Assert.Equal("ROUTE_NOT_FOUND", missingEnvelope.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, missingEnvelope.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Seeded_reader_can_login_and_load_core_product_pages()
    {
        await LoginAsync("reader@bookspace.local", "Reader123!");

        foreach (var endpoint in new[]
                 {
                     "/api/books",
                     "/api/authors",
                     "/api/categories",
                     "/api/library",
                     "/api/reading-sessions",
                     "/api/feed",
                     "/api/clubs",
                     "/api/challenges",
                     "/api/dashboard",
                     "/api/notifications"
                 })
        {
            var response = await _client.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        }
    }

    [Fact]
    public async Task Feed_projects_review_reading_club_and_challenge_activities()
    {
        await LoginAsync("reader@bookspace.local", "Reader123!");
        var challenges = await GetDataAsync("/api/challenges");
        var activeChallenge = challenges.GetProperty("items").EnumerateArray()
            .First(challenge => challenge.GetProperty("isJoined").GetBoolean());
        var challengeId = activeChallenge.GetProperty("id").GetGuid();
        var detail = await _client.GetAsync($"/api/challenges/{challengeId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var feed = await GetDataAsync("/api/feed");
        var types = feed.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .ToList();
        Assert.Contains("REVIEW", types);
        Assert.Contains("READING_PROGRESS", types);
        Assert.Contains("CLUB_POST", types);
        Assert.Contains("CHALLENGE", types);
    }

    [Fact]
    public async Task Review_likes_and_unfollows_are_idempotent()
    {
        await LoginAsync("admin@bookspace.local", "Admin123!");
        var adminId = (await GetDataAsync("/api/auth/me")).GetProperty("id").GetGuid();
        var books = await GetDataAsync("/api/books");
        var bookId = books.GetProperty("items").EnumerateArray()
            .First(book => book.GetProperty("reviewCount").GetInt32() > 0)
            .GetProperty("id")
            .GetGuid();
        var reviews = await GetDataAsync($"/api/reviews?bookId={bookId}");
        var reviewId = reviews.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/reviews/{reviewId}/like", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/reviews/{reviewId}/like", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"/api/reviews/{reviewId}/like")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"/api/reviews/{reviewId}/like")).StatusCode);

        await LoginAsync("reader@bookspace.local", "Reader123!");
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"/api/users/{adminId}/follow")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"/api/users/{adminId}/follow")).StatusCode);
    }

    [Fact]
    public async Task Club_feed_visibility_and_owner_comment_moderation_follow_product_rules()
    {
        await LoginAsync("admin@bookspace.local", "Admin123!");
        var adminId = (await GetDataAsync("/api/auth/me")).GetProperty("id").GetGuid();
        var publicClubResponse = await _client.PostAsJsonAsync("/api/clubs", new
        {
            name = $"Câu lạc bộ kiểm thử {Guid.NewGuid():N}",
            description = "Không gian công khai dùng để kiểm thử phân quyền bình luận.",
            isPrivate = false
        });
        Assert.Equal(HttpStatusCode.Created, publicClubResponse.StatusCode);
        var publicClubId = (await ReadDataAsync(publicClubResponse)).GetProperty("id").GetGuid();
        var postResponse = await _client.PostAsJsonAsync($"/api/clubs/{publicClubId}/posts", new
        {
            content = "Bài viết công khai dùng để kiểm thử quyền moderation của chủ câu lạc bộ."
        });
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var publicPostId = (await ReadDataAsync(postResponse)).GetProperty("id").GetGuid();

        var privateClubResponse = await _client.PostAsJsonAsync("/api/clubs", new
        {
            name = $"Nhật ký riêng {Guid.NewGuid():N}",
            description = "Không gian riêng tư không được phép rò rỉ vào bảng tin người theo dõi.",
            isPrivate = true
        });
        Assert.Equal(HttpStatusCode.Created, privateClubResponse.StatusCode);
        var privateClubId = (await ReadDataAsync(privateClubResponse)).GetProperty("id").GetGuid();
        var privatePostResponse = await _client.PostAsJsonAsync($"/api/clubs/{privateClubId}/posts", new
        {
            content = "Bài viết riêng tư không được xuất hiện trên feed của người ngoài."
        });
        Assert.Equal(HttpStatusCode.Created, privatePostResponse.StatusCode);
        var privatePostId = (await ReadDataAsync(privatePostResponse)).GetProperty("id").GetGuid();

        await LoginAsync("reader@bookspace.local", "Reader123!");
        var followAdmin = await _client.PostAsync($"/api/users/{adminId}/follow", null);
        Assert.True(followAdmin.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/clubs/{publicClubId}/join", null)).StatusCode);
        var commentResponse = await _client.PostAsJsonAsync(
            $"/api/clubs/posts/{publicPostId}/comments",
            new { content = "Bình luận của thành viên để chủ câu lạc bộ nghiệm thu moderation." });
        Assert.Equal(HttpStatusCode.Created, commentResponse.StatusCode);
        var commentId = (await ReadDataAsync(commentResponse)).GetProperty("id").GetGuid();
        var readerFeed = await GetDataAsync("/api/feed");
        Assert.DoesNotContain(
            readerFeed.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == privatePostId);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/clubs/{privateClubId}")).StatusCode);

        await LoginAsync("admin@bookspace.local", "Admin123!");
        var notifications = await GetDataAsync("/api/notifications");
        Assert.Contains(
            notifications.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("type").GetString() == "CLUB" &&
                    item.GetProperty("message").GetString()!.Contains("bình luận", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/clubs/post-comments/{commentId}")).StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_returns_consistent_unauthorized_envelope()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/dashboard");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("UNAUTHORIZED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Admin_routes_reject_a_reader_with_a_consistent_forbidden_envelope()
    {
        await LoginAsync("reader@bookspace.local", "Reader123!");

        var response = await _client.PostAsJsonAsync("/api/admin/authors", new
        {
            name = "Tác giả không được phép tạo"
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("FORBIDDEN", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Disabled_external_catalog_returns_a_controlled_independent_result()
    {
        var response = await _client.GetAsync("/api/external-books/search?query=clean%20code&limit=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.False(data.GetProperty("available").GetBoolean());
        Assert.Equal("bookstore", data.GetProperty("provider").GetString());
        Assert.Empty(data.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Refresh_token_is_rotated_and_revoked_on_logout()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "reader@bookspace.local",
            password = "Reader123!"
        });
        var firstSession = await ReadDataAsync(login);
        var firstRefresh = firstSession.GetProperty("refreshToken").GetString();

        var refreshed = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = firstRefresh });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var secondSession = await ReadDataAsync(refreshed);
        var secondRefresh = secondSession.GetProperty("refreshToken").GetString();
        Assert.NotEqual(firstRefresh, secondRefresh);

        var reuseOld = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = firstRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseOld.StatusCode);

        var logout = await _client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = secondRefresh });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        var reuseLoggedOut = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = secondRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseLoggedOut.StatusCode);
    }

    [Fact]
    public async Task Public_social_payload_hides_email_and_club_detail_contains_posts()
    {
        await LoginAsync("reader@bookspace.local", "Reader123!");
        var feed = await GetDataAsync("/api/feed");
        var firstFeedItem = feed.GetProperty("items").EnumerateArray().First();
        Assert.Equal(JsonValueKind.Null, firstFeedItem.GetProperty("actor").GetProperty("email").ValueKind);

        _client.DefaultRequestHeaders.Authorization = null;
        var clubs = await GetDataAsync("/api/clubs");
        var clubId = clubs.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var club = await GetDataAsync($"/api/clubs/{clubId}");
        Assert.True(club.TryGetProperty("posts", out var posts));
        Assert.NotEmpty(posts.EnumerateArray());
        Assert.Equal(JsonValueKind.Null, club.GetProperty("owner").GetProperty("email").ValueKind);
    }

    [Fact]
    public async Task Reader_can_add_book_update_progress_and_record_session()
    {
        await LoginAsync("reader@bookspace.local", "Reader123!");
        var books = await GetDataAsync("/api/books");
        var library = await GetDataAsync("/api/library");
        var existingIds = library.GetProperty("items")
            .EnumerateArray()
            .Select(x => x.GetProperty("bookId").GetGuid())
            .ToHashSet();
        var book = books.GetProperty("items")
            .EnumerateArray()
            .First(x => !existingIds.Contains(x.GetProperty("id").GetGuid()));
        var bookId = book.GetProperty("id").GetGuid();

        var addResponse = await _client.PostAsJsonAsync("/api/library", new
        {
            bookId,
            shelf = "READING"
        });
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var added = await ReadDataAsync(addResponse);
        var itemId = added.GetProperty("id").GetGuid();

        var progressResponse = await _client.PatchAsJsonAsync($"/api/library/{itemId}", new
        {
            currentPage = 20
        });
        Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
        Assert.Equal(20, (await ReadDataAsync(progressResponse)).GetProperty("currentPage").GetInt32());

        var sessionResponse = await _client.PostAsJsonAsync("/api/reading-sessions", new
        {
            bookId,
            startedAt = DateTimeOffset.UtcNow.AddHours(-1),
            durationMinutes = 30,
            pagesRead = 10,
            note = "Phiên đọc từ kiểm thử tích hợp."
        });
        Assert.Equal(HttpStatusCode.Created, sessionResponse.StatusCode);
    }

    [Fact]
    public async Task New_reader_can_complete_social_club_challenge_and_notification_flows()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"reader-{suffix}@bookspace.local",
            password = "Reader123!",
            displayName = $"Độc giả {suffix}"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registered = await ReadDataAsync(registerResponse);
        var userId = registered.GetProperty("user").GetProperty("id").GetGuid();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", registered.GetProperty("accessToken").GetString());

        var profileResponse = await _client.PatchAsJsonAsync("/api/users/me", new
        {
            displayName = $"Độc giả kiểm thử {suffix}",
            bio = "Tài khoản dùng để nghiệm thu các luồng xã hội.",
            avatarUrl = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        Assert.Equal(
            "Tài khoản dùng để nghiệm thu các luồng xã hội.",
            (await ReadDataAsync(profileResponse)).GetProperty("bio").GetString());

        var books = await GetDataAsync("/api/books");
        var bookId = books.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var reviewResponse = await _client.PostAsJsonAsync("/api/reviews", new
        {
            bookId,
            rating = 4,
            content = "Một đánh giá được tạo từ kiểm thử tích hợp.",
            containsSpoilers = false
        });
        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);
        var reviewId = (await ReadDataAsync(reviewResponse)).GetProperty("id").GetGuid();

        var updateReviewResponse = await _client.PutAsJsonAsync($"/api/reviews/{reviewId}", new
        {
            rating = 5,
            content = "Nội dung đánh giá đã được cập nhật.",
            containsSpoilers = false
        });
        Assert.Equal(HttpStatusCode.OK, updateReviewResponse.StatusCode);
        Assert.Equal(5, (await ReadDataAsync(updateReviewResponse)).GetProperty("rating").GetInt32());

        var likeResponse = await _client.PostAsync($"/api/reviews/{reviewId}/like", null);
        Assert.Equal(HttpStatusCode.OK, likeResponse.StatusCode);
        Assert.True((await ReadDataAsync(likeResponse)).GetProperty("likedByCurrentUser").GetBoolean());

        var reviewCommentResponse = await _client.PostAsJsonAsync(
            $"/api/reviews/{reviewId}/comments",
            new { content = "Bình luận kiểm thử cho đánh giá." });
        Assert.Equal(HttpStatusCode.Created, reviewCommentResponse.StatusCode);
        var reviewCommentId = (await ReadDataAsync(reviewCommentResponse)).GetProperty("id").GetGuid();

        var comments = await GetDataAsync($"/api/reviews/{reviewId}/comments");
        Assert.Contains(
            comments.GetProperty("items").EnumerateArray(),
            comment => comment.GetProperty("id").GetGuid() == reviewCommentId);

        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/review-comments/{reviewCommentId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/reviews/{reviewId}/like")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/reviews/{reviewId}")).StatusCode);

        var clubs = await GetDataAsync("/api/clubs");
        var club = clubs.GetProperty("items").EnumerateArray().First();
        var clubId = club.GetProperty("id").GetGuid();
        var clubOwnerId = club.GetProperty("owner").GetProperty("id").GetGuid();

        var followResponse = await _client.PostAsync($"/api/users/{clubOwnerId}/follow", null);
        Assert.Equal(HttpStatusCode.OK, followResponse.StatusCode);
        Assert.True((await ReadDataAsync(followResponse)).GetProperty("isFollowing").GetBoolean());
        var followers = await GetDataAsync($"/api/users/{clubOwnerId}/followers");
        Assert.Contains(
            followers.GetProperty("items").EnumerateArray(),
            follower => follower.GetProperty("id").GetGuid() == userId);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/users/{clubOwnerId}/follow")).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PostAsync($"/api/clubs/{clubId}/join", null)).StatusCode);
        var postResponse = await _client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/posts",
            new { content = "Bài viết kiểm thử trong câu lạc bộ." });
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var postId = (await ReadDataAsync(postResponse)).GetProperty("id").GetGuid();

        var postCommentResponse = await _client.PostAsJsonAsync(
            $"/api/clubs/posts/{postId}/comments",
            new { content = "Bình luận kiểm thử trong câu lạc bộ." });
        Assert.Equal(HttpStatusCode.Created, postCommentResponse.StatusCode);
        var postCommentId = (await ReadDataAsync(postCommentResponse)).GetProperty("id").GetGuid();
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/clubs/post-comments/{postCommentId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/clubs/posts/{postId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/clubs/{clubId}/join")).StatusCode);

        var challenges = await GetDataAsync("/api/challenges");
        var challengeId = challenges.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PostAsync($"/api/challenges/{challengeId}/join", null)).StatusCode);
        var removedProgressEndpoint = await _client.PatchAsJsonAsync(
            $"/api/challenges/{challengeId}/progress",
            new { currentBooks = 1 });
        Assert.Equal(HttpStatusCode.NotFound, removedProgressEndpoint.StatusCode);
        var myChallenges = await GetDataAsync("/api/challenges/my");
        Assert.Contains(
            myChallenges.GetProperty("items").EnumerateArray(),
            challenge => challenge.GetProperty("id").GetGuid() == challengeId);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/challenges/{challengeId}/join")).StatusCode);

        var unreadCount = await GetDataAsync("/api/notifications/unread-count");
        Assert.True(unreadCount.GetProperty("count").GetInt32() >= 0);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PatchAsync("/api/notifications/read-all", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_author_category_book_and_challenge()
    {
        await LoginAsync("admin@bookspace.local", "Admin123!");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var authorResponse = await _client.PostAsJsonAsync("/api/admin/authors", new
        {
            name = $"Tác giả kiểm thử {suffix}",
            biography = "Dữ liệu kiểm thử tích hợp."
        });
        Assert.Equal(HttpStatusCode.Created, authorResponse.StatusCode);
        var authorId = (await ReadDataAsync(authorResponse)).GetProperty("id").GetGuid();

        var categoryResponse = await _client.PostAsJsonAsync("/api/admin/categories", new
        {
            name = $"Thể loại kiểm thử {suffix}",
            description = "Dữ liệu kiểm thử tích hợp."
        });
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        var categoryId = (await ReadDataAsync(categoryResponse)).GetProperty("id").GetGuid();

        var bookResponse = await _client.PostAsJsonAsync("/api/admin/books", new
        {
            title = $"Cuốn sách kiểm thử {suffix}",
            authorId,
            categoryIds = new[] { categoryId },
            description = "Cuốn sách được tạo bởi kiểm thử tích hợp.",
            isbn = $"TEST-{suffix}",
            pageCount = 180,
            publishedYear = 2026
        });
        Assert.Equal(HttpStatusCode.Created, bookResponse.StatusCode);

        var challengeResponse = await _client.PostAsJsonAsync("/api/admin/challenges", new
        {
            title = $"Thử thách kiểm thử {suffix}",
            description = "Hoàn thành mục tiêu đọc.",
            startDate = DateTimeOffset.UtcNow.AddDays(-1),
            endDate = DateTimeOffset.UtcNow.AddMonths(1),
            goalBooks = 3
        });
        Assert.Equal(HttpStatusCode.Created, challengeResponse.StatusCode);

        var createdChallenge = await ReadDataAsync(challengeResponse);
        var challengeId = createdChallenge.GetProperty("id").GetGuid();
        Assert.False(createdChallenge.GetProperty("isPublished").GetBoolean());

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/challenges/{challengeId}")).StatusCode);

        var publicChallenges = await GetDataAsync("/api/challenges");
        Assert.DoesNotContain(
            publicChallenges.GetProperty("items").EnumerateArray(),
            challenge => challenge.GetProperty("id").GetGuid() == challengeId);

        var adminChallenges = await GetDataAsync("/api/admin/challenges");
        Assert.Contains(
            adminChallenges.GetProperty("items").EnumerateArray(),
            challenge => challenge.GetProperty("id").GetGuid() == challengeId &&
                         !challenge.GetProperty("isPublished").GetBoolean());

        await LoginAsync("reader@bookspace.local", "Reader123!");
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await _client.PostAsync($"/api/challenges/{challengeId}/join", null)).StatusCode);

        await LoginAsync("admin@bookspace.local", "Admin123!");
        var publishResponse = await _client.PatchAsJsonAsync(
            $"/api/admin/challenges/{challengeId}/publish",
            new { isPublished = true });
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        Assert.True((await ReadDataAsync(publishResponse)).GetProperty("isPublished").GetBoolean());

        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.GetAsync($"/api/challenges/{challengeId}")).StatusCode);

        var lockedUpdate = await _client.PatchAsJsonAsync($"/api/admin/challenges/{challengeId}", new
        {
            title = $"Thử thách kiểm thử đã đổi {suffix}",
            description = "Không thể đổi luật khi thử thách đã xuất bản.",
            startDate = DateTimeOffset.UtcNow.AddDays(-2),
            endDate = DateTimeOffset.UtcNow.AddMonths(1),
            goalBooks = 4
        });
        Assert.Equal(HttpStatusCode.Conflict, lockedUpdate.StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await _client.DeleteAsync($"/api/admin/challenges/{challengeId}")).StatusCode);
    }

    [Fact]
    public async Task Challenge_progress_is_derived_before_reads_and_completion_notification_is_idempotent()
    {
        await LoginAsync("admin@bookspace.local", "Admin123!");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var authorId = (await GetDataAsync("/api/authors"))
            .GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var categoryId = (await GetDataAsync("/api/categories"))
            .GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var bookResponse = await _client.PostAsJsonAsync("/api/admin/books", new
        {
            title = $"Sách tiến độ thử thách {suffix}",
            authorId,
            categoryIds = new[] { categoryId },
            isbn = $"CHAL-{suffix}",
            pageCount = 100,
            publishedYear = 2026
        });
        Assert.Equal(HttpStatusCode.Created, bookResponse.StatusCode);
        var bookId = (await ReadDataAsync(bookResponse)).GetProperty("id").GetGuid();
        var challengeStart = DateTimeOffset.UtcNow.AddSeconds(-1);
        var create = await _client.PostAsJsonAsync("/api/admin/challenges", new
        {
            title = $"Thử thách suy ra tiến độ {suffix}",
            description = "Kiểm tra tiến độ từ thư viện.",
            startDate = challengeStart,
            endDate = DateTimeOffset.UtcNow.AddHours(2),
            goalBooks = 1
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var challengeId = (await ReadDataAsync(create)).GetProperty("id").GetGuid();
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PatchAsJsonAsync(
                $"/api/admin/challenges/{challengeId}/publish",
                new { isPublished = true })).StatusCode);
        var joinAfterReadingCreate = await _client.PostAsJsonAsync("/api/admin/challenges", new
        {
            title = $"Thử thách tham gia sau khi đọc {suffix}",
            description = "Kiểm tra FinishedAt trước JoinedAt vẫn được tính.",
            startDate = challengeStart,
            endDate = DateTimeOffset.UtcNow.AddHours(2),
            goalBooks = 1
        });
        Assert.Equal(HttpStatusCode.Created, joinAfterReadingCreate.StatusCode);
        var joinAfterReadingChallengeId =
            (await ReadDataAsync(joinAfterReadingCreate)).GetProperty("id").GetGuid();
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PatchAsJsonAsync(
                $"/api/admin/challenges/{joinAfterReadingChallengeId}/publish",
                new { isPublished = true })).StatusCode);

        await LoginAsync("reader@bookspace.local", "Reader123!");
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PostAsync($"/api/challenges/{challengeId}/join", null)).StatusCode);
        var addLibraryItem = await _client.PostAsJsonAsync(
            "/api/library",
            new { bookId, shelf = "READ" });
        Assert.Equal(HttpStatusCode.Created, addLibraryItem.StatusCode);
        var libraryItemId = (await ReadDataAsync(addLibraryItem)).GetProperty("id").GetGuid();

        var completionLink = $"/challenges/{challengeId}";
        var notificationsAfterReading = await GetDataAsync("/api/notifications?page=1&pageSize=100");
        Assert.Single(
            notificationsAfterReading.GetProperty("items").EnumerateArray(),
            item =>
                item.GetProperty("type").GetString() == "CHALLENGE" &&
                item.GetProperty("link").GetString() == completionLink);

        var joinAfterReadingResponse = await _client.PostAsync(
            $"/api/challenges/{joinAfterReadingChallengeId}/join",
            null);
        Assert.Equal(HttpStatusCode.OK, joinAfterReadingResponse.StatusCode);
        Assert.Equal(
            1,
            (await ReadDataAsync(joinAfterReadingResponse))
                .GetProperty("currentBooks").GetInt32());

        var concurrentDetails = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => _client.GetAsync($"/api/challenges/{challengeId}")));
        Assert.All(concurrentDetails, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var detail = await ReadDataAsync(concurrentDetails[0]);
        Assert.Equal(1, detail.GetProperty("currentBooks").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, detail.GetProperty("completedAt").ValueKind);

        var challengeList = await GetDataAsync("/api/challenges?page=1&pageSize=100");
        Assert.Equal(
            1,
            challengeList.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == challengeId)
                .GetProperty("currentBooks").GetInt32());
        var myChallenges = await GetDataAsync("/api/challenges/my?page=1&pageSize=100");
        Assert.Equal(
            1,
            myChallenges.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == challengeId)
                .GetProperty("currentBooks").GetInt32());
        var dashboard = await GetDataAsync("/api/dashboard");
        Assert.Equal(
            1,
            dashboard.GetProperty("activeChallenges").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == challengeId)
                .GetProperty("currentBooks").GetInt32());
        await GetDataAsync($"/api/challenges/{challengeId}");

        var notifications = await GetDataAsync("/api/notifications?page=1&pageSize=100");
        Assert.Single(
            notifications.GetProperty("items").EnumerateArray(),
            item =>
                item.GetProperty("type").GetString() == "CHALLENGE" &&
                item.GetProperty("link").GetString() == completionLink);

        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PatchAsJsonAsync(
                $"/api/library/{libraryItemId}",
                new { shelf = "READING" })).StatusCode);
        var afterShelfChange = await GetDataAsync($"/api/challenges/{challengeId}");
        Assert.Equal(1, afterShelfChange.GetProperty("currentBooks").GetInt32());

        var removedProgressEndpoint = await _client.PatchAsJsonAsync(
            $"/api/challenges/{challengeId}/progress",
            new { currentBooks = 0 });
        Assert.Equal(HttpStatusCode.NotFound, removedProgressEndpoint.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/challenges/{challengeId}/join")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.DeleteAsync($"/api/challenges/{challengeId}/join")).StatusCode);
        var afterLeave = await GetDataAsync($"/api/challenges/{challengeId}");
        Assert.False(afterLeave.GetProperty("isJoined").GetBoolean());
        Assert.Equal(0, afterLeave.GetProperty("currentBooks").GetInt32());
    }

    private async Task LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
    }

    private async Task<JsonElement> GetDataAsync(string endpoint)
    {
        var response = await _client.GetAsync(endpoint);
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
}
