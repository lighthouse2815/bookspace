using BookSpace.Application.Contracts;

namespace BookSpace.Application.Abstractions;

public interface IClubChatRealtimePublisher
{
    Task PublishMessageCreatedAsync(
        ClubChatMessageDto message,
        IReadOnlyList<Guid> activeMemberIds,
        CancellationToken cancellationToken);
}
