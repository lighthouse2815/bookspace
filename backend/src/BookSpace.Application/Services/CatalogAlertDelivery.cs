using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

internal static class CatalogAlertDelivery
{
    public static void AddNewBookAlerts(
        IBookSpaceDbContext db,
        Book book,
        Guid authorId,
        IReadOnlyCollection<Guid> categoryIds)
    {
        var normalizedCategoryIds = categoryIds.Distinct().ToList();
        var candidateUserIds = db.UserAuthorFollows
            .Where(link => link.AuthorId == authorId)
            .Select(link => link.UserId)
            .Concat(db.UserCategoryFollows
                .Where(link => normalizedCategoryIds.Contains(link.CategoryId))
                .Select(link => link.UserId))
            .Distinct()
            .ToList();
        if (candidateUserIds.Count == 0)
        {
            return;
        }

        var recipientIds = db.Users
            .Where(user =>
                candidateUserIds.Contains(user.Id) &&
                !user.IsLocked &&
                user.DeletedAt == null &&
                user.IsCatalogNotificationEnabled)
            .Select(user => user.Id)
            .ToList();
        var deduplicationKeys = recipientIds
            .ToDictionary(userId => userId, userId => $"catalog-book:{book.Id}:user:{userId}");
        var keys = deduplicationKeys.Values.ToList();
        var existingKeys = db.Notifications
            .Where(notification =>
                notification.DeduplicationKey != null &&
                keys.Contains(notification.DeduplicationKey))
            .Select(notification => notification.DeduplicationKey!)
            .ToHashSet(StringComparer.Ordinal);

        var notifications = recipientIds
            .Where(userId => !existingKeys.Contains(deduplicationKeys[userId]))
            .Select(userId => new Notification(
                userId,
                NotificationType.CATALOG,
                "Sách mới từ nội dung bạn theo dõi",
                $"“{book.Title}” vừa được thêm vào BookSpace.",
                $"/books/{book.Id}",
                deduplicationKeys[userId]))
            .ToList();
        db.AddRange(notifications);
    }
}
