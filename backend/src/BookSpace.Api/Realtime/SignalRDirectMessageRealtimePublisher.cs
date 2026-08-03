using BookSpace.Api.Hubs;
using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace BookSpace.Api.Realtime;

public sealed class SignalRDirectMessageRealtimePublisher(
    IHubContext<DirectMessageHub, IDirectMessageClient> hubContext,
    ILogger<SignalRDirectMessageRealtimePublisher> logger) : IDirectMessageRealtimePublisher
{
    public async Task PublishMessageCreatedAsync(
        DirectMessageDto message,
        IReadOnlyList<Guid> recipientIds,
        CancellationToken cancellationToken)
    {
        var userIds = recipientIds
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
                .DirectMessageCreated(message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Realtime tin nhắn riêng bị hủy sau khi đã lưu tin nhắn {MessageId}.",
                message.Id);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Không thể phát realtime cho tin nhắn riêng {MessageId}.",
                message.Id);
        }
    }
}
