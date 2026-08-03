using BookSpace.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BookSpace.Api.Hubs;

public interface IDirectMessageClient
{
    Task DirectMessageCreated(DirectMessageDto message);
}

[Authorize]
public sealed class DirectMessageHub : Hub<IDirectMessageClient>;
