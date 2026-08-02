using BookSpace.Application.Contracts;

namespace BookSpace.Application.Services;

public interface IClubChatService
{
    ClubChatMessagePageDto GetMessages(
        Guid userId,
        Guid clubId,
        string? cursor,
        int pageSize);

    Task<ClubChatMessageDto> SendMessageAsync(
        Guid userId,
        Guid clubId,
        SendClubChatMessageRequest request,
        CancellationToken cancellationToken);

    ClubChatUnreadDto GetUnreadCount(Guid userId, Guid clubId);

    Task<ClubChatUnreadDto> MarkReadAsync(
        Guid userId,
        Guid clubId,
        MarkClubChatReadRequest request,
        CancellationToken cancellationToken);
}
