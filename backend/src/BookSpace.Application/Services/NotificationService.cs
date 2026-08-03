using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class NotificationService(IBookSpaceDbContext db) : INotificationService
{
    private readonly ServiceMapper _mapper = new(db);

    public NotificationDto GetOne(Guid userId, Guid notificationId)
    {
        var notification = db.Notifications.FirstOrDefault(x => x.Id == notificationId && x.UserId == userId)
                           ?? throw ServiceErrors.NotFound("NOTIFICATION_NOT_FOUND", "Không tìm thấy thông báo.");
        return _mapper.Notification(notification);
    }

    public PageResult<NotificationDto> Get(
        Guid userId,
        bool? unreadOnly,
        NotificationCategory? category,
        int page,
        int pageSize)
    {
        var query = Filter(db.Notifications.Where(x => x.UserId == userId), category);
        if (unreadOnly == true)
        {
            query = query.Where(x => x.ReadAt == null);
        }

        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(_mapper.Notification)
            .ToList();
        return PageResult<NotificationDto>.Create(items, normalizedPage, size, total);
    }

    public int GetUnreadCount(Guid userId, NotificationCategory? category) =>
        Filter(
                db.Notifications.Where(x => x.UserId == userId && x.ReadAt == null),
                category)
            .Count();

    public NotificationPreferencesDto GetPreferences(Guid userId) =>
        MapPreferences(FindUser(userId));

    public async Task<NotificationPreferencesDto> UpdatePreferencesAsync(
        Guid userId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var user = FindUser(userId);
        user.UpdateNotificationPreferences(
            request.IsFollowNotificationEnabled,
            request.IsReviewNotificationEnabled,
            request.IsClubNotificationEnabled,
            request.IsChallengeNotificationEnabled,
            request.IsDirectMessageNotificationEnabled);
        await db.SaveChangesAsync(cancellationToken);
        return MapPreferences(user);
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = db.Notifications.FirstOrDefault(x => x.Id == notificationId && x.UserId == userId)
                           ?? throw ServiceErrors.NotFound("NOTIFICATION_NOT_FOUND", "Không tìm thấy thông báo.");
        notification.MarkRead();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var notifications = db.Notifications.Where(x => x.UserId == userId && x.ReadAt == null).ToList();
        foreach (var notification in notifications)
        {
            notification.MarkRead();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private User FindUser(Guid userId) =>
        db.Users.FirstOrDefault(x => x.Id == userId)
        ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");

    private static NotificationPreferencesDto MapPreferences(User user) =>
        new(
            user.IsFollowNotificationEnabled,
            user.IsReviewNotificationEnabled,
            user.IsClubNotificationEnabled,
            user.IsChallengeNotificationEnabled,
            user.IsDirectMessageNotificationEnabled);

    private static IQueryable<Notification> Filter(
        IQueryable<Notification> query,
        NotificationCategory? category)
    {
        if (category.HasValue && !Enum.IsDefined(category.Value))
        {
            throw ServiceErrors.BadRequest(
                "INVALID_NOTIFICATION_CATEGORY",
                "Nhóm thông báo không hợp lệ.");
        }

        return category switch
        {
            NotificationCategory.FOLLOW => query.Where(x => x.Type == NotificationType.FOLLOW),
            NotificationCategory.REVIEW => query.Where(x =>
                x.Type == NotificationType.REVIEW_LIKE ||
                x.Type == NotificationType.COMMENT),
            NotificationCategory.CLUB => query.Where(x => x.Type == NotificationType.CLUB),
            NotificationCategory.CHALLENGE => query.Where(x => x.Type == NotificationType.CHALLENGE),
            NotificationCategory.DIRECT_MESSAGE => query.Where(x =>
                x.Type == NotificationType.DIRECT_MESSAGE),
            NotificationCategory.SYSTEM => query.Where(x => x.Type == NotificationType.SYSTEM),
            _ => query
        };
    }
}
