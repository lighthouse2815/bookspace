using BookSpace.Domain.Common;
using BookSpace.Domain.Enums;

namespace BookSpace.Domain.Entities;

public sealed class Notification : Entity
{
    private Notification() { }

    public Notification(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? link = null,
        string? deduplicationKey = null)
    {
        UserId = userId;
        Type = type;
        Title = Guard.Required(title, "Tiêu đề thông báo", 200);
        Message = Guard.Required(message, "Nội dung thông báo", 1000);
        Link = Guard.Optional(link, "Đường dẫn thông báo", 1000);
        DeduplicationKey = Guard.Optional(
            deduplicationKey,
            "Khóa chống trùng thông báo",
            200);
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    public string? DeduplicationKey { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public bool IsRead => ReadAt.HasValue;

    public void MarkRead()
    {
        ReadAt ??= DateTimeOffset.UtcNow;
        Touch();
    }
}
