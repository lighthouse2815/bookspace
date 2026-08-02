using BookSpace.Domain.Common;

namespace BookSpace.Domain.Entities;

public sealed class ClubChatMessage : Entity
{
    private ClubChatMessage() { }

    public ClubChatMessage(
        Guid clubId,
        Guid senderId,
        string content,
        DateTimeOffset createdAt)
    {
        ClubId = clubId;
        SenderId = senderId;
        Content = Guard.Required(content, "Nội dung tin nhắn", 2000);
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid ClubId { get; private set; }
    public BookClub Club { get; private set; } = null!;
    public Guid SenderId { get; private set; }
    public User Sender { get; private set; } = null!;
    public string Content { get; private set; } = string.Empty;
}

public sealed class ClubChatReadState : Entity
{
    private ClubChatReadState() { }

    public ClubChatReadState(Guid membershipId)
    {
        MembershipId = membershipId;
    }

    public Guid MembershipId { get; private set; }
    public BookClubMember Membership { get; private set; } = null!;
    public Guid? LastReadMessageId { get; private set; }
    public DateTimeOffset? LastReadAt { get; private set; }

    public bool Advance(Guid messageId, DateTimeOffset messageCreatedAt)
    {
        var utcCreatedAt = messageCreatedAt.ToUniversalTime();
        if (LastReadAt.HasValue &&
            (LastReadAt.Value > utcCreatedAt ||
             (LastReadAt.Value == utcCreatedAt &&
              LastReadMessageId.HasValue &&
              LastReadMessageId.Value.CompareTo(messageId) >= 0)))
        {
            return false;
        }

        LastReadMessageId = messageId;
        LastReadAt = utcCreatedAt;
        Touch();
        return true;
    }
}
