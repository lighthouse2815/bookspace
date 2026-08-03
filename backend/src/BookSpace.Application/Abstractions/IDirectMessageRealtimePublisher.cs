using BookSpace.Application.Contracts;

namespace BookSpace.Application.Abstractions;

public interface IDirectMessageRealtimePublisher
{
    Task PublishMessageCreatedAsync(
        DirectMessageDto message,
        IReadOnlyList<Guid> recipientIds,
        CancellationToken cancellationToken);
}
