using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ClubService(
    IBookSpaceDbContext db,
    TimeProvider timeProvider) : IClubService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private readonly ServiceMapper _mapper = new(db);

    public PageResult<ClubSummary> GetClubs(Guid? viewerId, string? search, int page, int pageSize)
    {
        var query = db.BookClubs;
        if (viewerId.HasValue)
        {
            var joinedIds = db.BookClubMembers.Where(x => x.UserId == viewerId.Value).Select(x => x.ClubId);
            query = query.Where(x => x.Visibility == ClubVisibility.PUBLIC || joinedIds.Contains(x.Id));
        }
        else
        {
            query = query.Where(x => x.Visibility == ClubVisibility.PUBLIC);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(keyword));
        }

        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(x => _mapper.Club(x, viewerId))
            .ToList();
        return PageResult<ClubSummary>.Create(items, normalizedPage, size, total);
    }

    public ClubSummary GetClub(Guid clubId, Guid? viewerId)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, viewerId);
        return MapDetail(club, viewerId);
    }

    public async Task<ClubSummary> CreateAsync(
        Guid ownerId,
        CreateClubRequest request,
        CancellationToken cancellationToken)
    {
        EnsureUserExists(ownerId);
        var club = new BookClub(
            ownerId,
            request.Name,
            request.Description,
            request.CoverImageUrl,
            request.IsPrivate ? ClubVisibility.PRIVATE : ClubVisibility.PUBLIC);
        db.Add(club);
        db.Add(new BookClubMember(club.Id, ownerId, ClubMemberRole.OWNER));
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(club, ownerId);
    }

    public async Task<ClubSummary> UpdateAsync(
        Guid ownerId,
        Guid clubId,
        UpdateClubRequest request,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureOwner(club, ownerId);
        club.Update(
            request.Name,
            request.Description,
            request.CoverImageUrl,
            request.IsPrivate ? ClubVisibility.PRIVATE : ClubVisibility.PUBLIC);
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(club, ownerId);
    }

    public async Task JoinAsync(Guid userId, Guid clubId, CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureUserExists(userId);
        if (club.Visibility == ClubVisibility.PRIVATE)
        {
            throw ServiceErrors.Forbidden(
                "PRIVATE_CLUB",
                "Câu lạc bộ riêng tư chỉ nhận thành viên qua lời mời.");
        }

        if (db.BookClubMembers.Any(x => x.ClubId == clubId && x.UserId == userId))
        {
            throw ServiceErrors.Conflict("ALREADY_CLUB_MEMBER", "Bạn đã là thành viên câu lạc bộ.");
        }

        db.Add(new BookClubMember(clubId, userId, ClubMemberRole.MEMBER));
        NotificationDelivery.AddIfEnabled(db, new Notification(
            club.OwnerId,
            NotificationType.CLUB,
            "Thành viên mới",
            $"{GetUserName(userId)} vừa tham gia {club.Name}.",
            $"/clubs/{club.Id}"), userId);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LeaveAsync(Guid userId, Guid clubId, CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        if (club.OwnerId == userId)
        {
            throw ServiceErrors.Conflict(
                "OWNER_CANNOT_LEAVE",
                "Chủ câu lạc bộ không thể rời câu lạc bộ.");
        }

        var membership = db.BookClubMembers.FirstOrDefault(x => x.ClubId == clubId && x.UserId == userId)
                         ?? throw ServiceErrors.NotFound(
                             "CLUB_MEMBERSHIP_NOT_FOUND",
                             "Bạn chưa tham gia câu lạc bộ.");
        LeaveActiveSprintParticipations(clubId, userId);
        membership.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }

    public PageResult<ClubMemberDto> GetMembers(
        Guid clubId,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, viewerId);
        var query = db.BookClubMembers.Where(x => x.ClubId == clubId);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderBy(x => x.Role == ClubMemberRole.OWNER ? 0 : x.Role == ClubMemberRole.MODERATOR ? 1 : 2)
            .ThenBy(x => x.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(_mapper.ClubMember)
            .ToList();
        return PageResult<ClubMemberDto>.Create(items, normalizedPage, size, total);
    }

    public async Task<ClubMemberDto> UpdateMemberRoleAsync(
        Guid ownerId,
        Guid clubId,
        Guid memberUserId,
        UpdateClubMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureOwner(club, ownerId);
        if (!Enum.IsDefined(request.Role) ||
            request.Role is not ClubMemberRole.MODERATOR and not ClubMemberRole.MEMBER)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_CLUB_MEMBER_ROLE",
                "Vai trò chỉ có thể là MODERATOR hoặc MEMBER.");
        }

        var membership = FindMembership(clubId, memberUserId);
        if (membership.Role == ClubMemberRole.OWNER)
        {
            throw ServiceErrors.Conflict(
                "OWNER_ROLE_IMMUTABLE",
                "Không thể thay đổi vai trò của chủ câu lạc bộ.");
        }

        if (membership.Role == request.Role)
        {
            return _mapper.ClubMember(membership);
        }

        membership.ChangeRole(request.Role);
        NotificationDelivery.AddIfEnabled(db, new Notification(
            memberUserId,
            NotificationType.CLUB,
            "Vai trò trong câu lạc bộ đã thay đổi",
            request.Role == ClubMemberRole.MODERATOR
                ? $"Bạn đã trở thành điều hành viên của {club.Name}."
                : $"Vai trò của bạn trong {club.Name} đã được chuyển thành thành viên.",
            $"/clubs/{club.Id}"), ownerId);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.ClubMember(membership);
    }

    public async Task RemoveMemberAsync(
        Guid actorId,
        Guid clubId,
        Guid memberUserId,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        var actorRole = EnsureManager(clubId, actorId);
        var membership = FindMembership(clubId, memberUserId);
        if (membership.Role == ClubMemberRole.OWNER || memberUserId == club.OwnerId)
        {
            throw ServiceErrors.Conflict(
                "OWNER_CANNOT_BE_REMOVED",
                "Không thể loại chủ câu lạc bộ.");
        }

        if (actorRole == ClubMemberRole.MODERATOR && membership.Role != ClubMemberRole.MEMBER)
        {
            throw ServiceErrors.Forbidden(
                "MODERATOR_CANNOT_REMOVE_STAFF",
                "Điều hành viên chỉ có thể loại thành viên thường.");
        }

        LeaveActiveSprintParticipations(clubId, memberUserId);
        membership.SoftDelete();
        NotificationDelivery.AddIfEnabled(db, new Notification(
            memberUserId,
            NotificationType.CLUB,
            "Đã rời câu lạc bộ",
            $"Bạn đã được đưa ra khỏi {club.Name}.",
            "/clubs"), actorId);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClubInvitationDto> InviteAsync(
        Guid actorId,
        Guid clubId,
        InviteClubMemberRequest request,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureManager(clubId, actorId);
        var normalizedEmail = NormalizeEmail(request.Email);
        var invitedUser = db.Users.FirstOrDefault(x => x.Email == normalizedEmail)
                          ?? throw ServiceErrors.NotFound(
                              "INVITED_USER_NOT_FOUND",
                              "Không tìm thấy tài khoản BookSpace với email này.");

        if (db.BookClubMembers.Any(x => x.ClubId == clubId && x.UserId == invitedUser.Id))
        {
            throw ServiceErrors.Conflict(
                "ALREADY_CLUB_MEMBER",
                "Người dùng này đã là thành viên câu lạc bộ.");
        }

        var now = timeProvider.GetUtcNow();
        var pending = db.ClubInvitations.FirstOrDefault(x =>
            x.ClubId == clubId &&
            x.InvitedUserId == invitedUser.Id &&
            x.Status == ClubInvitationStatus.PENDING);
        if (pending is not null && !pending.ExpireIfNeeded(now))
        {
            return _mapper.ClubInvitation(pending, actorId);
        }

        if (pending is not null)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var invitation = new ClubInvitation(
            clubId,
            actorId,
            invitedUser.Id,
            now,
            now.Add(InvitationLifetime));
        db.Add(invitation);
        UserSafetyPolicy.EnsureCanInteract(db, actorId, invitedUser.Id);
        NotificationDelivery.AddIfEnabled(db, new Notification(
            invitedUser.Id,
            NotificationType.CLUB,
            "Lời mời tham gia câu lạc bộ",
            $"{GetUserName(actorId)} đã mời bạn tham gia {club.Name}.",
            "/clubs/invitations"), actorId);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.ClubInvitation(invitation, actorId);
    }

    public async Task<PageResult<ClubInvitationDto>> GetClubInvitationsAsync(
        Guid actorId,
        Guid clubId,
        ClubInvitationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        FindClub(clubId);
        EnsureManager(clubId, actorId);
        await ExpireInvitationsAsync(
            db.ClubInvitations.Where(x =>
                x.ClubId == clubId &&
                x.Status == ClubInvitationStatus.PENDING),
            cancellationToken);

        var query = db.ClubInvitations.Where(x => x.ClubId == clubId);
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return PageInvitations(query, actorId, page, pageSize);
    }

    public async Task<PageResult<ClubInvitationDto>> GetMyInvitationsAsync(
        Guid userId,
        ClubInvitationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureUserExists(userId);
        await ExpireInvitationsAsync(
            db.ClubInvitations.Where(x =>
                x.InvitedUserId == userId &&
                x.Status == ClubInvitationStatus.PENDING),
            cancellationToken);

        var query = db.ClubInvitations.Where(x => x.InvitedUserId == userId);
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return PageInvitations(query, userId, page, pageSize);
    }

    public async Task<ClubMemberDto> AcceptInvitationAsync(
        Guid userId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var invitation = FindInvitation(invitationId);
        EnsureInvitationRecipient(invitation, userId);
        var club = FindClub(invitation.ClubId);

        if (invitation.Status == ClubInvitationStatus.ACCEPTED)
        {
            return db.BookClubMembers
                       .Where(x => x.ClubId == invitation.ClubId && x.UserId == userId)
                       .ToList()
                       .Select(_mapper.ClubMember)
                       .FirstOrDefault()
                   ?? throw ServiceErrors.Conflict(
                       "ACCEPTED_INVITATION_WITHOUT_MEMBERSHIP",
                       "Lời mời đã được chấp nhận nhưng không tìm thấy tư cách thành viên.");
        }

        var now = timeProvider.GetUtcNow();
        if (invitation.ExpireIfNeeded(now))
        {
            await db.SaveChangesAsync(cancellationToken);
            throw ServiceErrors.Conflict(
                "CLUB_INVITATION_EXPIRED",
                "Lời mời tham gia câu lạc bộ đã hết hạn.");
        }

        if (invitation.Status != ClubInvitationStatus.PENDING)
        {
            throw ServiceErrors.Conflict(
                "CLUB_INVITATION_NOT_PENDING",
                "Lời mời không còn ở trạng thái chờ xử lý.");
        }

        var membership = db.BookClubMembers.FirstOrDefault(x =>
            x.ClubId == invitation.ClubId &&
            x.UserId == userId);
        invitation.Accept(now);
        if (membership is null)
        {
            membership = new BookClubMember(invitation.ClubId, userId, ClubMemberRole.MEMBER);
            db.Add(membership);
        }

        if (invitation.InviterId != userId)
        {
            NotificationDelivery.AddIfEnabled(db, new Notification(
                invitation.InviterId,
                NotificationType.CLUB,
                "Lời mời đã được chấp nhận",
                $"{GetUserName(userId)} đã tham gia {club.Name}.",
                $"/clubs/{club.Id}"), userId);
        }

        await db.SaveChangesAsync(cancellationToken);
        return _mapper.ClubMember(membership);
    }

    public async Task<ClubInvitationDto> DeclineInvitationAsync(
        Guid userId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var invitation = FindInvitation(invitationId);
        EnsureInvitationRecipient(invitation, userId);
        if (invitation.Status == ClubInvitationStatus.DECLINED)
        {
            return _mapper.ClubInvitation(invitation, userId);
        }

        var now = timeProvider.GetUtcNow();
        if (invitation.ExpireIfNeeded(now))
        {
            await db.SaveChangesAsync(cancellationToken);
            throw ServiceErrors.Conflict(
                "CLUB_INVITATION_EXPIRED",
                "Lời mời tham gia câu lạc bộ đã hết hạn.");
        }

        if (invitation.Status != ClubInvitationStatus.PENDING)
        {
            throw ServiceErrors.Conflict(
                "CLUB_INVITATION_NOT_PENDING",
                "Lời mời không còn ở trạng thái chờ xử lý.");
        }

        invitation.Decline(now);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.ClubInvitation(invitation, userId);
    }

    public async Task<ClubInvitationDto> RevokeInvitationAsync(
        Guid actorId,
        Guid clubId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        FindClub(clubId);
        EnsureManager(clubId, actorId);
        var invitation = db.ClubInvitations.FirstOrDefault(x =>
                             x.Id == invitationId &&
                             x.ClubId == clubId)
                         ?? throw ServiceErrors.NotFound(
                             "CLUB_INVITATION_NOT_FOUND",
                             "Không tìm thấy lời mời tham gia câu lạc bộ.");

        if (invitation.Status == ClubInvitationStatus.REVOKED)
        {
            return _mapper.ClubInvitation(invitation, actorId);
        }

        var now = timeProvider.GetUtcNow();
        if (invitation.ExpireIfNeeded(now))
        {
            await db.SaveChangesAsync(cancellationToken);
            return _mapper.ClubInvitation(invitation, actorId);
        }

        if (invitation.Status != ClubInvitationStatus.PENDING)
        {
            throw ServiceErrors.Conflict(
                "CLUB_INVITATION_NOT_PENDING",
                "Chỉ có thể thu hồi lời mời đang chờ xử lý.");
        }

        invitation.Revoke(now);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.ClubInvitation(invitation, actorId);
    }

    public async Task<ClubSummary> SetCurrentBookAsync(
        Guid actorId,
        Guid clubId,
        SetClubCurrentBookRequest request,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureManager(clubId, actorId);
        if (request.BookId == Guid.Empty)
        {
            throw ServiceErrors.BadRequest("INVALID_BOOK_ID", "Mã sách không hợp lệ.");
        }

        var book = db.Books.FirstOrDefault(x => x.Id == request.BookId)
                   ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
        if (club.CurrentBookId == book.Id)
        {
            return MapDetail(club, actorId);
        }

        club.SetCurrentBook(book.Id);
        AddMemberNotifications(
            clubId,
            actorId,
            "Sách đọc chung mới",
            $"{club.Name} đã chọn “{book.Title}” làm sách đọc chung.",
            $"/clubs/{club.Id}");
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(club, actorId);
    }

    public async Task<ClubSummary> ClearCurrentBookAsync(
        Guid actorId,
        Guid clubId,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureManager(clubId, actorId);
        if (!club.CurrentBookId.HasValue)
        {
            return MapDetail(club, actorId);
        }

        club.SetCurrentBook(null);
        AddMemberNotifications(
            clubId,
            actorId,
            "Đã cập nhật sách đọc chung",
            $"{club.Name} đã kết thúc đợt đọc chung hiện tại.",
            $"/clubs/{club.Id}");
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(club, actorId);
    }

    public PageResult<ClubPostDto> GetPosts(Guid clubId, Guid? viewerId, int page, int pageSize)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, viewerId);
        var query = db.ClubPosts.Where(x => x.ClubId == clubId);
        if (viewerId.HasValue)
        {
            var hiddenUserIds = UserSafetyPolicy.HiddenUserIds(db, viewerId.Value);
            query = query.Where(x => !hiddenUserIds.Contains(x.AuthorId));
        }
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(_mapper.ClubPost)
            .ToList();
        return PageResult<ClubPostDto>.Create(items, normalizedPage, size, total);
    }

    public async Task<ClubPostDto> AddPostAsync(
        Guid userId,
        Guid clubId,
        CreateClubPostRequest request,
        CancellationToken cancellationToken)
    {
        EnsureMember(clubId, userId);
        var generatedTitle = request.Content.Length <= 80 ? request.Content : request.Content[..80];
        var post = new ClubPost(clubId, userId, generatedTitle, request.Content);
        db.Add(post);
        await db.SaveChangesAsync(cancellationToken);
        return _mapper.ClubPost(post);
    }

    public async Task DeletePostAsync(
        Guid userId,
        bool isAdmin,
        Guid postId,
        CancellationToken cancellationToken)
    {
        var post = FindPost(postId);
        var canModerate = db.BookClubMembers.Any(x =>
            x.ClubId == post.ClubId &&
            x.UserId == userId &&
            (x.Role == ClubMemberRole.OWNER || x.Role == ClubMemberRole.MODERATOR));
        if (post.AuthorId != userId && !canModerate && !isAdmin)
        {
            throw ServiceErrors.Forbidden("FORBIDDEN", "Bạn không có quyền xóa bài viết này.");
        }

        post.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }

    public PageResult<ClubPostCommentDto> GetPostComments(
        Guid postId,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        var post = FindPost(postId);
        EnsureCanView(FindClub(post.ClubId), viewerId);
        var query = db.ClubPostComments.Where(x => x.PostId == postId);
        if (viewerId.HasValue)
        {
            var hiddenUserIds = UserSafetyPolicy.HiddenUserIds(db, viewerId.Value);
            query = query.Where(x => !hiddenUserIds.Contains(x.AuthorId));
        }
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderBy(x => x.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(_mapper.ClubPostComment)
            .ToList();
        return PageResult<ClubPostCommentDto>.Create(items, normalizedPage, size, total);
    }

    public async Task<ClubPostCommentDto> AddPostCommentAsync(
        Guid userId,
        Guid postId,
        CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var post = FindPost(postId);
        EnsureMember(post.ClubId, userId);
        UserSafetyPolicy.EnsureCanInteract(db, userId, post.AuthorId);
        var comment = new ClubPostComment(postId, userId, request.Content);
        db.Add(comment);
        if (post.AuthorId != userId)
        {
            NotificationDelivery.AddIfEnabled(db, new Notification(
                post.AuthorId,
                NotificationType.CLUB,
                "Bình luận mới trong câu lạc bộ",
                $"{GetUserName(userId)} đã bình luận bài viết của bạn.",
                $"/clubs/{post.ClubId}"), userId);
        }

        await db.SaveChangesAsync(cancellationToken);
        return _mapper.ClubPostComment(comment);
    }

    public async Task DeletePostCommentAsync(
        Guid userId,
        bool isAdmin,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var comment = db.ClubPostComments.FirstOrDefault(x => x.Id == commentId)
                      ?? throw ServiceErrors.NotFound("COMMENT_NOT_FOUND", "Không tìm thấy bình luận.");
        var post = FindPost(comment.PostId);
        var canModerate = db.BookClubMembers.Any(x =>
            x.ClubId == post.ClubId &&
            x.UserId == userId &&
            (x.Role == ClubMemberRole.OWNER || x.Role == ClubMemberRole.MODERATOR));
        if (comment.AuthorId != userId && !canModerate && !isAdmin)
        {
            throw ServiceErrors.Forbidden("FORBIDDEN", "Bạn không có quyền xóa bình luận này.");
        }

        comment.SoftDelete();
        await db.SaveChangesAsync(cancellationToken);
    }

    private BookClub FindClub(Guid id) =>
        db.BookClubs.FirstOrDefault(x => x.Id == id)
        ?? throw ServiceErrors.NotFound("CLUB_NOT_FOUND", "Không tìm thấy câu lạc bộ.");

    private ClubPost FindPost(Guid id) =>
        db.ClubPosts.FirstOrDefault(x => x.Id == id)
        ?? throw ServiceErrors.NotFound("CLUB_POST_NOT_FOUND", "Không tìm thấy bài viết.");

    private ClubInvitation FindInvitation(Guid id) =>
        db.ClubInvitations.FirstOrDefault(x => x.Id == id)
        ?? throw ServiceErrors.NotFound(
            "CLUB_INVITATION_NOT_FOUND",
            "Không tìm thấy lời mời tham gia câu lạc bộ.");

    private BookClubMember FindMembership(Guid clubId, Guid userId) =>
        db.BookClubMembers.FirstOrDefault(x => x.ClubId == clubId && x.UserId == userId)
        ?? throw ServiceErrors.NotFound(
            "CLUB_MEMBERSHIP_NOT_FOUND",
            "Không tìm thấy thành viên trong câu lạc bộ.");

    private void EnsureMember(Guid clubId, Guid userId)
    {
        FindClub(clubId);
        if (!db.BookClubMembers.Any(x => x.ClubId == clubId && x.UserId == userId))
        {
            throw ServiceErrors.Forbidden(
                "CLUB_MEMBERSHIP_REQUIRED",
                "Bạn cần tham gia câu lạc bộ trước.");
        }
    }

    private ClubMemberRole EnsureManager(Guid clubId, Guid userId)
    {
        var role = db.BookClubMembers
            .Where(x => x.ClubId == clubId && x.UserId == userId)
            .Select(x => (ClubMemberRole?)x.Role)
            .FirstOrDefault();
        if (role is not ClubMemberRole.OWNER and not ClubMemberRole.MODERATOR)
        {
            throw ServiceErrors.Forbidden(
                "CLUB_MANAGEMENT_FORBIDDEN",
                "Bạn không có quyền quản lý câu lạc bộ này.");
        }

        return role.Value;
    }

    private static void EnsureOwner(BookClub club, Guid userId)
    {
        if (club.OwnerId != userId)
        {
            throw ServiceErrors.Forbidden(
                "CLUB_OWNER_REQUIRED",
                "Chỉ chủ câu lạc bộ mới có thể thực hiện thao tác này.");
        }
    }

    private static void EnsureInvitationRecipient(ClubInvitation invitation, Guid userId)
    {
        if (invitation.InvitedUserId != userId)
        {
            throw ServiceErrors.Forbidden(
                "CLUB_INVITATION_FORBIDDEN",
                "Bạn không có quyền xử lý lời mời này.");
        }
    }

    private void EnsureUserExists(Guid userId)
    {
        if (!db.Users.Any(x => x.Id == userId))
        {
            throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        }
    }

    private void EnsureCanView(BookClub club, Guid? viewerId)
    {
        if (club.Visibility == ClubVisibility.PRIVATE &&
            (!viewerId.HasValue ||
             !db.BookClubMembers.Any(x => x.ClubId == club.Id && x.UserId == viewerId.Value)))
        {
            throw ServiceErrors.NotFound("CLUB_NOT_FOUND", "Không tìm thấy câu lạc bộ.");
        }
    }

    private async Task ExpireInvitationsAsync(
        IQueryable<ClubInvitation> query,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var changed = false;
        foreach (var invitation in query.ToList())
        {
            changed |= invitation.ExpireIfNeeded(now);
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private PageResult<ClubInvitationDto> PageInvitations(
        IQueryable<ClubInvitation> query,
        Guid viewerId,
        int page,
        int pageSize)
    {
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(x => _mapper.ClubInvitation(x, viewerId))
            .ToList();
        return PageResult<ClubInvitationDto>.Create(items, normalizedPage, size, total);
    }

    private void AddMemberNotifications(
        Guid clubId,
        Guid actorId,
        string title,
        string message,
        string link)
    {
        var recipientIds = db.BookClubMembers
            .Where(x => x.ClubId == clubId && x.UserId != actorId)
            .Select(x => x.UserId)
            .Distinct()
            .ToList();
        NotificationDelivery.AddRangeIfEnabled(
            db,
            recipientIds.Select(userId =>
                new Notification(userId, NotificationType.CLUB, title, message, link)),
            actorId);
    }

    private void LeaveActiveSprintParticipations(Guid clubId, Guid userId)
    {
        var now = timeProvider.GetUtcNow();
        var mutableSprints = db.ClubReadingSprints
            .Where(x => x.ClubId == clubId)
            .ToList()
            .Where(x => x.GetStatus(now) is not ReadingSprintStatus.COMPLETED and not ReadingSprintStatus.CANCELLED)
            .ToDictionary(x => x.Id);
        if (mutableSprints.Count == 0)
        {
            return;
        }

        var touchedSprintIds = new HashSet<Guid>();
        foreach (var participant in db.ClubReadingSprintParticipants
                     .Where(x =>
                         x.UserId == userId &&
                         x.LeftAt == null)
                     .ToList()
                     .Where(x => mutableSprints.ContainsKey(x.SprintId)))
        {
            if (participant.Leave(now) &&
                touchedSprintIds.Add(participant.SprintId))
            {
                mutableSprints[participant.SprintId].RecordActivity(now);
            }
        }
    }

    private string GetUserName(Guid userId) =>
        db.Users
            .Where(x => x.Id == userId)
            .Select(x => x.DisplayName)
            .FirstOrDefault()
        ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");

    private static string NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw ServiceErrors.BadRequest(
                "INVALID_EMAIL",
                "Email người được mời không được để trống.");
        }

        if (normalized.Length > 254 || !normalized.Contains('@'))
        {
            throw ServiceErrors.BadRequest(
                "INVALID_EMAIL",
                "Email người được mời không hợp lệ.");
        }

        return normalized;
    }

    private ClubSummary MapDetail(BookClub club, Guid? viewerId)
    {
        var posts = db.ClubPosts
            .Where(x => x.ClubId == club.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToList()
            .Select(_mapper.ClubPost)
            .ToList();
        return _mapper.Club(club, viewerId) with { Posts = posts };
    }
}
