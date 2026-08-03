using BookSpace.Domain.Common;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class Conversation : Entity
{
    private Conversation() { }

    public Conversation(Guid firstUserId, Guid secondUserId, DateTimeOffset createdAt)
    {
        if (firstUserId == Guid.Empty || secondUserId == Guid.Empty || firstUserId == secondUserId)
        {
            throw new DomainException(
                "INVALID_CONVERSATION_PARTICIPANTS",
                "Người tham gia cuộc trò chuyện không hợp lệ.");
        }

        var firstComesBeforeSecond = firstUserId.CompareTo(secondUserId) < 0;
        UserOneId = firstComesBeforeSecond ? firstUserId : secondUserId;
        UserTwoId = firstComesBeforeSecond ? secondUserId : firstUserId;
        CreatedAt = createdAt.ToUniversalTime();
        LastActivityAt = CreatedAt;
    }

    public Guid UserOneId { get; private set; }
    public User UserOne { get; private set; } = null!;
    public Guid UserTwoId { get; private set; }
    public User UserTwo { get; private set; } = null!;
    public DateTimeOffset LastActivityAt { get; private set; }

    public bool Contains(Guid userId) => UserOneId == userId || UserTwoId == userId;

    public Guid OtherParticipantId(Guid userId)
    {
        if (UserOneId == userId)
        {
            return UserTwoId;
        }

        if (UserTwoId == userId)
        {
            return UserOneId;
        }

        throw new DomainException(
            "CONVERSATION_ACCESS_DENIED",
            "Bạn không thuộc cuộc trò chuyện này.");
    }

    public void MarkActivity(DateTimeOffset activityAt)
    {
        var utcActivityAt = activityAt.ToUniversalTime();
        if (utcActivityAt > LastActivityAt)
        {
            LastActivityAt = utcActivityAt;
        }

        Touch();
    }
}

public sealed class DirectMessage : Entity
{
    private DirectMessage() { }

    public DirectMessage(
        Guid conversationId,
        Guid senderId,
        string content,
        DateTimeOffset createdAt)
    {
        if (conversationId == Guid.Empty || senderId == Guid.Empty)
        {
            throw new DomainException(
                "INVALID_DIRECT_MESSAGE",
                "Tin nhắn riêng không hợp lệ.");
        }

        ConversationId = conversationId;
        SenderId = senderId;
        Content = Guard.Required(content, "Nội dung tin nhắn", 2000);
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid ConversationId { get; private set; }
    public Conversation Conversation { get; private set; } = null!;
    public Guid SenderId { get; private set; }
    public User Sender { get; private set; } = null!;
    public string Content { get; private set; } = string.Empty;
}

public sealed class DirectMessageReadState : Entity
{
    private DirectMessageReadState() { }

    public DirectMessageReadState(Guid conversationId, Guid userId)
    {
        if (conversationId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException(
                "INVALID_DIRECT_MESSAGE_READ_STATE",
                "Trạng thái đọc tin nhắn riêng không hợp lệ.");
        }

        ConversationId = conversationId;
        UserId = userId;
    }

    public Guid ConversationId { get; private set; }
    public Conversation Conversation { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
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
