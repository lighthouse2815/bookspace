using BookSpace.Domain.Entities;
using BookSpace.Domain.Exceptions;

namespace BookSpace.UnitTests;

public sealed class UserSafetyEntityTests
{
    [Fact]
    public void Block_and_mute_reject_self_targeting()
    {
        var userId = Guid.NewGuid();

        var block = Assert.Throws<DomainException>(() => new UserBlock(userId, userId));
        var mute = Assert.Throws<DomainException>(() => new UserMute(userId, userId));

        Assert.Equal("CANNOT_BLOCK_SELF", block.Code);
        Assert.Equal("CANNOT_MUTE_SELF", mute.Code);
    }

    [Fact]
    public void Block_and_mute_keep_directional_relationship_ids()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var block = new UserBlock(ownerId, targetId);
        var mute = new UserMute(ownerId, targetId);

        Assert.Equal(ownerId, block.BlockerId);
        Assert.Equal(targetId, block.BlockedUserId);
        Assert.Equal(ownerId, mute.UserId);
        Assert.Equal(targetId, mute.MutedUserId);
    }
}
