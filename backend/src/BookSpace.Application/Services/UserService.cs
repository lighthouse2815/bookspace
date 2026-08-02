using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class UserService(
    IBookSpaceDbContext db,
    IAsyncQueryExecutor queryExecutor,
    IUserDiscoveryQuery discoveryQuery,
    IFollowMutationBoundary followMutationBoundary) : IUserService
{
    public UserProfile Get(Guid userId, Guid? viewerId)
    {
        UserSafetyPolicy.EnsureCanView(db, viewerId, userId);
        var user = db.Users.FirstOrDefault(x => x.Id == userId && !x.IsLocked)
                   ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        return Map(user, viewerId);
    }

    public async Task<PageResult<UserDiscoveryItem>> SearchAsync(
        string? search,
        Guid? viewerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrEmpty(normalizedSearch) &&
            normalizedSearch.Length is < 2 or > 100)
        {
            throw new UseCaseException(
                "INVALID_USER_SEARCH",
                "Từ khóa tìm kiếm độc giả phải có từ 2 đến 100 ký tự.");
        }

        var users = db.Users.Where(user => !user.IsLocked);
        if (viewerId.HasValue)
        {
            var blockedUserIds = UserSafetyPolicy.BlockedUserIds(db, viewerId.Value);
            users = users.Where(user =>
                user.Id != viewerId.Value &&
                !blockedUserIds.Contains(user.Id));
        }

        if (!string.IsNullOrEmpty(normalizedSearch))
        {
            users = discoveryQuery.ApplyDisplayNameSearch(users, normalizedSearch);
        }

        var total = await queryExecutor.CountAsync(users, cancellationToken);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var candidates = await queryExecutor.ToListAsync(
            ProjectDiscovery(
                    users
                        .OrderBy(user => user.DisplayName)
                        .ThenBy(user => user.Id),
                    viewerId)
                .Skip(skip)
                .Take(size),
            cancellationToken);
        var reason = string.IsNullOrEmpty(normalizedSearch)
            ? ("DIRECTORY", "Độc giả đang hoạt động trên BookSpace.")
            : ("SEARCH_MATCH", "Phù hợp với tên hiển thị bạn đang tìm.");
        var items = candidates
            .Select(candidate => ToDiscoveryItem(candidate, reason.Item1, reason.Item2))
            .ToList();

        return PageResult<UserDiscoveryItem>.Create(items, normalizedPage, size, total);
    }

    public async Task<PageResult<UserDiscoveryItem>> GetSuggestionsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await queryExecutor.AnyAsync(
                db.Users.Where(user => user.Id == userId),
                cancellationToken))
        {
            throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        }

        var followedUserIds = db.Follows
            .Where(follow => follow.FollowerId == userId)
            .Select(follow => follow.FollowingId);
        var hiddenUserIds = UserSafetyPolicy.HiddenUserIds(db, userId);
        var users = db.Users.Where(user =>
            !user.IsLocked &&
            user.Id != userId &&
            !hiddenUserIds.Contains(user.Id) &&
            !followedUserIds.Contains(user.Id));

        var total = await queryExecutor.CountAsync(users, cancellationToken);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var orderedUsers = users
            .OrderByDescending(user => db.Follows.Count(follow =>
                follow.FollowingId == user.Id &&
                !follow.Follower.IsLocked &&
                followedUserIds.Contains(follow.FollowerId)))
            .ThenByDescending(user => db.Follows.Count(follow =>
                follow.FollowingId == user.Id))
            .ThenByDescending(user => db.LibraryItems.Count(item =>
                item.UserId == user.Id &&
                item.Status == LibraryStatus.READ))
            .ThenBy(user => user.DisplayName)
            .ThenBy(user => user.Id);
        var candidates = await queryExecutor.ToListAsync(
            ProjectDiscovery(orderedUsers, userId)
                .Skip(skip)
                .Take(size),
            cancellationToken);
        var items = candidates.Select(ToSuggestedDiscoveryItem).ToList();

        return PageResult<UserDiscoveryItem>.Create(items, normalizedPage, size, total);
    }

    public async Task<UserProfile> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = db.Users.FirstOrDefault(x => x.Id == userId)
                   ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        user.UpdateProfile(request.DisplayName, request.Bio, request.AvatarUrl);
        await db.SaveChangesAsync(cancellationToken);
        return Map(user, userId);
    }

    public async Task<UserProfile> UpdatePrivacyAsync(
        Guid userId,
        UpdateProfilePrivacyRequest request,
        CancellationToken cancellationToken)
    {
        var user = db.Users.FirstOrDefault(x => x.Id == userId)
                   ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        user.UpdatePublicReadingVisibility(
            request.IsReadingShelfPublic,
            request.IsReadingActivityPublic);
        await db.SaveChangesAsync(cancellationToken);
        return Map(user, userId);
    }

    public async Task FollowAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var targetUser = db.Users.FirstOrDefault(x => x.Id == targetUserId && !x.IsLocked)
                         ?? throw ServiceErrors.NotFound(
                             "USER_NOT_FOUND",
                             "Không tìm thấy người dùng cần theo dõi.");
        UserSafetyPolicy.EnsureCanInteract(db, userId, targetUserId);
        if (db.Follows.Any(x => x.FollowerId == userId && x.FollowingId == targetUserId))
        {
            throw ServiceErrors.Conflict("ALREADY_FOLLOWING", "Bạn đã theo dõi người dùng này.");
        }

        var actorName = db.Users.Where(x => x.Id == userId).Select(x => x.DisplayName).First();
        var created = await followMutationBoundary.TryCreateAsync(
            new Follow(userId, targetUserId),
            NotificationDelivery.IsEnabled(
                db,
                targetUserId,
                NotificationType.FOLLOW,
                userId)
                ? new Notification(
                    targetUserId,
                    NotificationType.FOLLOW,
                    "Bạn có người theo dõi mới",
                    $"{actorName} vừa theo dõi bạn.",
                    $"/users/{userId}")
                : null,
            cancellationToken);
        if (!created)
        {
            throw ServiceErrors.Conflict("ALREADY_FOLLOWING", "Bạn đã theo dõi người dùng này.");
        }
    }

    public async Task UnfollowAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken)
    {
        EnsureUser(targetUserId);
        var follow = db.Follows.FirstOrDefault(x => x.FollowerId == userId && x.FollowingId == targetUserId);
        if (follow is null)
        {
            return;
        }

        db.Remove(follow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public PageResult<UserSummary> GetFollowers(
        Guid userId,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        UserSafetyPolicy.EnsureCanView(db, viewerId, userId);
        EnsureUser(userId);
        var ids = db.Follows.Where(x => x.FollowingId == userId).Select(x => x.FollowerId).ToList();
        return PageUsers(ids, viewerId, page, pageSize);
    }

    public PageResult<UserSummary> GetFollowing(
        Guid userId,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        UserSafetyPolicy.EnsureCanView(db, viewerId, userId);
        EnsureUser(userId);
        var ids = db.Follows.Where(x => x.FollowerId == userId).Select(x => x.FollowingId).ToList();
        return PageUsers(ids, viewerId, page, pageSize);
    }

    private PageResult<UserSummary> PageUsers(
        IReadOnlyCollection<Guid> ids,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var query = db.Users.Where(x => ids.Contains(x.Id) && !x.IsLocked);
        if (viewerId.HasValue)
        {
            var blockedUserIds = UserSafetyPolicy.BlockedUserIds(db, viewerId.Value);
            query = query.Where(x => !blockedUserIds.Contains(x.Id));
        }

        var total = query.Count();
        var users = query
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(new ServiceMapper(db).User)
            .ToList();
        return PageResult<UserSummary>.Create(users, normalizedPage, size, total);
    }

    private UserProfile Map(User user, Guid? viewerId)
    {
        var isOtherViewer = viewerId.HasValue && viewerId.Value != user.Id;
        var isFollowing = isOtherViewer && db.Follows.Any(x =>
            x.FollowerId == viewerId!.Value && x.FollowingId == user.Id);
        var followsYou = isOtherViewer && db.Follows.Any(x =>
            x.FollowerId == user.Id && x.FollowingId == viewerId!.Value);
        var mutualFollowCount = isOtherViewer
            ? db.Follows.Count(candidateFollow =>
                candidateFollow.FollowingId == user.Id &&
                db.Follows.Any(viewerFollow =>
                    viewerFollow.FollowerId == viewerId!.Value &&
                    viewerFollow.FollowingId == candidateFollow.FollowerId))
            : 0;

        return new UserProfile(
            user.Id,
            viewerId == user.Id ? user.Email : null,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.Role,
            db.Follows.Count(x => x.FollowingId == user.Id),
            db.Follows.Count(x => x.FollowerId == user.Id),
            db.LibraryItems.Count(x => x.UserId == user.Id && x.Status == LibraryStatus.READ),
            isFollowing,
            followsYou,
            mutualFollowCount,
            new ProfilePrivacyDto(
                user.IsReadingShelfPublic,
                user.IsReadingActivityPublic),
            user.CreatedAt,
            isOtherViewer && UserSafetyPolicy.IsMutedBy(db, viewerId!.Value, user.Id));
    }

    private IQueryable<DiscoveryCandidate> ProjectDiscovery(
        IQueryable<User> users,
        Guid? viewerId)
    {
        if (!viewerId.HasValue)
        {
            return users.Select(user => new DiscoveryCandidate(
                user.Id,
                user.DisplayName,
                user.Bio,
                user.AvatarUrl,
                db.Follows.Count(follow => follow.FollowingId == user.Id),
                db.LibraryItems.Count(item =>
                    item.UserId == user.Id &&
                    item.Status == LibraryStatus.READ),
                false,
                false,
                0));
        }

        var viewer = viewerId.Value;
        var followedUserIds = db.Follows
            .Where(follow => follow.FollowerId == viewer)
            .Select(follow => follow.FollowingId);
        return users.Select(user => new DiscoveryCandidate(
            user.Id,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            db.Follows.Count(follow => follow.FollowingId == user.Id),
            db.LibraryItems.Count(item =>
                item.UserId == user.Id &&
                item.Status == LibraryStatus.READ),
            followedUserIds.Contains(user.Id),
            db.Follows.Any(follow =>
                follow.FollowerId == user.Id &&
                follow.FollowingId == viewer),
            db.Follows.Count(follow =>
                follow.FollowingId == user.Id &&
                !follow.Follower.IsLocked &&
                followedUserIds.Contains(follow.FollowerId))));
    }

    private static UserDiscoveryItem ToDiscoveryItem(
        DiscoveryCandidate candidate,
        string reason,
        string reasonText) =>
        new(
            candidate.Id,
            candidate.DisplayName,
            candidate.Bio,
            candidate.AvatarUrl,
            candidate.FollowerCount,
            candidate.BooksReadCount,
            candidate.IsFollowing,
            candidate.FollowsYou,
            candidate.MutualFollowCount,
            reason,
            reasonText);

    private static UserDiscoveryItem ToSuggestedDiscoveryItem(DiscoveryCandidate candidate)
    {
        var (reason, reasonText) = candidate switch
        {
            { MutualFollowCount: > 0 } => (
                "MUTUAL_FOLLOWS",
                $"{candidate.MutualFollowCount} người bạn theo dõi cũng theo dõi độc giả này."),
            { FollowsYou: true } => (
                "FOLLOWS_YOU",
                "Độc giả này đang theo dõi bạn."),
            { FollowerCount: > 0 } => (
                "POPULAR_READER",
                "Được nhiều độc giả BookSpace theo dõi."),
            { BooksReadCount: > 0 } => (
                "ACTIVE_READER",
                "Đang xây dựng hành trình đọc trên BookSpace."),
            _ => (
                "NEW_READER",
                "Một độc giả bạn có thể muốn làm quen.")
        };

        return ToDiscoveryItem(candidate, reason, reasonText);
    }

    private void EnsureUser(Guid userId)
    {
        if (!db.Users.Any(x => x.Id == userId))
        {
            throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        }
    }

    private sealed record DiscoveryCandidate(
        Guid Id,
        string DisplayName,
        string? Bio,
        string? AvatarUrl,
        int FollowerCount,
        int BooksReadCount,
        bool IsFollowing,
        bool FollowsYou,
        int MutualFollowCount);
}
