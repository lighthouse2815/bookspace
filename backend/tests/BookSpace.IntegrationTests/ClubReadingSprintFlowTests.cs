using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookSpace.IntegrationTests;

public sealed class ClubReadingSprintFlowTests(BookSpaceApiFactory factory)
    : IClassFixture<BookSpaceApiFactory>
{
    [Fact]
    public async Task Manager_can_create_and_update_planned_sprint_while_private_visibility_is_preserved()
    {
        using var owner = await RegisterAsync("sprint-private-owner");
        using var moderator = await RegisterAsync("sprint-private-moderator");
        using var outsider = await RegisterAsync("sprint-private-outsider");
        using var anonymous = factory.CreateClient();
        var clubId = await CreateClubAsync(owner.Client, isPrivate: true);
        await InviteAndAcceptAsync(owner.Client, moderator, clubId);
        await PromoteModeratorAsync(owner.Client, clubId, moderator.Id);
        var book = await GetFirstBookAsync(owner.Client);
        var startsAt = DateTimeOffset.UtcNow.AddDays(2);
        var endsAt = startsAt.AddDays(14);

        var createResponse = await owner.Client.PostAsJsonAsync(
            SprintPath(clubId),
            SprintRequest(
                book.Id,
                "Đọc riêng cùng nhau",
                startsAt,
                endsAt,
                "PAGES",
                Math.Min(100, book.PageCount)));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadDataAsync(createResponse);
        var sprintId = created.GetProperty("id").GetGuid();
        Assert.Equal("PLANNED", created.GetProperty("status").GetString());
        Assert.Equal("PAGES", created.GetProperty("targetUnit").GetString());
        Assert.Equal(book.Id, created.GetProperty("book").GetProperty("id").GetGuid());
        Assert.True(created.GetProperty("permissions").GetProperty("canManage").GetBoolean());

        var updateResponse = await moderator.Client.PatchAsJsonAsync(
            SprintPath(clubId, sprintId),
            SprintRequest(
                book.Id,
                "Đọc riêng đã cập nhật",
                startsAt.AddHours(1),
                endsAt.AddHours(1),
                "PAGES",
                Math.Min(90, book.PageCount)));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadDataAsync(updateResponse);
        Assert.Equal("Đọc riêng đã cập nhật", updated.GetProperty("title").GetString());
        Assert.Equal("MODERATOR", (await GetDataAsync(moderator.Client, $"/api/clubs/{clubId}"))
            .GetProperty("viewerRole")
            .GetString());

        var memberList = await GetDataAsync(
            moderator.Client,
            $"{SprintPath(clubId)}?status=PLANNED&page=1&pageSize=20");
        Assert.Contains(
            memberList.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == sprintId);

        foreach (var client in new[] { outsider.Client, anonymous })
        {
            await AssertFailureAsync(
                await client.GetAsync(SprintPath(clubId)),
                HttpStatusCode.NotFound,
                "CLUB_NOT_FOUND");
            await AssertFailureAsync(
                await client.GetAsync(SprintPath(clubId, sprintId)),
                HttpStatusCode.NotFound,
                "CLUB_NOT_FOUND");
        }

        await AssertFailureAsync(
            await outsider.Client.PostAsJsonAsync(
                SprintPath(clubId),
                SprintRequest(
                    book.Id,
                    "Không được tạo",
                    startsAt,
                    endsAt,
                    "PAGES",
                    Math.Min(50, book.PageCount))),
            HttpStatusCode.NotFound,
            "CLUB_NOT_FOUND");

        var activeResponse = await owner.Client.PostAsJsonAsync(
            SprintPath(clubId),
            SprintRequest(
                book.Id,
                "Sprint đang diễn ra",
                DateTimeOffset.UtcNow.AddHours(-1),
                DateTimeOffset.UtcNow.AddDays(3),
                "PAGES",
                Math.Min(100, book.PageCount)));
        Assert.Equal(HttpStatusCode.Created, activeResponse.StatusCode);
        var activeId = (await ReadDataAsync(activeResponse)).GetProperty("id").GetGuid();
        await AssertFailureAsync(
            await moderator.Client.PatchAsJsonAsync(
                SprintPath(clubId, activeId),
                SprintRequest(
                    book.Id,
                    "Không thể sửa khi active",
                    DateTimeOffset.UtcNow.AddHours(-1),
                    DateTimeOffset.UtcNow.AddDays(4),
                    "PAGES",
                    Math.Min(100, book.PageCount))),
            HttpStatusCode.Conflict,
            "READING_SPRINT_UPDATE_NOT_ALLOWED");
    }

    [Fact]
    public async Task Participation_and_progress_are_idempotent_monotonic_and_support_pages_and_chapters()
    {
        using var owner = await RegisterAsync("sprint-progress-owner");
        using var member = await RegisterAsync("sprint-progress-member");
        using var clubMemberWithoutParticipation =
            await RegisterAsync("sprint-progress-nonparticipant");
        using var outsider = await RegisterAsync("sprint-progress-outsider");
        var clubId = await CreateClubAsync(owner.Client, isPrivate: false);
        await JoinClubAsync(member.Client, clubId);
        await JoinClubAsync(clubMemberWithoutParticipation.Client, clubId);
        var book = await GetFirstBookAsync(owner.Client);
        var pagesTarget = Math.Min(100, book.PageCount);

        await AssertFailureAsync(
            await owner.Client.PostAsJsonAsync(
                SprintPath(clubId),
                SprintRequest(
                    book.Id,
                    "Mục tiêu trang vượt sách",
                    DateTimeOffset.UtcNow.AddHours(-1),
                    DateTimeOffset.UtcNow.AddDays(2),
                    "PAGES",
                    book.PageCount + 1)),
            HttpStatusCode.BadRequest,
            "READING_SPRINT_TARGET_EXCEEDS_BOOK_PAGES");
        await AssertFailureAsync(
            await owner.Client.PostAsJsonAsync(
                SprintPath(clubId),
                SprintRequest(
                    book.Id,
                    "Mục tiêu chương quá lớn",
                    DateTimeOffset.UtcNow.AddHours(-1),
                    DateTimeOffset.UtcNow.AddDays(2),
                    "CHAPTERS",
                    501)),
            HttpStatusCode.BadRequest,
            "READING_SPRINT_CHAPTER_TARGET_TOO_LARGE");

        var pagesSprint = await CreateActiveSprintAsync(
            owner.Client,
            clubId,
            book.Id,
            "Sprint theo trang",
            "PAGES",
            pagesTarget);
        var pagesSprintId = pagesSprint.GetProperty("id").GetGuid();

        await AssertFailureAsync(
            await outsider.Client.PostAsync($"{SprintPath(clubId, pagesSprintId)}/join", null),
            HttpStatusCode.Forbidden,
            "CLUB_MEMBERSHIP_REQUIRED");

        var firstJoin = await PostDataAsync(
            member.Client,
            $"{SprintPath(clubId, pagesSprintId)}/join");
        var participantId = firstJoin.GetProperty("id").GetGuid();
        var repeatedJoin = await PostDataAsync(
            member.Client,
            $"{SprintPath(clubId, pagesSprintId)}/join");
        Assert.Equal(participantId, repeatedJoin.GetProperty("id").GetGuid());
        Assert.True(repeatedJoin.GetProperty("isActive").GetBoolean());

        var firstLeave = await DeleteDataAsync(
            member.Client,
            $"{SprintPath(clubId, pagesSprintId)}/join");
        var repeatedLeave = await DeleteDataAsync(
            member.Client,
            $"{SprintPath(clubId, pagesSprintId)}/join");
        Assert.Equal(participantId, firstLeave.GetProperty("id").GetGuid());
        Assert.Equal(participantId, repeatedLeave.GetProperty("id").GetGuid());
        Assert.False(repeatedLeave.GetProperty("isActive").GetBoolean());

        var rejoined = await PostDataAsync(
            member.Client,
            $"{SprintPath(clubId, pagesSprintId)}/join");
        Assert.Equal(participantId, rejoined.GetProperty("id").GetGuid());
        Assert.True(rejoined.GetProperty("isActive").GetBoolean());

        await AssertFailureAsync(
            await clubMemberWithoutParticipation.Client.PutAsJsonAsync(
                $"{SprintPath(clubId, pagesSprintId)}/progress",
                new { progressValue = 1, note = (string?)null }),
            HttpStatusCode.NotFound,
            "READING_SPRINT_PARTICIPANT_NOT_FOUND");

        var progressValue = Math.Min(50, pagesTarget);
        var progressResponse = await member.Client.PutAsJsonAsync(
            $"{SprintPath(clubId, pagesSprintId)}/progress",
            new { progressValue, note = "Lần cập nhật hợp lệ." });
        Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
        var progressed = await ReadDataAsync(progressResponse);
        Assert.Equal(progressValue, progressed.GetProperty("progressValue").GetInt32());
        Assert.Equal(
            Percent(progressValue, pagesTarget),
            progressed.GetProperty("progressPercent").GetInt32());

        var repeatedProgress = await member.Client.PutAsJsonAsync(
            $"{SprintPath(clubId, pagesSprintId)}/progress",
            new { progressValue, note = "Ghi chú này không tạo check-in thứ hai." });
        Assert.Equal(HttpStatusCode.OK, repeatedProgress.StatusCode);
        var timelineAfterRepeat = await GetDataAsync(
            member.Client,
            $"{SprintPath(clubId, pagesSprintId)}/timeline?page=1&pageSize=20");
        var pageCheckIns = timelineAfterRepeat.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(pageCheckIns);
        Assert.Equal("Lần cập nhật hợp lệ.", pageCheckIns[0].GetProperty("note").GetString());

        await AssertFailureAsync(
            await member.Client.PutAsJsonAsync(
                $"{SprintPath(clubId, pagesSprintId)}/progress",
                new { progressValue = progressValue - 1, note = (string?)null }),
            HttpStatusCode.Conflict,
            "READING_SPRINT_PROGRESS_CANNOT_DECREASE");
        await AssertFailureAsync(
            await member.Client.PutAsJsonAsync(
                $"{SprintPath(clubId, pagesSprintId)}/progress",
                new { progressValue = pagesTarget + 1, note = (string?)null }),
            HttpStatusCode.BadRequest,
            "INVALID_READING_SPRINT_PROGRESS");

        var chapterSprint = await CreateActiveSprintAsync(
            owner.Client,
            clubId,
            book.Id,
            "Sprint theo chương",
            "CHAPTERS",
            10);
        var chapterSprintId = chapterSprint.GetProperty("id").GetGuid();
        await PostDataAsync(member.Client, $"{SprintPath(clubId, chapterSprintId)}/join");
        var chapterProgress = await PutDataAsync(
            member.Client,
            $"{SprintPath(clubId, chapterSprintId)}/progress",
            new { progressValue = 5, note = "Hoàn tất năm chương." });
        Assert.Equal(5, chapterProgress.GetProperty("progressValue").GetInt32());
        Assert.Equal(50, chapterProgress.GetProperty("progressPercent").GetInt32());

        var leaveClubResponse = await member.Client.DeleteAsync($"/api/clubs/{clubId}/join");
        Assert.Equal(HttpStatusCode.OK, leaveClubResponse.StatusCode);
        var detailAfterClubLeave = await GetDataAsync(
            member.Client,
            SprintPath(clubId, chapterSprintId));
        var participationAfterClubLeave = detailAfterClubLeave.GetProperty("viewerParticipation");
        Assert.False(participationAfterClubLeave.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, participationAfterClubLeave.GetProperty("leftAt").ValueKind);
    }

    [Fact]
    public async Task Leaderboard_and_timeline_are_stable_and_private_club_activity_does_not_leak()
    {
        using var owner = await RegisterAsync("sprint-rank-owner");
        using var memberOne = await RegisterAsync("sprint-rank-one");
        using var memberTwo = await RegisterAsync("sprint-rank-two");
        using var outsider = await RegisterAsync("sprint-rank-outsider");
        using var anonymous = factory.CreateClient();
        var clubId = await CreateClubAsync(owner.Client, isPrivate: true);
        await InviteAndAcceptAsync(owner.Client, memberOne, clubId);
        await InviteAndAcceptAsync(owner.Client, memberTwo, clubId);
        var book = await GetFirstBookAsync(owner.Client);
        var sprint = await CreateActiveSprintAsync(
            owner.Client,
            clubId,
            book.Id,
            "Bảng xếp hạng riêng tư",
            "PAGES",
            100);
        var sprintId = sprint.GetProperty("id").GetGuid();

        await PostDataAsync(owner.Client, $"{SprintPath(clubId, sprintId)}/join");
        await PostDataAsync(memberOne.Client, $"{SprintPath(clubId, sprintId)}/join");
        await PostDataAsync(memberTwo.Client, $"{SprintPath(clubId, sprintId)}/join");
        await PutDataAsync(
            owner.Client,
            $"{SprintPath(clubId, sprintId)}/progress",
            new { progressValue = 90, note = "Owner đạt 90." });
        await PutDataAsync(
            memberOne.Client,
            $"{SprintPath(clubId, sprintId)}/progress",
            new { progressValue = 60, note = "Thành viên một đạt 60." });
        await PutDataAsync(
            memberTwo.Client,
            $"{SprintPath(clubId, sprintId)}/progress",
            new { progressValue = 60, note = "Thành viên hai đạt 60." });

        var firstBoard = await GetDataAsync(
            owner.Client,
            $"{SprintPath(clubId, sprintId)}/leaderboard?page=1&pageSize=20");
        var firstItems = firstBoard.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, firstItems.Count);
        Assert.Equal(owner.Id, firstItems[0].GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal([90, 60, 60], firstItems.Select(x => x.GetProperty("progressValue").GetInt32()));
        Assert.Equal([90, 60, 60], firstItems.Select(x => x.GetProperty("progressPercent").GetInt32()));
        Assert.Equal([1, 2, 3], firstItems.Select(x => x.GetProperty("rank").GetInt32()));

        var secondBoard = await GetDataAsync(
            owner.Client,
            $"{SprintPath(clubId, sprintId)}/leaderboard?page=1&pageSize=20");
        Assert.Equal(
            firstItems.Select(x => x.GetProperty("id").GetGuid()),
            secondBoard.GetProperty("items").EnumerateArray()
                .Select(x => x.GetProperty("id").GetGuid()));

        var timeline = await GetDataAsync(
            memberOne.Client,
            $"{SprintPath(clubId, sprintId)}/timeline?page=1&pageSize=20");
        Assert.Equal(3, timeline.GetProperty("totalItems").GetInt32());
        Assert.Contains(
            timeline.GetProperty("items").EnumerateArray(),
            item =>
                item.GetProperty("user").GetProperty("id").GetGuid() == memberTwo.Id &&
                item.GetProperty("note").GetString() == "Thành viên hai đạt 60.");

        foreach (var client in new[] { outsider.Client, anonymous })
        {
            await AssertFailureAsync(
                await client.GetAsync($"{SprintPath(clubId, sprintId)}/leaderboard"),
                HttpStatusCode.NotFound,
                "CLUB_NOT_FOUND");
            await AssertFailureAsync(
                await client.GetAsync($"{SprintPath(clubId, sprintId)}/timeline"),
                HttpStatusCode.NotFound,
                "CLUB_NOT_FOUND");
        }

        var kickResponse = await owner.Client.DeleteAsync(
            $"/api/clubs/{clubId}/members/{memberTwo.Id}");
        Assert.Equal(HttpStatusCode.OK, kickResponse.StatusCode);
        var boardAfterKick = await GetDataAsync(
            owner.Client,
            $"{SprintPath(clubId, sprintId)}/leaderboard?page=1&pageSize=20");
        Assert.DoesNotContain(
            boardAfterKick.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("user").GetProperty("id").GetGuid() == memberTwo.Id);
        var timelineAfterKick = await GetDataAsync(
            owner.Client,
            $"{SprintPath(clubId, sprintId)}/timeline?page=1&pageSize=20");
        Assert.Equal(3, timelineAfterKick.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Milestones_and_thread_responses_enforce_manager_participant_and_soft_delete_permissions()
    {
        using var owner = await RegisterAsync("sprint-milestone-owner");
        using var participantOne = await RegisterAsync("sprint-milestone-one");
        using var participantTwo = await RegisterAsync("sprint-milestone-two");
        using var nonParticipant = await RegisterAsync("sprint-milestone-nonparticipant");
        var clubId = await CreateClubAsync(owner.Client, isPrivate: false);
        await JoinClubAsync(participantOne.Client, clubId);
        await JoinClubAsync(participantTwo.Client, clubId);
        await JoinClubAsync(nonParticipant.Client, clubId);
        var book = await GetFirstBookAsync(owner.Client);
        var sprint = await CreateActiveSprintAsync(
            owner.Client,
            clubId,
            book.Id,
            "Thảo luận theo cột mốc",
            "PAGES",
            100);
        var sprintId = sprint.GetProperty("id").GetGuid();
        await PostDataAsync(participantOne.Client, $"{SprintPath(clubId, sprintId)}/join");
        await PostDataAsync(participantTwo.Client, $"{SprintPath(clubId, sprintId)}/join");

        var milestoneResponse = await owner.Client.PostAsJsonAsync(
            $"{SprintPath(clubId, sprintId)}/milestones",
            new
            {
                title = "Nửa chặng đường",
                description = "Trao đổi khi đạt 50 trang.",
                targetValue = 50
            });
        Assert.Equal(HttpStatusCode.Created, milestoneResponse.StatusCode);
        var milestone = await ReadDataAsync(milestoneResponse);
        var milestoneId = milestone.GetProperty("id").GetGuid();

        var updatedMilestone = await owner.Client.PatchAsJsonAsync(
            $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}",
            new
            {
                title = "Mốc thảo luận 50 trang",
                description = "Mô tả đã cập nhật.",
                targetValue = 50
            });
        Assert.Equal(HttpStatusCode.OK, updatedMilestone.StatusCode);
        Assert.Equal(
            "Mốc thảo luận 50 trang",
            (await ReadDataAsync(updatedMilestone)).GetProperty("title").GetString());

        await AssertFailureAsync(
            await participantOne.Client.PatchAsJsonAsync(
                $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}",
                new { title = "Không được sửa", description = (string?)null, targetValue = 40 }),
            HttpStatusCode.Forbidden,
            "CLUB_MANAGEMENT_FORBIDDEN");
        await AssertFailureAsync(
            await nonParticipant.Client.PostAsJsonAsync(
                $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}/responses",
                new { content = "Chưa tham gia sprint." }),
            HttpStatusCode.Forbidden,
            "READING_SPRINT_PARTICIPATION_REQUIRED");

        var responseOne = await PostDataAsync(
            participantOne.Client,
            $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}/responses",
            new { content = "Phản hồi thứ nhất của thành viên một." },
            HttpStatusCode.Created);
        var responseTwo = await PostDataAsync(
            participantOne.Client,
            $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}/responses",
            new { content = "Phản hồi thứ hai của thành viên một." },
            HttpStatusCode.Created);
        var responseThree = await PostDataAsync(
            participantTwo.Client,
            $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}/responses",
            new { content = "Phản hồi của thành viên hai." },
            HttpStatusCode.Created);

        var participantView = await GetDataAsync(
            participantOne.Client,
            $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}/responses?page=1&pageSize=20");
        var participantItems = participantView.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, participantItems.Count);
        Assert.True(participantItems.Single(x =>
            x.GetProperty("id").GetGuid() == responseOne.GetProperty("id").GetGuid())
            .GetProperty("canDelete")
            .GetBoolean());
        Assert.True(participantItems.Single(x =>
            x.GetProperty("id").GetGuid() == responseTwo.GetProperty("id").GetGuid())
            .GetProperty("canDelete")
            .GetBoolean());
        Assert.False(participantItems.Single(x =>
            x.GetProperty("id").GetGuid() == responseThree.GetProperty("id").GetGuid())
            .GetProperty("canDelete")
            .GetBoolean());

        var managerView = await GetDataAsync(
            owner.Client,
            $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}/responses?page=1&pageSize=20");
        Assert.All(
            managerView.GetProperty("items").EnumerateArray(),
            item => Assert.True(item.GetProperty("canDelete").GetBoolean()));

        await AssertFailureAsync(
            await participantTwo.Client.DeleteAsync(
                $"{SprintPath(clubId, sprintId)}/milestone-responses/{responseOne.GetProperty("id").GetGuid()}"),
            HttpStatusCode.Forbidden,
            "READING_SPRINT_RESPONSE_DELETE_FORBIDDEN");

        Assert.Equal(
            HttpStatusCode.OK,
            (await participantOne.Client.DeleteAsync(
                $"{SprintPath(clubId, sprintId)}/milestone-responses/{responseOne.GetProperty("id").GetGuid()}"))
            .StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await owner.Client.DeleteAsync(
                $"{SprintPath(clubId, sprintId)}/milestone-responses/{responseThree.GetProperty("id").GetGuid()}"))
            .StatusCode);
        var responsesAfterDelete = await GetDataAsync(
            owner.Client,
            $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}/responses?page=1&pageSize=20");
        var remainingResponses = responsesAfterDelete.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(remainingResponses);
        Assert.Equal(
            responseTwo.GetProperty("id").GetGuid(),
            remainingResponses[0].GetProperty("id").GetGuid());

        var deleteMilestone = await owner.Client.DeleteAsync(
            $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}");
        Assert.Equal(HttpStatusCode.OK, deleteMilestone.StatusCode);
        var detailAfterDelete = await GetDataAsync(owner.Client, SprintPath(clubId, sprintId));
        Assert.DoesNotContain(
            detailAfterDelete.GetProperty("milestones").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == milestoneId);
        await AssertFailureAsync(
            await owner.Client.GetAsync(
                $"{SprintPath(clubId, sprintId)}/milestones/{milestoneId}/responses"),
            HttpStatusCode.NotFound,
            "READING_SPRINT_MILESTONE_NOT_FOUND");
    }

    [Fact]
    public async Task Reminder_terminal_commands_history_and_disabled_bookstore_are_idempotent()
    {
        using var owner = await RegisterAsync("sprint-lifecycle-owner");
        using var participant = await RegisterAsync("sprint-lifecycle-member");
        var clubId = await CreateClubAsync(owner.Client, isPrivate: false);
        await JoinClubAsync(participant.Client, clubId);
        var external = await GetDataAsync(
            owner.Client,
            "/api/external-books/search?query=reading&limit=5");
        Assert.False(external.GetProperty("available").GetBoolean());
        Assert.Empty(external.GetProperty("items").EnumerateArray());

        var book = await GetFirstBookAsync(owner.Client);
        var completedSprint = await CreateActiveSprintAsync(
            owner.Client,
            clubId,
            book.Id,
            "Sprint sẽ hoàn thành",
            "PAGES",
            100);
        var completedSprintId = completedSprint.GetProperty("id").GetGuid();
        await PostDataAsync(owner.Client, $"{SprintPath(clubId, completedSprintId)}/join");
        await PostDataAsync(participant.Client, $"{SprintPath(clubId, completedSprintId)}/join");
        var milestone = await PostDataAsync(
            owner.Client,
            $"{SprintPath(clubId, completedSprintId)}/milestones",
            new { title = "Mốc terminal", description = (string?)null, targetValue = 50 },
            HttpStatusCode.Created);
        var milestoneId = milestone.GetProperty("id").GetGuid();
        var threadResponse = await PostDataAsync(
            participant.Client,
            $"{SprintPath(clubId, completedSprintId)}/milestones/{milestoneId}/responses",
            new { content = "Phản hồi trước khi hoàn thành sprint." },
            HttpStatusCode.Created);
        var threadResponseId = threadResponse.GetProperty("id").GetGuid();

        var reminderBefore = await CountNotificationsAsync(
            participant.Client,
            completedSprintId,
            "Nhắc tiến độ đợt đọc");
        var firstReminder = await PostDataAsync(
            owner.Client,
            $"{SprintPath(clubId, completedSprintId)}/reminders");
        var reminderTimestamp = firstReminder.GetProperty("lastReminderAt").GetDateTimeOffset();
        Assert.Equal(
            reminderBefore + 1,
            await CountNotificationsAsync(
                participant.Client,
                completedSprintId,
                "Nhắc tiến độ đợt đọc"));
        var repeatedReminder = await PostDataAsync(
            owner.Client,
            $"{SprintPath(clubId, completedSprintId)}/reminders");
        AssertSameInstantWithinPersistencePrecision(
            reminderTimestamp,
            repeatedReminder.GetProperty("lastReminderAt").GetDateTimeOffset());
        Assert.Equal(
            reminderBefore + 1,
            await CountNotificationsAsync(
                participant.Client,
                completedSprintId,
                "Nhắc tiến độ đợt đọc"));

        var completionNotificationsBefore = await CountNotificationsAsync(
            participant.Client,
            completedSprintId,
            "Đợt đọc đã hoàn thành");
        var firstCompletion = await PostDataAsync(
            owner.Client,
            $"{SprintPath(clubId, completedSprintId)}/complete");
        Assert.Equal("COMPLETED", firstCompletion.GetProperty("status").GetString());
        var completedAt = firstCompletion.GetProperty("completedAt").GetDateTimeOffset();
        var repeatedCompletion = await PostDataAsync(
            owner.Client,
            $"{SprintPath(clubId, completedSprintId)}/complete");
        AssertSameInstantWithinPersistencePrecision(
            completedAt,
            repeatedCompletion.GetProperty("completedAt").GetDateTimeOffset());
        Assert.Equal(
            completionNotificationsBefore + 1,
            await CountNotificationsAsync(
                participant.Client,
                completedSprintId,
                "Đợt đọc đã hoàn thành"));

        var currentPayload = SprintRequest(
            book.Id,
            "Không sửa terminal",
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddDays(2),
            "PAGES",
            100);
        await AssertFailureAsync(
            await owner.Client.PatchAsJsonAsync(
                SprintPath(clubId, completedSprintId),
                currentPayload),
            HttpStatusCode.Conflict,
            "READING_SPRINT_UPDATE_NOT_ALLOWED");
        await AssertFailureAsync(
            await owner.Client.PostAsync($"{SprintPath(clubId, completedSprintId)}/join", null),
            HttpStatusCode.Conflict,
            "READING_SPRINT_PARTICIPATION_NOT_ALLOWED");
        await AssertFailureAsync(
            await participant.Client.DeleteAsync($"{SprintPath(clubId, completedSprintId)}/join"),
            HttpStatusCode.Conflict,
            "READING_SPRINT_PARTICIPATION_NOT_ALLOWED");
        await AssertFailureAsync(
            await participant.Client.PutAsJsonAsync(
                $"{SprintPath(clubId, completedSprintId)}/progress",
                new { progressValue = 10, note = (string?)null }),
            HttpStatusCode.Conflict,
            "READING_SPRINT_NOT_ACTIVE");
        await AssertFailureAsync(
            await owner.Client.PostAsJsonAsync(
                $"{SprintPath(clubId, completedSprintId)}/milestones",
                new { title = "Không tạo terminal", description = (string?)null, targetValue = 10 }),
            HttpStatusCode.Conflict,
            "READING_SPRINT_MILESTONE_MUTATION_NOT_ALLOWED");
        await AssertFailureAsync(
            await participant.Client.PostAsJsonAsync(
                $"{SprintPath(clubId, completedSprintId)}/milestones/{milestoneId}/responses",
                new { content = "Không phản hồi terminal." }),
            HttpStatusCode.Conflict,
            "READING_SPRINT_NOT_ACTIVE");
        await AssertFailureAsync(
            await participant.Client.DeleteAsync(
                $"{SprintPath(clubId, completedSprintId)}/milestone-responses/{threadResponseId}"),
            HttpStatusCode.Conflict,
            "READING_SPRINT_NOT_ACTIVE");
        await AssertFailureAsync(
            await owner.Client.PostAsync(
                $"{SprintPath(clubId, completedSprintId)}/reminders",
                null),
            HttpStatusCode.Conflict,
            "READING_SPRINT_NOT_ACTIVE");

        var cancelledSprint = await CreateActiveSprintAsync(
            owner.Client,
            clubId,
            book.Id,
            "Sprint sẽ hủy",
            "CHAPTERS",
            10);
        var cancelledSprintId = cancelledSprint.GetProperty("id").GetGuid();
        var firstCancellation = await PostDataAsync(
            owner.Client,
            $"{SprintPath(clubId, cancelledSprintId)}/cancel");
        Assert.Equal("CANCELLED", firstCancellation.GetProperty("status").GetString());
        var cancelledAt = firstCancellation.GetProperty("cancelledAt").GetDateTimeOffset();
        var repeatedCancellation = await PostDataAsync(
            owner.Client,
            $"{SprintPath(clubId, cancelledSprintId)}/cancel");
        AssertSameInstantWithinPersistencePrecision(
            cancelledAt,
            repeatedCancellation.GetProperty("cancelledAt").GetDateTimeOffset());
        await AssertFailureAsync(
            await owner.Client.PostAsync(
                $"{SprintPath(clubId, cancelledSprintId)}/complete",
                null),
            HttpStatusCode.Conflict,
            "READING_SPRINT_ALREADY_CANCELLED");

        var plannedSprint = await CreateSprintAsync(
            owner.Client,
            clubId,
            book.Id,
            "Sprint sắp diễn ra",
            DateTimeOffset.UtcNow.AddDays(2),
            DateTimeOffset.UtcNow.AddDays(4),
            "CHAPTERS",
            8);
        var plannedSprintId = plannedSprint.GetProperty("id").GetGuid();
        await AssertFailureAsync(
            await owner.Client.PostAsync(
                $"{SprintPath(clubId, plannedSprintId)}/complete",
                null),
            HttpStatusCode.Conflict,
            "READING_SPRINT_NOT_STARTED");

        var completedHistory = await GetDataAsync(
            owner.Client,
            $"{SprintPath(clubId)}?status=COMPLETED&page=1&pageSize=20");
        Assert.Contains(
            completedHistory.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == completedSprintId);
        Assert.All(
            completedHistory.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("COMPLETED", item.GetProperty("status").GetString()));
        var cancelledHistory = await GetDataAsync(
            owner.Client,
            $"{SprintPath(clubId)}?status=CANCELLED&page=1&pageSize=20");
        Assert.Contains(
            cancelledHistory.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == cancelledSprintId);
        Assert.All(
            cancelledHistory.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("CANCELLED", item.GetProperty("status").GetString()));
        var plannedHistory = await GetDataAsync(
            owner.Client,
            $"{SprintPath(clubId)}?status=PLANNED&page=1&pageSize=20");
        Assert.Contains(
            plannedHistory.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == plannedSprintId);

        var repeatedHistory = await GetDataAsync(
            owner.Client,
            $"{SprintPath(clubId)}?status=COMPLETED&page=1&pageSize=20");
        Assert.Equal(
            completedHistory.GetProperty("items").EnumerateArray()
                .Select(x => x.GetProperty("id").GetGuid()),
            repeatedHistory.GetProperty("items").EnumerateArray()
                .Select(x => x.GetProperty("id").GetGuid()));
    }

    private async Task<RegisteredUser> RegisterAsync(string prefix)
    {
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"{prefix}-{suffix}@bookspace.local";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Reader123!",
            displayName = $"{prefix} {suffix[..8]}"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadDataAsync(response);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return new RegisteredUser(
            client,
            data.GetProperty("user").GetProperty("id").GetGuid(),
            email);
    }

    private static async Task<Guid> CreateClubAsync(HttpClient owner, bool isPrivate)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var response = await owner.PostAsJsonAsync("/api/clubs", new
        {
            name = $"CLB Sprint {suffix[..10]}",
            description = "Câu lạc bộ dùng để nghiệm thu đợt đọc chung.",
            coverImageUrl = (string?)null,
            isPrivate
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadDataAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task InviteAndAcceptAsync(
        HttpClient owner,
        RegisteredUser invited,
        Guid clubId)
    {
        var invitationResponse = await owner.PostAsJsonAsync(
            $"/api/clubs/{clubId}/invitations",
            new { email = invited.Email });
        Assert.Equal(HttpStatusCode.Created, invitationResponse.StatusCode);
        var invitationId = (await ReadDataAsync(invitationResponse)).GetProperty("id").GetGuid();
        var acceptResponse = await invited.Client.PostAsync(
            $"/api/clubs/invitations/{invitationId}/accept",
            null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
    }

    private static async Task PromoteModeratorAsync(
        HttpClient owner,
        Guid clubId,
        Guid userId)
    {
        var response = await owner.PatchAsJsonAsync(
            $"/api/clubs/{clubId}/members/{userId}/role",
            new { role = "MODERATOR" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task JoinClubAsync(HttpClient client, Guid clubId)
    {
        var response = await client.PostAsync($"/api/clubs/{clubId}/join", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<BookInfo> GetFirstBookAsync(HttpClient client)
    {
        var books = await GetDataAsync(client, "/api/books?page=1&pageSize=1");
        var book = books.GetProperty("items").EnumerateArray().First();
        return new BookInfo(
            book.GetProperty("id").GetGuid(),
            book.GetProperty("pageCount").GetInt32());
    }

    private static async Task<JsonElement> CreateActiveSprintAsync(
        HttpClient manager,
        Guid clubId,
        Guid bookId,
        string title,
        string targetUnit,
        int targetValue) =>
        await CreateSprintAsync(
            manager,
            clubId,
            bookId,
            title,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddDays(7),
            targetUnit,
            targetValue);

    private static async Task<JsonElement> CreateSprintAsync(
        HttpClient manager,
        Guid clubId,
        Guid bookId,
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string targetUnit,
        int targetValue)
    {
        var response = await manager.PostAsJsonAsync(
            SprintPath(clubId),
            SprintRequest(
                bookId,
                title,
                startsAt,
                endsAt,
                targetUnit,
                targetValue));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static object SprintRequest(
        Guid bookId,
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string targetUnit,
        int targetValue) =>
        new
        {
            bookId,
            title,
            description = $"Mô tả cho {title}.",
            startsAt,
            endsAt,
            targetUnit,
            targetValue
        };

    private static string SprintPath(Guid clubId, Guid? sprintId = null) =>
        sprintId.HasValue
            ? $"/api/clubs/{clubId}/reading-sprints/{sprintId.Value}"
            : $"/api/clubs/{clubId}/reading-sprints";

    private static int Percent(int progressValue, int targetValue) =>
        Math.Clamp(
            (int)Math.Round(progressValue * 100d / targetValue, MidpointRounding.AwayFromZero),
            0,
            100);

    private static void AssertSameInstantWithinPersistencePrecision(
        DateTimeOffset expected,
        DateTimeOffset actual) =>
        Assert.InRange(
            (expected - actual).Duration(),
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(1));

    private static async Task<int> CountNotificationsAsync(
        HttpClient client,
        Guid sprintId,
        string title)
    {
        var notifications = await GetDataAsync(client, "/api/notifications?page=1&pageSize=100");
        return notifications
            .GetProperty("items")
            .EnumerateArray()
            .Count(item =>
                item.GetProperty("type").GetString() == "CLUB" &&
                item.GetProperty("title").GetString() == title &&
                item.GetProperty("link").GetString()?.Contains(
                    sprintId.ToString(),
                    StringComparison.OrdinalIgnoreCase) == true);
    }

    private static async Task<JsonElement> GetDataAsync(HttpClient client, string endpoint)
    {
        var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task<JsonElement> PostDataAsync(
        HttpClient client,
        string endpoint)
    {
        var response = await client.PostAsync(endpoint, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task<JsonElement> PostDataAsync(
        HttpClient client,
        string endpoint,
        object body,
        HttpStatusCode expectedStatus)
    {
        var response = await client.PostAsJsonAsync(endpoint, body);
        Assert.Equal(expectedStatus, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task<JsonElement> PutDataAsync(
        HttpClient client,
        string endpoint,
        object body)
    {
        var response = await client.PutAsJsonAsync(endpoint, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task<JsonElement> DeleteDataAsync(HttpClient client, string endpoint)
    {
        var response = await client.DeleteAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadDataAsync(response);
    }

    private static async Task AssertFailureAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.False(envelope.GetProperty("success").GetBoolean());
        Assert.Equal(expectedCode, envelope.GetProperty("code").GetString());
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

    private sealed record RegisteredUser(HttpClient Client, Guid Id, string Email) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record BookInfo(Guid Id, int PageCount);
}
