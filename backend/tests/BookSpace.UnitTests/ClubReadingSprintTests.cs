using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.UnitTests;

public sealed class ClubReadingSprintTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 30, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Sprint_status_is_derived_in_utc_and_terminal_status_takes_precedence()
    {
        var sprint = CreateDomainSprint(
            Now.AddHours(1),
            Now.AddHours(3));

        Assert.Equal(ReadingSprintStatus.PLANNED, sprint.GetStatus(Now));
        Assert.Equal(ReadingSprintStatus.ACTIVE, sprint.GetStatus(Now.AddHours(1)));
        Assert.Equal(ReadingSprintStatus.ENDED, sprint.GetStatus(Now.AddHours(3)));

        Assert.True(sprint.Cancel(Now.AddMinutes(30)));
        Assert.False(sprint.Cancel(Now.AddMinutes(31)));
        Assert.Equal(ReadingSprintStatus.CANCELLED, sprint.GetStatus(Now.AddDays(10)));
        Assert.Equal(TimeSpan.Zero, sprint.StartsAt.Offset);
        Assert.Equal(TimeSpan.Zero, sprint.EndsAt.Offset);
        Assert.Equal(TimeSpan.Zero, sprint.CancelledAt!.Value.Offset);
    }

    [Fact]
    public void Sprint_rejects_invalid_period_target_and_completion_before_start()
    {
        var invalidPeriod = Assert.Throws<DomainException>(() =>
            CreateDomainSprint(Now.AddHours(2), Now.AddHours(1)));
        var invalidTarget = Assert.Throws<DomainException>(() =>
            new ClubReadingSprint(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Đợt đọc",
                null,
                Now,
                Now.AddDays(1),
                ReadingSprintTargetUnit.PAGES,
                0,
                Now));
        var planned = CreateDomainSprint(Now.AddHours(1), Now.AddHours(2));
        var completion = Assert.Throws<DomainException>(() => planned.Complete(Now));

        Assert.Equal("INVALID_READING_SPRINT_PERIOD", invalidPeriod.Code);
        Assert.Equal("INVALID_READING_SPRINT_TARGET", invalidTarget.Code);
        Assert.Equal("READING_SPRINT_NOT_STARTED", completion.Code);
    }

    [Fact]
    public void Participant_progress_is_monotonic_capped_idempotent_and_preserved_on_rejoin()
    {
        var participant = new ClubReadingSprintParticipant(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now);

        Assert.True(participant.UpdateProgress(40, 100, Now.AddMinutes(1)));
        Assert.False(participant.UpdateProgress(40, 100, Now.AddMinutes(2)));
        var lower = Assert.Throws<DomainException>(() =>
            participant.UpdateProgress(39, 100, Now.AddMinutes(3)));
        var over = Assert.Throws<DomainException>(() =>
            participant.UpdateProgress(101, 100, Now.AddMinutes(3)));
        Assert.True(participant.Leave(Now.AddMinutes(4)));
        Assert.False(participant.Leave(Now.AddMinutes(5)));
        Assert.True(participant.Rejoin(Now.AddMinutes(6)));

        Assert.Equal("READING_SPRINT_PROGRESS_CANNOT_DECREASE", lower.Code);
        Assert.Equal("INVALID_READING_SPRINT_PROGRESS", over.Code);
        Assert.Equal(40, participant.ProgressValue);
        Assert.True(participant.IsActive);

        Assert.True(participant.UpdateProgress(100, 100, Now.AddMinutes(7)));
        var completedAt = participant.CompletedAt;
        Assert.False(participant.UpdateProgress(100, 100, Now.AddMinutes(8)));
        Assert.Equal(completedAt, participant.CompletedAt);
    }

    [Fact]
    public void Milestone_target_must_stay_within_sprint_target()
    {
        var aboveTarget = Assert.Throws<DomainException>(() =>
            new ClubReadingSprintMilestone(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Cột mốc",
                null,
                101,
                100,
                Now));
        var milestone = new ClubReadingSprintMilestone(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Cột mốc",
            null,
            50,
            100,
            Now);
        var invalidUpdate = Assert.Throws<DomainException>(() =>
            milestone.Update("Cột mốc mới", null, 0, 100));

        Assert.Equal("INVALID_READING_SPRINT_MILESTONE_TARGET", aboveTarget.Code);
        Assert.Equal("INVALID_READING_SPRINT_MILESTONE_TARGET", invalidUpdate.Code);
    }

    [Fact]
    public async Task Only_manager_creates_sprint_and_creation_notifies_other_club_members()
    {
        var fixture = SprintFixture.Create();
        var request = fixture.ActiveRequest();

        var forbidden = await Assert.ThrowsAsync<UseCaseException>(() =>
            fixture.Service.CreateAsync(
                fixture.Member.Id,
                fixture.Club.Id,
                request,
                CancellationToken.None));
        var created = await fixture.Service.CreateAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            request,
            CancellationToken.None);

        Assert.Equal("CLUB_MANAGEMENT_FORBIDDEN", forbidden.Code);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal(ReadingSprintStatus.ACTIVE, created.Status);
        Assert.Equal(2, fixture.Db.Set<Notification>().Count);
        Assert.DoesNotContain(
            fixture.Db.Set<Notification>(),
            notification => notification.UserId == fixture.Owner.Id);
    }

    [Fact]
    public async Task Private_club_hides_sprint_from_outsider_before_permission_checks()
    {
        var fixture = SprintFixture.Create();
        var sprint = fixture.AddActiveSprint();

        var hiddenRead = Assert.Throws<UseCaseException>(() =>
            fixture.Service.GetSprint(
                fixture.Club.Id,
                sprint.Id,
                fixture.Outsider.Id));
        var hiddenMutation = await Assert.ThrowsAsync<UseCaseException>(() =>
            fixture.Service.JoinAsync(
                fixture.Outsider.Id,
                fixture.Club.Id,
                sprint.Id,
                CancellationToken.None));

        Assert.Equal("CLUB_NOT_FOUND", hiddenRead.Code);
        Assert.Equal(404, hiddenRead.StatusCode);
        Assert.Equal("CLUB_NOT_FOUND", hiddenMutation.Code);
        Assert.Equal(404, hiddenMutation.StatusCode);
    }

    [Fact]
    public async Task Join_and_progress_are_idempotent_without_duplicate_checkins()
    {
        var fixture = SprintFixture.Create();
        var sprint = fixture.AddActiveSprint();

        var firstJoin = await fixture.Service.JoinAsync(
            fixture.Member.Id,
            fixture.Club.Id,
            sprint.Id,
            CancellationToken.None);
        var membership = fixture.Db.Set<BookClubMember>().Single(x =>
            x.ClubId == fixture.Club.Id &&
            x.UserId == fixture.Member.Id);
        Assert.Equal(Now, sprint.UpdatedAt);
        Assert.Equal(Now, membership.UpdatedAt);

        fixture.TimeProvider.Set(Now.AddMinutes(1));
        var repeatedJoin = await fixture.Service.JoinAsync(
            fixture.Member.Id,
            fixture.Club.Id,
            sprint.Id,
            CancellationToken.None);
        var progressed = await fixture.Service.UpdateProgressAsync(
            fixture.Member.Id,
            fixture.Club.Id,
            sprint.Id,
            new UpdateReadingSprintProgressRequest(60, "Đã đọc phần đầu."),
            CancellationToken.None);
        var repeatedProgress = await fixture.Service.UpdateProgressAsync(
            fixture.Member.Id,
            fixture.Club.Id,
            sprint.Id,
            new UpdateReadingSprintProgressRequest(60, "Không tạo check-in mới."),
            CancellationToken.None);
        var lower = await Assert.ThrowsAsync<UseCaseException>(() =>
            fixture.Service.UpdateProgressAsync(
                fixture.Member.Id,
                fixture.Club.Id,
                sprint.Id,
                new UpdateReadingSprintProgressRequest(59, null),
                CancellationToken.None));

        Assert.Equal(firstJoin.Id, repeatedJoin.Id);
        Assert.Single(fixture.Db.Set<ClubReadingSprintParticipant>());
        Assert.Equal(Now, membership.UpdatedAt);
        Assert.Equal(60, progressed.ProgressValue);
        Assert.Equal(Now.AddMinutes(1), sprint.UpdatedAt);
        Assert.Equal(progressed.Id, repeatedProgress.Id);
        Assert.Single(fixture.Db.Set<ClubReadingSprintCheckIn>());
        Assert.Equal("READING_SPRINT_PROGRESS_CANNOT_DECREASE", lower.Code);
        Assert.Equal(409, lower.StatusCode);

        await fixture.Service.UpdateProgressAsync(
            fixture.Member.Id,
            fixture.Club.Id,
            sprint.Id,
            new UpdateReadingSprintProgressRequest(sprint.TargetValue, null),
            CancellationToken.None);
        var completedDetail = fixture.Service.GetSprint(
            fixture.Club.Id,
            sprint.Id,
            fixture.Member.Id);
        Assert.False(completedDetail.Permissions.CanCheckIn);
        Assert.True(completedDetail.Permissions.CanDiscuss);
    }

    [Fact]
    public async Task Reminder_and_completion_are_idempotent_without_duplicate_notifications()
    {
        var fixture = SprintFixture.Create();
        var sprint = fixture.AddActiveSprint();
        await fixture.Service.JoinAsync(
            fixture.Member.Id,
            fixture.Club.Id,
            sprint.Id,
            CancellationToken.None);

        await fixture.Service.SendReminderAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            sprint.Id,
            CancellationToken.None);
        var afterFirstReminder = fixture.Db.Set<Notification>().Count;
        await fixture.Service.SendReminderAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            sprint.Id,
            CancellationToken.None);

        Assert.Equal(1, afterFirstReminder);
        Assert.Equal(afterFirstReminder, fixture.Db.Set<Notification>().Count);

        await fixture.Service.CompleteAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            sprint.Id,
            CancellationToken.None);
        var afterCompletion = fixture.Db.Set<Notification>().Count;
        var completedAgain = await fixture.Service.CompleteAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            sprint.Id,
            CancellationToken.None);

        Assert.Equal(2, afterCompletion);
        Assert.Equal(afterCompletion, fixture.Db.Set<Notification>().Count);
        Assert.Equal(ReadingSprintStatus.COMPLETED, completedAgain.Status);
    }

    [Fact]
    public async Task Milestone_notification_and_discussion_require_active_participation()
    {
        var fixture = SprintFixture.Create();
        var sprint = fixture.AddActiveSprint();
        await fixture.Service.JoinAsync(
            fixture.Member.Id,
            fixture.Club.Id,
            sprint.Id,
            CancellationToken.None);

        var milestone = await fixture.Service.CreateMilestoneAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            sprint.Id,
            new SaveReadingSprintMilestoneRequest("Phần một", null, 50),
            CancellationToken.None);
        var participationRequired = await Assert.ThrowsAsync<UseCaseException>(() =>
            fixture.Service.AddMilestoneResponseAsync(
                fixture.Moderator.Id,
                fixture.Club.Id,
                sprint.Id,
                milestone.Id,
                new CreateReadingSprintMilestoneResponseRequest("Ý kiến của tôi"),
                CancellationToken.None));
        var response = await fixture.Service.AddMilestoneResponseAsync(
            fixture.Member.Id,
            fixture.Club.Id,
            sprint.Id,
            milestone.Id,
            new CreateReadingSprintMilestoneResponseRequest("Một cột mốc đáng nhớ."),
            CancellationToken.None);

        Assert.Single(fixture.Db.Set<Notification>());
        Assert.Equal(fixture.Member.Id, fixture.Db.Set<Notification>()[0].UserId);
        Assert.Equal(
            "READING_SPRINT_PARTICIPATION_REQUIRED",
            participationRequired.Code);
        Assert.Equal(403, participationRequired.StatusCode);
        Assert.True(response.CanDelete);
    }

    [Fact]
    public async Task Planned_sprint_cannot_change_unit_or_reduce_target_past_existing_data()
    {
        var fixture = SprintFixture.Create();
        var sprint = fixture.AddPlannedSprint();
        var milestone = await fixture.Service.CreateMilestoneAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            sprint.Id,
            new SaveReadingSprintMilestoneRequest("Cột mốc 80", null, 80),
            CancellationToken.None);

        var belowMilestone = await Assert.ThrowsAsync<UseCaseException>(() =>
            fixture.Service.UpdateAsync(
                fixture.Owner.Id,
                fixture.Club.Id,
                sprint.Id,
                fixture.PlannedRequest() with { TargetValue = 79 },
                CancellationToken.None));
        await fixture.Service.DeleteMilestoneAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            sprint.Id,
            milestone.Id,
            CancellationToken.None);
        var unitLocked = await Assert.ThrowsAsync<UseCaseException>(() =>
            fixture.Service.UpdateAsync(
                fixture.Owner.Id,
                fixture.Club.Id,
                sprint.Id,
                fixture.PlannedRequest() with
                {
                    TargetUnit = ReadingSprintTargetUnit.CHAPTERS,
                    TargetValue = 10
                },
                CancellationToken.None));

        Assert.Equal("READING_SPRINT_TARGET_BELOW_MILESTONE", belowMilestone.Code);
        Assert.Equal("READING_SPRINT_TARGET_UNIT_LOCKED", unitLocked.Code);
    }

    [Fact]
    public async Task Removing_club_member_marks_nonterminal_sprint_participation_as_left()
    {
        var fixture = SprintFixture.Create();
        var sprint = fixture.AddActiveSprint();
        var participant = new ClubReadingSprintParticipant(
            sprint.Id,
            fixture.Member.Id,
            Now.AddHours(-1));
        fixture.Db.Add(participant);
        var clubService = new ClubService(fixture.Db, fixture.TimeProvider);

        await clubService.RemoveMemberAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            fixture.Member.Id,
            CancellationToken.None);

        Assert.Equal(Now, participant.LeftAt);
        Assert.False(participant.IsActive);
        Assert.Equal(Now, sprint.UpdatedAt);
    }

    private static ClubReadingSprint CreateDomainSprint(
        DateTimeOffset startsAt,
        DateTimeOffset endsAt) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Đợt đọc kiểm thử",
            null,
            startsAt,
            endsAt,
            ReadingSprintTargetUnit.PAGES,
            100,
            Now);

    private sealed record SprintFixture(
        FakeBookSpaceDbContext Db,
        ClubReadingSprintService Service,
        MutableTimeProvider TimeProvider,
        User Owner,
        User Moderator,
        User Member,
        User Outsider,
        BookClub Club,
        Book Book)
    {
        public static SprintFixture Create()
        {
            var db = new FakeBookSpaceDbContext();
            var owner = new User("owner@bookspace.local", "hash", "Chủ câu lạc bộ");
            var moderator = new User(
                "moderator@bookspace.local",
                "hash",
                "Điều hành viên");
            var member = new User("member@bookspace.local", "hash", "Thành viên");
            var outsider = new User("outsider@bookspace.local", "hash", "Người ngoài");
            var club = new BookClub(
                owner.Id,
                "Câu lạc bộ kiểm thử",
                null,
                null,
                ClubVisibility.PRIVATE);
            var book = new Book(
                "Sách kiểm thử",
                null,
                "9780000000001",
                null,
                300,
                2026);
            db.AddRange([owner, moderator, member, outsider]);
            db.Add(club);
            db.Add(book);
            db.Add(new BookClubMember(club.Id, owner.Id, ClubMemberRole.OWNER));
            db.Add(new BookClubMember(
                club.Id,
                moderator.Id,
                ClubMemberRole.MODERATOR));
            db.Add(new BookClubMember(club.Id, member.Id, ClubMemberRole.MEMBER));
            var timeProvider = new MutableTimeProvider(Now);
            return new SprintFixture(
                db,
                new ClubReadingSprintService(db, timeProvider),
                timeProvider,
                owner,
                moderator,
                member,
                outsider,
                club,
                book);
        }

        public SaveReadingSprintRequest ActiveRequest() =>
            new(
                Book.Id,
                "Đợt đọc đang diễn ra",
                "Mô tả",
                Now.AddDays(-1),
                Now.AddDays(5),
                ReadingSprintTargetUnit.PAGES,
                200);

        public SaveReadingSprintRequest PlannedRequest() =>
            new(
                Book.Id,
                "Đợt đọc sắp tới",
                "Mô tả",
                Now.AddDays(1),
                Now.AddDays(10),
                ReadingSprintTargetUnit.PAGES,
                200);

        public ClubReadingSprint AddActiveSprint()
        {
            var request = ActiveRequest();
            var sprint = new ClubReadingSprint(
                Club.Id,
                request.BookId,
                Owner.Id,
                request.Title,
                request.Description,
                request.StartsAt,
                request.EndsAt,
                request.TargetUnit,
                request.TargetValue,
                Now.AddDays(-2));
            Db.Add(sprint);
            return sprint;
        }

        public ClubReadingSprint AddPlannedSprint()
        {
            var request = PlannedRequest();
            var sprint = new ClubReadingSprint(
                Club.Id,
                request.BookId,
                Owner.Id,
                request.Title,
                request.Description,
                request.StartsAt,
                request.EndsAt,
                request.TargetUnit,
                request.TargetValue,
                Now);
            Db.Add(sprint);
            return sprint;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Set(DateTimeOffset value) => now = value.ToUniversalTime();
    }

    private sealed class FakeBookSpaceDbContext : IBookSpaceDbContext
    {
        private readonly Dictionary<Type, object> _sets = [];

        public IQueryable<User> Users => Query<User>();
        public IQueryable<RefreshToken> RefreshTokens => Query<RefreshToken>();
        public IQueryable<Follow> Follows => Query<Follow>();
        public IQueryable<Author> Authors => Query<Author>();
        public IQueryable<Category> Categories => Query<Category>();
        public IQueryable<Book> Books => Query<Book>();
        public IQueryable<BookAuthor> BookAuthors => Query<BookAuthor>();
        public IQueryable<BookCategory> BookCategories => Query<BookCategory>();
        public IQueryable<LibraryItem> LibraryItems => Query<LibraryItem>();
        public IQueryable<LibraryItem> LibraryItemsIncludingDeleted => Query<LibraryItem>();
        public IQueryable<ReadingSession> ReadingSessions => Query<ReadingSession>();
        public IQueryable<ActiveReadingSession> ActiveReadingSessions => Query<ActiveReadingSession>();
        public IQueryable<Review> Reviews => Query<Review>();
        public IQueryable<ReviewComment> ReviewComments => Query<ReviewComment>();
        public IQueryable<ReviewLike> ReviewLikes => Query<ReviewLike>();
        public IQueryable<BookClub> BookClubs => Query<BookClub>();
        public IQueryable<BookClubMember> BookClubMembers => Query<BookClubMember>();
        public IQueryable<ClubInvitation> ClubInvitations => Query<ClubInvitation>();
        public IQueryable<ClubPost> ClubPosts => Query<ClubPost>();
        public IQueryable<ClubPostComment> ClubPostComments => Query<ClubPostComment>();
        public IQueryable<ClubChatMessage> ClubChatMessages => Query<ClubChatMessage>();
        public IQueryable<ClubChatReadState> ClubChatReadStates => Query<ClubChatReadState>();
        public IQueryable<ClubReadingSprint> ClubReadingSprints =>
            Query<ClubReadingSprint>();
        public IQueryable<ClubReadingSprintParticipant> ClubReadingSprintParticipants =>
            Query<ClubReadingSprintParticipant>();
        public IQueryable<ClubReadingSprintCheckIn> ClubReadingSprintCheckIns =>
            Query<ClubReadingSprintCheckIn>();
        public IQueryable<ClubReadingSprintMilestone> ClubReadingSprintMilestones =>
            Query<ClubReadingSprintMilestone>().Where(x => x.DeletedAt == null);
        public IQueryable<ClubReadingSprintMilestone> ClubReadingSprintMilestonesIncludingDeleted =>
            Query<ClubReadingSprintMilestone>();
        public IQueryable<ClubReadingSprintMilestoneResponse> ClubReadingSprintMilestoneResponses =>
            Query<ClubReadingSprintMilestoneResponse>();
        public IQueryable<ReadingChallenge> ReadingChallenges => Query<ReadingChallenge>();
        public IQueryable<ChallengeParticipation> ChallengeParticipations =>
            Query<ChallengeParticipation>();
        public IQueryable<Notification> Notifications => Query<Notification>();

        public List<T> Set<T>() where T : class
        {
            if (!_sets.TryGetValue(typeof(T), out var set))
            {
                set = new List<T>();
                _sets[typeof(T)] = set;
            }

            return (List<T>)set;
        }

        public void Add<T>(T entity) where T : class => Set<T>().Add(entity);

        public void AddRange<T>(IEnumerable<T> entities) where T : class =>
            Set<T>().AddRange(entities);

        public void Remove<T>(T entity) where T : class => Set<T>().Remove(entity);

        public void RemoveRange<T>(IEnumerable<T> entities) where T : class =>
            Set<T>().RemoveAll(item => entities.Contains(item));

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        private IQueryable<T> Query<T>() where T : class => Set<T>().AsQueryable();
    }
}
