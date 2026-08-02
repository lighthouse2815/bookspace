using BookSpace.Application.Abstractions;

namespace BookSpace.Application.Services;

internal static class UserSafetyPolicy
{
    public static bool IsBlockedBetween(
        IBookSpaceDbContext db,
        Guid firstUserId,
        Guid secondUserId) =>
        firstUserId != secondUserId && db.UserBlocks.Any(x =>
            x.BlockerId == firstUserId && x.BlockedUserId == secondUserId ||
            x.BlockerId == secondUserId && x.BlockedUserId == firstUserId);

    public static bool IsMutedBy(
        IBookSpaceDbContext db,
        Guid viewerId,
        Guid actorId) =>
        viewerId != actorId && db.UserMutes.Any(x =>
            x.UserId == viewerId && x.MutedUserId == actorId);

    public static bool IsHiddenFrom(
        IBookSpaceDbContext db,
        Guid viewerId,
        Guid actorId) =>
        viewerId != actorId &&
        (IsBlockedBetween(db, viewerId, actorId) || IsMutedBy(db, viewerId, actorId));

    public static IQueryable<Guid> BlockedUserIds(
        IBookSpaceDbContext db,
        Guid viewerId) =>
        db.UserBlocks
            .Where(x => x.BlockerId == viewerId || x.BlockedUserId == viewerId)
            .Select(x => x.BlockerId == viewerId ? x.BlockedUserId : x.BlockerId)
            .Distinct();

    public static IQueryable<Guid> HiddenUserIds(
        IBookSpaceDbContext db,
        Guid viewerId) =>
        BlockedUserIds(db, viewerId)
            .Concat(db.UserMutes
                .Where(x => x.UserId == viewerId)
                .Select(x => x.MutedUserId))
            .Distinct();

    public static void EnsureCanView(
        IBookSpaceDbContext db,
        Guid? viewerId,
        Guid targetUserId)
    {
        if (viewerId.HasValue &&
            IsBlockedBetween(db, viewerId.Value, targetUserId))
        {
            throw ServiceErrors.NotFound(
                "USER_NOT_FOUND",
                "Không tìm thấy người dùng.");
        }
    }

    public static void EnsureCanInteract(
        IBookSpaceDbContext db,
        Guid actorId,
        Guid targetUserId)
    {
        if (IsBlockedBetween(db, actorId, targetUserId))
        {
            throw ServiceErrors.Forbidden(
                "USER_RELATION_BLOCKED",
                "Không thể tương tác vì một trong hai tài khoản đã chặn tài khoản còn lại.");
        }
    }
}
