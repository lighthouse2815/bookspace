using BookSpace.Api.Hubs;
using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace BookSpace.Api.Realtime;

public sealed class SignalRClubChatRealtimePublisher(
    IHubContext<ClubChatHub, IClubChatClient> hubContext,
    ILogger<SignalRClubChatRealtimePublisher> logger) : IClubChatRealtimePublisher
{
    public async Task PublishMessageCreatedAsync(
        ClubChatMessageDto message,
        IReadOnlyList<Guid> activeMemberIds,
        CancellationToken cancellationToken)
    {
        var userIds = activeMemberIds
            .Distinct()
            .Select(userId => userId.ToString())
            .ToList();
        if (userIds.Count == 0)
        {
            return;
        }

        try
        {
            await hubContext.Clients
                .Users(userIds)
                .ClubChatMessageCreated(message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Realtime Club Chat bị hủy sau khi đã lưu tin nhắn {MessageId}.",
                message.Id);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Không thể phát realtime cho tin nhắn Club Chat {MessageId}.",
                message.Id);
        }
    }
}
