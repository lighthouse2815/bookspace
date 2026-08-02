using BookSpace.Api.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Authorize]
[Route("api/clubs/{clubId:guid}/chat")]
public sealed class ClubChatController(IClubChatService clubChatService) : ApiControllerBase
{
    [HttpGet("messages")]
    public ActionResult<ApiResponse<ClubChatMessagePageDto>> Messages(
        Guid clubId,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 30) =>
        OkData(clubChatService.GetMessages(CurrentUserId, clubId, cursor, pageSize));

    [HttpPost("messages")]
    public async Task<ActionResult<ApiResponse<ClubChatMessageDto>>> SendMessage(
        Guid clubId,
        SendClubChatMessageRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await clubChatService.SendMessageAsync(
                CurrentUserId,
                clubId,
                request,
                cancellationToken),
            "Đã gửi tin nhắn.");

    [HttpGet("unread-count")]
    public ActionResult<ApiResponse<ClubChatUnreadDto>> UnreadCount(Guid clubId) =>
        OkData(clubChatService.GetUnreadCount(CurrentUserId, clubId));

    [HttpPost("read")]
    public async Task<ActionResult<ApiResponse<ClubChatUnreadDto>>> MarkRead(
        Guid clubId,
        MarkClubChatReadRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await clubChatService.MarkReadAsync(
                CurrentUserId,
                clubId,
                request,
                cancellationToken),
            "Đã cập nhật trạng thái đọc tin nhắn.");
}
