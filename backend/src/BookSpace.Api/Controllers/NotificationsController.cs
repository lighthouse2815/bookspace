using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Enums;
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
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(
            notificationService.Get(
                CurrentUserId,
                unreadOnly,
                ParseCategory(category),
                page,
                pageSize));

    [HttpGet("unread-count")]
    public ActionResult<ApiResponse<object>> UnreadCount(
        [FromQuery] string? category) =>
        OkData<object>(
            new
            {
                count = notificationService.GetUnreadCount(
                    CurrentUserId,
                    ParseCategory(category))
            });

    [HttpGet("preferences")]
    public ActionResult<ApiResponse<NotificationPreferencesDto>> Preferences() =>
        OkData(notificationService.GetPreferences(CurrentUserId));

    [HttpPatch("preferences")]
    public async Task<ActionResult<ApiResponse<NotificationPreferencesDto>>> UpdatePreferences(
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await notificationService.UpdatePreferencesAsync(
                CurrentUserId,
                request,
                cancellationToken),
            "Cập nhật tùy chọn thông báo thành công.");

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

    private static NotificationCategory? ParseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        if (Enum.TryParse<NotificationCategory>(category, true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new UseCaseException(
            "INVALID_NOTIFICATION_CATEGORY",
            "Nhóm thông báo không hợp lệ.");
    }
}
