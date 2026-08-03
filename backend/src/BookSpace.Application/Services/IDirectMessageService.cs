using BookSpace.Application.Contracts;

namespace BookSpace.Application.Services;

public interface IDirectMessageService
{
    ConversationPageDto GetConversations(
        Guid userId,
        string? cursor,
        int pageSize);

    ConversationDto GetConversation(Guid userId, Guid conversationId);

    Task<ConversationDto> StartConversationAsync(
        Guid userId,
        StartConversationRequest request,
        CancellationToken cancellationToken);

    DirectMessagePageDto GetMessages(
        Guid userId,
        Guid conversationId,
        string? cursor,
        int pageSize);

    Task<DirectMessageDto> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        SendDirectMessageRequest request,
        CancellationToken cancellationToken);

    DirectMessageUnreadCountDto GetUnreadCount(Guid userId);

    Task<DirectMessageReadStateDto> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        MarkDirectMessageReadRequest request,
        CancellationToken cancellationToken);
}
