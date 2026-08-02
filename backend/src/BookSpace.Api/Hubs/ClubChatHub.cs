using BookSpace.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BookSpace.Api.Hubs;

public interface IClubChatClient
{
    Task ClubChatMessageCreated(ClubChatMessageDto message);
}

[Authorize]
public sealed class ClubChatHub : Hub<IClubChatClient>;
