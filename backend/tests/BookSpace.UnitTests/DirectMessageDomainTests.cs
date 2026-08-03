using BookSpace.Domain.Entities;
using BookSpace.Domain.Exceptions;

namespace BookSpace.UnitTests;

public sealed class DirectMessageDomainTests
{
    [Fact]
    public void Conversation_normalizes_participants_and_tracks_activity()
    {
        var first = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var createdAt = new DateTimeOffset(2026, 8, 4, 8, 0, 0, TimeSpan.Zero);
        var conversation = new Conversation(first, second, createdAt);

        Assert.Equal(second, conversation.UserOneId);
        Assert.Equal(first, conversation.UserTwoId);
        Assert.True(conversation.Contains(first));
        Assert.Equal(second, conversation.OtherParticipantId(first));
        Assert.Equal(createdAt, conversation.LastActivityAt);

        conversation.MarkActivity(createdAt.AddMinutes(2));
        Assert.Equal(createdAt.AddMinutes(2), conversation.LastActivityAt);
        conversation.MarkActivity(createdAt.AddMinutes(1));
        Assert.Equal(createdAt.AddMinutes(2), conversation.LastActivityAt);
    }

    [Fact]
    public void Conversation_rejects_invalid_participants_and_non_participant_access()
    {
        var userId = Guid.NewGuid();
        var invalid = Assert.Throws<DomainException>(() =>
            new Conversation(userId, userId, DateTimeOffset.UtcNow));
        Assert.Equal("INVALID_CONVERSATION_PARTICIPANTS", invalid.Code);

        var conversation = new Conversation(userId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var denied = Assert.Throws<DomainException>(() =>
            conversation.OtherParticipantId(Guid.NewGuid()));
        Assert.Equal("CONVERSATION_ACCESS_DENIED", denied.Code);
    }

    [Fact]
    public void Direct_message_validates_content_and_read_state_only_advances()
    {
        var conversationId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var firstAt = new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero);
        var message = new DirectMessage(conversationId, senderId, "  Xin chào  ", firstAt);
        Assert.Equal("Xin chào", message.Content);

        var blank = Assert.Throws<DomainException>(() =>
            new DirectMessage(conversationId, senderId, "   ", firstAt));
        Assert.Equal("VALIDATION_ERROR", blank.Code);

        var state = new DirectMessageReadState(conversationId, senderId);
        var firstMessageId = Guid.NewGuid();
        var secondMessageId = Guid.NewGuid();
        Assert.True(state.Advance(firstMessageId, firstAt));
        Assert.True(state.Advance(secondMessageId, firstAt.AddMinutes(1)));
        Assert.False(state.Advance(firstMessageId, firstAt));
        Assert.Equal(secondMessageId, state.LastReadMessageId);
        Assert.Equal(firstAt.AddMinutes(1), state.LastReadAt);
    }
}
