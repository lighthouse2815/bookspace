using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notificationService) : ApiControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<PageResult<NotificationDto>>> Notifications(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(notificationService.Get(CurrentUserId, unreadOnly, page, pageSize));

    [HttpGet("unread-count")]
    public ActionResult<ApiResponse<object>> UnreadCount() =>
        OkData<object>(new { count = notificationService.GetUnreadCount(CurrentUserId) });

    [HttpPatch("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<NotificationDto>>> MarkRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        await notificationService.MarkReadAsync(CurrentUserId, id, cancellationToken);
        return OkData(notificationService.GetOne(CurrentUserId, id), "Đã đánh dấu thông báo là đã đọc.");
    }

    [HttpPatch("read-all")]
    public async Task<ActionResult<ApiResponse<object?>>> MarkAllRead(CancellationToken cancellationToken)
    {
        await notificationService.MarkAllReadAsync(CurrentUserId, cancellationToken);
        return OkEmptyData("Đã đọc tất cả thông báo.");
    }
}
