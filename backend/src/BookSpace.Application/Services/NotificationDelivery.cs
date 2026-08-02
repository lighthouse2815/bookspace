using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

internal static class NotificationDelivery
{
    public static bool IsEnabled(
        IBookSpaceDbContext db,
        Guid userId,
        NotificationType type,
        Guid? actorUserId = null)
    {
        var user = db.Users.FirstOrDefault(x => x.Id == userId);
        return user?.AllowsNotification(type) == true &&
               (!actorUserId.HasValue ||
                !UserSafetyPolicy.IsHiddenFrom(db, userId, actorUserId.Value));
    }

    public static bool AddIfEnabled(
        IBookSpaceDbContext db,
        Notification notification,
        Guid? actorUserId = null)
    {
        if (!IsEnabled(db, notification.UserId, notification.Type, actorUserId))
        {
            return false;
        }

        db.Add(notification);
        return true;
    }

    public static void AddRangeIfEnabled(
        IBookSpaceDbContext db,
        IEnumerable<Notification> notifications,
        Guid? actorUserId = null)
    {
        var candidates = notifications.ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var recipientIds = candidates.Select(x => x.UserId).Distinct().ToList();
        var recipients = db.Users
            .Where(x => recipientIds.Contains(x.Id))
            .ToList()
            .ToDictionary(x => x.Id);
        var enabled = candidates.Where(notification =>
            recipients.TryGetValue(notification.UserId, out var recipient) &&
            recipient.AllowsNotification(notification.Type) &&
            (!actorUserId.HasValue ||
             !UserSafetyPolicy.IsHiddenFrom(db, notification.UserId, actorUserId.Value)));
        db.AddRange(enabled);
    }
}
