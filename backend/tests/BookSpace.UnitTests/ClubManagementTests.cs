using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.UnitTests;

public sealed class ClubManagementTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 30, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Club_member_role_protects_owner_and_reserved_owner_role()
    {
        var owner = new BookClubMember(Guid.NewGuid(), Guid.NewGuid(), ClubMemberRole.OWNER);
        var member = new BookClubMember(Guid.NewGuid(), Guid.NewGuid(), ClubMemberRole.MEMBER);

        var immutable = Assert.Throws<DomainException>(() => owner.ChangeRole(ClubMemberRole.MEMBER));
        var reserved = Assert.Throws<DomainException>(() => member.ChangeRole(ClubMemberRole.OWNER));
        var invalid = Assert.Throws<DomainException>(() =>
            member.ChangeRole((ClubMemberRole)999));

        Assert.Equal("OWNER_ROLE_IMMUTABLE", immutable.Code);
        Assert.Equal("OWNER_ROLE_RESERVED", reserved.Code);
        Assert.Equal("INVALID_CLUB_MEMBER_ROLE", invalid.Code);
    }

    [Fact]
    public void Club_invitation_lifecycle_is_idempotent_and_expires_in_utc()
    {
        var invitation = new ClubInvitation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now,
            Now.AddDays(7));

        Assert.True(invitation.Accept(Now.AddHours(1)));
        Assert.False(invitation.Accept(Now.AddHours(2)));
        Assert.Equal(ClubInvitationStatus.ACCEPTED, invitation.Status);

        var expiring = new ClubInvitation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now,
            Now.AddMinutes(1));
        Assert.True(expiring.ExpireIfNeeded(Now.AddMinutes(1)));
        Assert.False(expiring.ExpireIfNeeded(Now.AddMinutes(2)));
        Assert.Equal(ClubInvitationStatus.EXPIRED, expiring.Status);
        Assert.Equal(TimeSpan.Zero, expiring.ExpiresAt.Offset);
    }

    [Fact]
    public void Club_chat_message_validates_content_and_read_cursor_never_regresses()
    {
        var invalid = Assert.Throws<DomainException>(() => new ClubChatMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "   ",
            Now));
        Assert.Equal("VALIDATION_ERROR", invalid.Code);
        var tooLong = Assert.Throws<DomainException>(() => new ClubChatMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('a', 2001),
            Now));
        Assert.Equal("VALIDATION_ERROR", tooLong.Code);

        var state = new ClubChatReadState(Guid.NewGuid());
        var olderId = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var newerId = Guid.Parse("20000000-0000-0000-0000-000000000000");
        Assert.True(state.Advance(olderId, Now));
        Assert.True(state.Advance(newerId, Now));
        Assert.False(state.Advance(olderId, Now));
        Assert.False(state.Advance(Guid.NewGuid(), Now.AddMinutes(-1)));
        Assert.Equal(newerId, state.LastReadMessageId);
        Assert.Equal(Now, state.LastReadAt);
    }

    [Fact]
    public async Task Club_chat_persists_message_and_notifications_before_realtime_publish()
    {
        var db = new FakeBookSpaceDbContext();
        var owner = new User("chat-owner@bookspace.local", "hash", "Chat Owner");
        var member = new User("chat-member@bookspace.local", "hash", "Chat Member");
        var club = new BookClub(
            owner.Id,
            "CLB Chat",
            null,
            null,
            ClubVisibility.PUBLIC);
        db.AddRange([owner, member]);
        db.Add(club);
        db.Add(new BookClubMember(club.Id, owner.Id, ClubMemberRole.OWNER));
        db.Add(new BookClubMember(club.Id, member.Id, ClubMemberRole.MEMBER));
        var saved = false;
        db.SaveChangesHandler = _ =>
        {
            saved = true;
            return Task.FromResult(1);
        };
        var boundary = new RecordingClubChatMutationBoundary(() => saved);
        var publisher = new RecordingChatPublisher(() => saved && boundary.Completed);
        var service = new ClubChatService(
            db,
            publisher,
            boundary,
            new FixedTimeProvider(Now));

        var result = await service.SendMessageAsync(
            owner.Id,
            club.Id,
            new SendClubChatMessageRequest("  Xin chào câu lạc bộ  "),
            CancellationToken.None);

        Assert.Equal("Xin chào câu lạc bộ", result.Content);
        Assert.Single(db.Set<ClubChatMessage>());
        var notification = Assert.Single(db.Set<Notification>());
        Assert.Equal(member.Id, notification.UserId);
        Assert.Equal(NotificationType.CLUB, notification.Type);
        Assert.Equal($"/clubs/{club.Id}?tab=chat", notification.Link);
        Assert.Equal(1, publisher.PublishCount);
        Assert.Equal(
            new[] { owner.Id, member.Id }.Order().ToArray(),
            publisher.MemberIds.Order().ToArray());
    }

    [Fact]
    public async Task Club_chat_does_not_publish_when_persistence_fails()
    {
        var db = new FakeBookSpaceDbContext();
        var owner = new User("chat-fail@bookspace.local", "hash", "Chat Fail");
        var club = new BookClub(
            owner.Id,
            "CLB Chat lỗi",
            null,
            null,
            ClubVisibility.PUBLIC);
        db.Add(owner);
        db.Add(club);
        db.Add(new BookClubMember(club.Id, owner.Id, ClubMemberRole.OWNER));
        db.SaveChangesHandler = _ => throw new InvalidOperationException("save failed");
        var publisher = new RecordingChatPublisher(() => false);
        var service = new ClubChatService(
            db,
            publisher,
            new InlineClubChatMutationBoundary(),
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendMessageAsync(
            owner.Id,
            club.Id,
            new SendClubChatMessageRequest("Không được phát realtime"),
            CancellationToken.None));
        Assert.Equal(0, publisher.PublishCount);
    }

    [Fact]
    public async Task Repeated_pending_invite_returns_same_invitation_without_duplicate_notification()
    {
        var fixture = ClubFixture.Create();

        var first = await fixture.Service.InviteAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            new InviteClubMemberRequest(fixture.Member.Email),
            CancellationToken.None);
        var second = await fixture.Service.InviteAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            new InviteClubMemberRequest(fixture.Member.Email.ToUpperInvariant()),
            CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(fixture.Db.Set<ClubInvitation>());
        Assert.Single(fixture.Db.Set<Notification>());
    }

    [Fact]
    public async Task Invitation_can_only_be_accepted_by_recipient_and_repeated_accept_has_no_side_effect()
    {
        var fixture = ClubFixture.Create();
        var invitation = await fixture.Service.InviteAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            new InviteClubMemberRequest(fixture.Member.Email),
            CancellationToken.None);
        var notificationCountAfterInvite = fixture.Db.Set<Notification>().Count;

        var unauthorized = await Assert.ThrowsAsync<UseCaseException>(() =>
            fixture.Service.AcceptInvitationAsync(
                fixture.Moderator.Id,
                invitation.Id,
                CancellationToken.None));
        Assert.Equal(403, unauthorized.StatusCode);
        Assert.Equal("CLUB_INVITATION_FORBIDDEN", unauthorized.Code);

        var accepted = await fixture.Service.AcceptInvitationAsync(
            fixture.Member.Id,
            invitation.Id,
            CancellationToken.None);
        var notificationCountAfterAccept = fixture.Db.Set<Notification>().Count;
        var acceptedAgain = await fixture.Service.AcceptInvitationAsync(
            fixture.Member.Id,
            invitation.Id,
            CancellationToken.None);

        Assert.Equal(ClubMemberRole.MEMBER, accepted.Role);
        Assert.Equal(accepted.Id, acceptedAgain.Id);
        Assert.Equal(notificationCountAfterInvite + 1, notificationCountAfterAccept);
        Assert.Equal(notificationCountAfterAccept, fixture.Db.Set<Notification>().Count);
        Assert.Single(
            fixture.Db.Set<BookClubMember>(),
            membership => membership.UserId == fixture.Member.Id);
    }

    [Fact]
    public async Task Only_owner_changes_roles_and_moderator_cannot_remove_staff()
    {
        var fixture = ClubFixture.Create(addModeratorMembership: true, addMemberMembership: true);

        var forbiddenRoleChange = await Assert.ThrowsAsync<UseCaseException>(() =>
            fixture.Service.UpdateMemberRoleAsync(
                fixture.Moderator.Id,
                fixture.Club.Id,
                fixture.Member.Id,
                new UpdateClubMemberRoleRequest(ClubMemberRole.MODERATOR),
                CancellationToken.None));
        Assert.Equal("CLUB_OWNER_REQUIRED", forbiddenRoleChange.Code);

        var changed = await fixture.Service.UpdateMemberRoleAsync(
            fixture.Owner.Id,
            fixture.Club.Id,
            fixture.Member.Id,
            new UpdateClubMemberRoleRequest(ClubMemberRole.MODERATOR),
            CancellationToken.None);
        Assert.Equal(ClubMemberRole.MODERATOR, changed.Role);

        var forbiddenRemoval = await Assert.ThrowsAsync<UseCaseException>(() =>
            fixture.Service.RemoveMemberAsync(
                fixture.Moderator.Id,
                fixture.Club.Id,
                fixture.Member.Id,
                CancellationToken.None));
        Assert.Equal("MODERATOR_CANNOT_REMOVE_STAFF", forbiddenRemoval.Code);
    }

    [Fact]
    public async Task Repeated_current_book_selection_does_not_duplicate_notifications()
    {
        var fixture = ClubFixture.Create(addModeratorMembership: true, addMemberMembership: true);
        var book = new Book(
            "Sách đọc chung",
            null,
            null,
            null,
            240,
            2026);
        fixture.Db.Add(book);

        var first = await fixture.Service.SetCurrentBookAsync(
            fixture.Moderator.Id,
            fixture.Club.Id,
            new SetClubCurrentBookRequest(book.Id),
            CancellationToken.None);
        var notificationCount = fixture.Db.Set<Notification>().Count;
        var second = await fixture.Service.SetCurrentBookAsync(
            fixture.Moderator.Id,
            fixture.Club.Id,
            new SetClubCurrentBookRequest(book.Id),
            CancellationToken.None);

        Assert.Equal(book.Id, first.CurrentBook?.Id);
        Assert.Equal(book.Id, second.CurrentBook?.Id);
        Assert.Equal(2, notificationCount);
        Assert.Equal(notificationCount, fixture.Db.Set<Notification>().Count);
    }

    private sealed record ClubFixture(
        FakeBookSpaceDbContext Db,
        ClubService Service,
        User Owner,
        User Moderator,
        User Member,
        BookClub Club)
    {
        public static ClubFixture Create(
            bool addModeratorMembership = false,
            bool addMemberMembership = false)
        {
            var db = new FakeBookSpaceDbContext();
            var owner = new User("owner@bookspace.local", "hash", "Chủ câu lạc bộ");
            var moderator = new User("moderator@bookspace.local", "hash", "Điều hành viên");
            var member = new User("member@bookspace.local", "hash", "Thành viên");
            var club = new BookClub(
                owner.Id,
                "Câu lạc bộ kiểm thử",
                null,
                null,
                ClubVisibility.PRIVATE);
            db.AddRange([owner, moderator, member]);
            db.Add(club);
            db.Add(new BookClubMember(club.Id, owner.Id, ClubMemberRole.OWNER));
            if (addModeratorMembership)
            {
                db.Add(new BookClubMember(club.Id, moderator.Id, ClubMemberRole.MODERATOR));
            }

            if (addMemberMembership)
            {
                db.Add(new BookClubMember(club.Id, member.Id, ClubMemberRole.MEMBER));
            }

            return new ClubFixture(
                db,
                new ClubService(db, new FixedTimeProvider(Now)),
                owner,
                moderator,
                member,
                club);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InlineClubChatMutationBoundary : IClubChatMutationBoundary
    {
        public Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);
    }

    private sealed class RecordingClubChatMutationBoundary(Func<bool> persistenceCompleted)
        : IClubChatMutationBoundary
    {
        public bool Completed { get; private set; }

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            var result = await operation(cancellationToken);
            Assert.True(persistenceCompleted());
            Completed = true;
            return result;
        }
    }

    private sealed class RecordingChatPublisher(Func<bool> persistenceCompleted)
        : IClubChatRealtimePublisher
    {
        public int PublishCount { get; private set; }
        public IReadOnlyList<Guid> MemberIds { get; private set; } = [];

        public Task PublishMessageCreatedAsync(
            ClubChatMessageDto message,
            IReadOnlyList<Guid> activeMemberIds,
            CancellationToken cancellationToken)
        {
            Assert.True(persistenceCompleted());
            PublishCount++;
            MemberIds = activeMemberIds;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBookSpaceDbContext : IBookSpaceDbContext
    {
        private readonly Dictionary<Type, object> _sets = [];

        public Func<CancellationToken, Task<int>>? SaveChangesHandler { get; set; }

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
        public IQueryable<ClubReadingSprint> ClubReadingSprints => Query<ClubReadingSprint>();
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
            SaveChangesHandler?.Invoke(cancellationToken) ?? Task.FromResult(1);

        private IQueryable<T> Query<T>() where T : class => Set<T>().AsQueryable();
    }
}
