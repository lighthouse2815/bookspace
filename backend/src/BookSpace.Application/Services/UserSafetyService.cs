using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;

namespace BookSpace.Application.Services;

public sealed class UserSafetyService(IBookSpaceDbContext db) : IUserSafetyService
{
    public PageResult<UserSafetyEntryDto> GetMine(Guid userId, int page, int pageSize)
    {
        _ = FindUser(userId);
        var blocks = db.UserBlocks
            .Where(x => x.BlockerId == userId)
            .ToList();
        var mutes = db.UserMutes
            .Where(x => x.UserId == userId)
            .ToList();
        var targetIds = blocks.Select(x => x.BlockedUserId)
            .Concat(mutes.Select(x => x.MutedUserId))
            .Distinct()
            .ToList();
        var users = db.Users
            .Where(x => targetIds.Contains(x.Id))
            .ToList()
            .ToDictionary(x => x.Id);
        var blockByTarget = blocks.ToDictionary(x => x.BlockedUserId);
        var muteByTarget = mutes.ToDictionary(x => x.MutedUserId);
        var ordered = targetIds
            .Where(users.ContainsKey)
            .OrderByDescending(targetId => LatestAt(
                blockByTarget.GetValueOrDefault(targetId)?.CreatedAt,
                muteByTarget.GetValueOrDefault(targetId)?.CreatedAt))
            .ThenBy(targetId => users[targetId].DisplayName)
            .ThenBy(targetId => targetId)
            .ToList();
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var items = ordered
            .Skip(skip)
            .Take(size)
            .Select(targetId => Map(
                users[targetId],
                blockByTarget.GetValueOrDefault(targetId),
                muteByTarget.GetValueOrDefault(targetId)))
            .ToList();
        return PageResult<UserSafetyEntryDto>.Create(
            items,
            normalizedPage,
            size,
            ordered.Count);
    }

    public async Task<UserSafetyEntryDto> BlockAsync(
        Guid userId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        EnsureDifferentUsers(userId, targetUserId, "CANNOT_BLOCK_SELF", "Bạn không thể tự chặn chính mình.");
        _ = FindUser(userId);
        var target = FindTarget(targetUserId);
        var existing = db.UserBlocks.FirstOrDefault(x =>
            x.BlockerId == userId && x.BlockedUserId == targetUserId);
        if (existing is not null)
        {
            return Map(target, existing, null);
        }

        var block = new UserBlock(userId, targetUserId);
        db.Add(block);
        var follows = db.Follows.Where(x =>
            x.FollowerId == userId && x.FollowingId == targetUserId ||
            x.FollowerId == targetUserId && x.FollowingId == userId)
            .ToList();
        db.RemoveRange(follows);
        var mute = db.UserMutes.FirstOrDefault(x =>
            x.UserId == userId && x.MutedUserId == targetUserId);
        if (mute is not null)
        {
            db.Remove(mute);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Map(target, block, null);
    }

    public async Task UnblockAsync(
        Guid userId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var block = db.UserBlocks.FirstOrDefault(x =>
            x.BlockerId == userId && x.BlockedUserId == targetUserId);
        if (block is null)
        {
            return;
        }

        db.Remove(block);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserSafetyEntryDto> MuteAsync(
        Guid userId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        EnsureDifferentUsers(userId, targetUserId, "CANNOT_MUTE_SELF", "Bạn không thể tự ẩn chính mình.");
        _ = FindUser(userId);
        var target = FindTarget(targetUserId);
        if (UserSafetyPolicy.IsBlockedBetween(db, userId, targetUserId))
        {
            throw ServiceErrors.Conflict(
                "USER_RELATION_BLOCKED",
                "Hãy bỏ chặn tài khoản trước khi thay đổi trạng thái ẩn nội dung.");
        }

        var existing = db.UserMutes.FirstOrDefault(x =>
            x.UserId == userId && x.MutedUserId == targetUserId);
        if (existing is not null)
        {
            return Map(target, null, existing);
        }

        var mute = new UserMute(userId, targetUserId);
        db.Add(mute);
        await db.SaveChangesAsync(cancellationToken);
        return Map(target, null, mute);
    }

    public async Task UnmuteAsync(
        Guid userId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var mute = db.UserMutes.FirstOrDefault(x =>
            x.UserId == userId && x.MutedUserId == targetUserId);
        if (mute is null)
        {
            return;
        }

        db.Remove(mute);
        await db.SaveChangesAsync(cancellationToken);
    }

    private User FindUser(Guid userId) =>
        db.Users.FirstOrDefault(x => x.Id == userId && !x.IsLocked)
        ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");

    private User FindTarget(Guid targetUserId) =>
        db.Users.FirstOrDefault(x => x.Id == targetUserId && !x.IsLocked)
        ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");

    private static void EnsureDifferentUsers(
        Guid userId,
        Guid targetUserId,
        string code,
        string message)
    {
        if (userId == targetUserId)
        {
            throw ServiceErrors.BadRequest(code, message);
        }
    }

    private static DateTimeOffset LatestAt(
        DateTimeOffset? blockedAt,
        DateTimeOffset? mutedAt) =>
        blockedAt.HasValue && blockedAt >= mutedAt
            ? blockedAt.Value
            : mutedAt ?? DateTimeOffset.MinValue;

    private static UserSafetyEntryDto Map(
        User user,
        UserBlock? block,
        UserMute? mute) =>
        new(
            new UserSummary(
                user.Id,
                null,
                user.DisplayName,
                user.AvatarUrl,
                user.Role),
            block is not null,
            mute is not null,
            block?.CreatedAt,
            mute?.CreatedAt);
}
