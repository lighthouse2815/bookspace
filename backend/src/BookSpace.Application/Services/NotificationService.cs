using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;

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

    public PageResult<NotificationDto> Get(Guid userId, bool? unreadOnly, int page, int pageSize)
    {
        var query = db.Notifications.Where(x => x.UserId == userId);
        if (unreadOnly == true)
        {
            query = query.Where(x => x.ReadAt == null);
        }

        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(_mapper.Notification)
            .ToList();
        return PageResult<NotificationDto>.Create(items, normalizedPage, size, total);
    }

    public int GetUnreadCount(Guid userId) =>
        db.Notifications.Count(x => x.UserId == userId && x.ReadAt == null);

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
}
