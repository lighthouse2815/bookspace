using BookSpace.Api.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Authorize]
[Route("api/conversations")]
public sealed class DirectMessagesController(IDirectMessageService directMessageService)
    : ApiControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<ConversationPageDto>> Conversations(
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20) =>
        OkData(directMessageService.GetConversations(CurrentUserId, cursor, pageSize));

    [HttpGet("unread-count")]
    public ActionResult<ApiResponse<DirectMessageUnreadCountDto>> UnreadCount() =>
        OkData(directMessageService.GetUnreadCount(CurrentUserId));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> StartConversation(
        StartConversationRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await directMessageService.StartConversationAsync(
                CurrentUserId,
                request,
                cancellationToken),
            "Cuộc trò chuyện đã sẵn sàng.");

    [HttpGet("{conversationId:guid}")]
    public ActionResult<ApiResponse<ConversationDto>> Conversation(Guid conversationId) =>
        OkData(directMessageService.GetConversation(CurrentUserId, conversationId));

    [HttpGet("{conversationId:guid}/messages")]
    public ActionResult<ApiResponse<DirectMessagePageDto>> Messages(
        Guid conversationId,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 30) =>
        OkData(directMessageService.GetMessages(
            CurrentUserId,
            conversationId,
            cursor,
            pageSize));

    [HttpPost("{conversationId:guid}/messages")]
    public async Task<ActionResult<ApiResponse<DirectMessageDto>>> SendMessage(
        Guid conversationId,
        SendDirectMessageRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await directMessageService.SendMessageAsync(
                CurrentUserId,
                conversationId,
                request,
                cancellationToken),
            "Đã gửi tin nhắn.");

    [HttpPost("{conversationId:guid}/read")]
    public async Task<ActionResult<ApiResponse<DirectMessageReadStateDto>>> MarkRead(
        Guid conversationId,
        MarkDirectMessageReadRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await directMessageService.MarkReadAsync(
                CurrentUserId,
                conversationId,
                request,
                cancellationToken),
            "Đã cập nhật trạng thái đọc tin nhắn.");
}
