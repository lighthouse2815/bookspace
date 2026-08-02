using BookSpace.Domain.Common;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class UserBlock : Entity
{
    private UserBlock() { }

    public UserBlock(Guid blockerId, Guid blockedUserId)
    {
        if (blockerId == blockedUserId)
        {
            throw new DomainException(
                "CANNOT_BLOCK_SELF",
                "Bạn không thể tự chặn chính mình.");
        }

        BlockerId = blockerId;
        BlockedUserId = blockedUserId;
    }

    public Guid BlockerId { get; private set; }
    public User Blocker { get; private set; } = null!;
    public Guid BlockedUserId { get; private set; }
    public User BlockedUser { get; private set; } = null!;
}

public sealed class UserMute : Entity
{
    private UserMute() { }

    public UserMute(Guid userId, Guid mutedUserId)
    {
        if (userId == mutedUserId)
        {
            throw new DomainException(
                "CANNOT_MUTE_SELF",
                "Bạn không thể tự ẩn chính mình.");
        }

        UserId = userId;
        MutedUserId = mutedUserId;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid MutedUserId { get; private set; }
    public User MutedUser { get; private set; } = null!;
}
