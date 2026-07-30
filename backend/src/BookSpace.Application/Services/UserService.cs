using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class UserService(IBookSpaceDbContext db) : IUserService
{
    public UserProfile Get(Guid userId, Guid? viewerId)
    {
        var user = db.Users.FirstOrDefault(x => x.Id == userId)
                   ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        return Map(user, viewerId);
    }

    public async Task<UserProfile> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = db.Users.FirstOrDefault(x => x.Id == userId)
                   ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        user.UpdateProfile(request.DisplayName, request.Bio, request.AvatarUrl);
        await db.SaveChangesAsync(cancellationToken);
        return Map(user, userId);
    }

    public async Task FollowAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken)
    {
        _ = db.Users.FirstOrDefault(x => x.Id == targetUserId)
            ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng cần theo dõi.");
        if (db.Follows.Any(x => x.FollowerId == userId && x.FollowingId == targetUserId))
        {
            throw ServiceErrors.Conflict("ALREADY_FOLLOWING", "Bạn đã theo dõi người dùng này.");
        }

        db.Add(new Follow(userId, targetUserId));
        var actorName = db.Users.Where(x => x.Id == userId).Select(x => x.DisplayName).First();
        db.Add(new Notification(
            targetUserId,
            NotificationType.FOLLOW,
            "Bạn có người theo dõi mới",
            $"{actorName} vừa theo dõi bạn.",
            $"/users/{userId}"));
        await db.SaveChangesAsync(cancellationToken);
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

    public PageResult<UserSummary> GetFollowers(Guid userId, int page, int pageSize)
    {
        EnsureUser(userId);
        var ids = db.Follows.Where(x => x.FollowingId == userId).Select(x => x.FollowerId).ToList();
        return PageUsers(ids, page, pageSize);
    }

    public PageResult<UserSummary> GetFollowing(Guid userId, int page, int pageSize)
    {
        EnsureUser(userId);
        var ids = db.Follows.Where(x => x.FollowerId == userId).Select(x => x.FollowingId).ToList();
        return PageUsers(ids, page, pageSize);
    }

    private PageResult<UserSummary> PageUsers(IReadOnlyCollection<Guid> ids, int page, int pageSize)
    {
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var users = db.Users
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.DisplayName)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(new ServiceMapper(db).User)
            .ToList();
        return PageResult<UserSummary>.Create(users, normalizedPage, size, ids.Count);
    }

    private UserProfile Map(User user, Guid? viewerId) =>
        new(
            user.Id,
            viewerId == user.Id ? user.Email : null,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.Role,
            db.Follows.Count(x => x.FollowingId == user.Id),
            db.Follows.Count(x => x.FollowerId == user.Id),
            db.LibraryItems.Count(x => x.UserId == user.Id && x.Status == LibraryStatus.READ),
            viewerId.HasValue && db.Follows.Any(x => x.FollowerId == viewerId.Value && x.FollowingId == user.Id),
            user.CreatedAt);

    private void EnsureUser(Guid userId)
    {
        if (!db.Users.Any(x => x.Id == userId))
        {
            throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        }
    }
}
