using System.Globalization;
using System.Text;
using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ClubChatService(
    IBookSpaceDbContext db,
    IClubChatRealtimePublisher realtimePublisher,
    IClubChatMutationBoundary mutationBoundary,
    TimeProvider timeProvider) : IClubChatService
{
    private const int DefaultPageSize = 30;

    public ClubChatMessagePageDto GetMessages(
        Guid userId,
        Guid clubId,
        string? cursor,
        int pageSize)
    {
        _ = FindActiveMembership(userId, clubId);
        var normalizedSize = pageSize <= 0
            ? DefaultPageSize
            : Math.Clamp(pageSize, 1, 100);
        var hiddenUserIds = UserSafetyPolicy.HiddenUserIds(db, userId);
        var query = db.ClubChatMessages.Where(x =>
            x.ClubId == clubId &&
            !hiddenUserIds.Contains(x.SenderId));
        var decodedCursor = DecodeCursor(cursor);
        if (decodedCursor is not null)
        {
            query = query.Where(x =>
                x.CreatedAt < decodedCursor.CreatedAt ||
                (x.CreatedAt == decodedCursor.CreatedAt &&
                 x.Id.CompareTo(decodedCursor.MessageId) < 0));
        }

        var messages = query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(normalizedSize + 1)
            .ToList();
        var hasMore = messages.Count > normalizedSize;
        if (hasMore)
        {
            messages.RemoveAt(messages.Count - 1);
        }

        var items = MapMessages(messages);
        var nextCursor = hasMore && messages.Count > 0
            ? EncodeCursor(messages[^1])
            : null;
        return new ClubChatMessagePageDto(items, nextCursor, hasMore);
    }

    public async Task<ClubChatMessageDto> SendMessageAsync(
        Guid userId,
        Guid clubId,
        SendClubChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var dispatch = await mutationBoundary.ExecuteAsync(
            operationCancellationToken => PersistMessageAsync(
                userId,
                clubId,
                request,
                operationCancellationToken),
            cancellationToken);
        await realtimePublisher.PublishMessageCreatedAsync(
            dispatch.Message,
            dispatch.ActiveMemberIds,
            cancellationToken);
        return dispatch.Message;
    }

    private async Task<MessageDispatch> PersistMessageAsync(
        Guid userId,
        Guid clubId,
        SendClubChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        _ = FindActiveMembership(userId, clubId);
        var sender = db.Users.FirstOrDefault(x => x.Id == userId)
                     ?? throw ServiceErrors.NotFound(
                         "USER_NOT_FOUND",
                         "Không tìm thấy người dùng.");
        var club = db.BookClubs.First(x => x.Id == clubId);
        var message = new ClubChatMessage(
            clubId,
            userId,
            request.Content,
            timeProvider.GetUtcNow());
        db.Add(message);

        var activeMemberIds = db.BookClubMembers
            .Where(x => x.ClubId == clubId)
            .Select(x => x.UserId)
            .Distinct()
            .ToList()
            .Where(recipientId =>
                recipientId == userId ||
                !UserSafetyPolicy.IsHiddenFrom(db, recipientId, userId))
            .ToList();
        var preview = message.Content.Length <= 180
            ? message.Content
            : $"{message.Content[..177]}...";
        NotificationDelivery.AddRangeIfEnabled(
            db,
            activeMemberIds
                .Where(recipientId => recipientId != userId)
                .Select(recipientId => new Notification(
                    recipientId,
                    NotificationType.CLUB,
                    $"Tin nhắn mới trong {club.Name}",
                    $"{sender.DisplayName}: {preview}",
                    $"/clubs/{clubId}?tab=chat",
                    $"club-chat:{message.Id:N}:{recipientId:N}")),
            userId);

        await db.SaveChangesAsync(cancellationToken);
        var dto = MapMessage(message, sender);
        return new MessageDispatch(dto, activeMemberIds);
    }

    public ClubChatUnreadDto GetUnreadCount(Guid userId, Guid clubId)
    {
        var membership = FindActiveMembership(userId, clubId);
        return BuildUnreadDto(userId, clubId, membership);
    }

    public Task<ClubChatUnreadDto> MarkReadAsync(
        Guid userId,
        Guid clubId,
        MarkClubChatReadRequest request,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            operationCancellationToken => MarkReadCoreAsync(
                userId,
                clubId,
                request,
                operationCancellationToken),
            cancellationToken);

    private async Task<ClubChatUnreadDto> MarkReadCoreAsync(
        Guid userId,
        Guid clubId,
        MarkClubChatReadRequest request,
        CancellationToken cancellationToken)
    {
        var membership = FindActiveMembership(userId, clubId);
        if (request.LastReadMessageId == Guid.Empty)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_CHAT_MESSAGE_ID",
                "Mã tin nhắn cuối đã đọc không hợp lệ.");
        }

        var message = db.ClubChatMessages.FirstOrDefault(x =>
                          x.Id == request.LastReadMessageId &&
                          x.ClubId == clubId &&
                          !UserSafetyPolicy.HiddenUserIds(db, userId).Contains(x.SenderId))
                      ?? throw ServiceErrors.NotFound(
                          "CLUB_CHAT_MESSAGE_NOT_FOUND",
                          "Không tìm thấy tin nhắn trong câu lạc bộ.");
        if (ComparePosition(
                message.CreatedAt,
                message.Id,
                membership.CreatedAt,
                Guid.Empty) <= 0)
        {
            return BuildUnreadDto(userId, clubId, membership);
        }

        var state = db.ClubChatReadStates.FirstOrDefault(x => x.MembershipId == membership.Id);
        if (state is null)
        {
            state = new ClubChatReadState(membership.Id);
            db.Add(state);
        }

        if (state.Advance(message.Id, message.CreatedAt))
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return BuildUnreadDto(userId, clubId, membership, state);
    }

    private BookClubMember FindActiveMembership(Guid userId, Guid clubId)
    {
        var club = db.BookClubs.FirstOrDefault(x => x.Id == clubId)
                   ?? throw ServiceErrors.NotFound(
                       "CLUB_NOT_FOUND",
                       "Không tìm thấy câu lạc bộ.");
        var membership = db.BookClubMembers.FirstOrDefault(x =>
            x.ClubId == clubId &&
            x.UserId == userId);
        if (membership is not null)
        {
            return membership;
        }

        if (club.Visibility == ClubVisibility.PRIVATE)
        {
            throw ServiceErrors.NotFound(
                "CLUB_NOT_FOUND",
                "Không tìm thấy câu lạc bộ.");
        }

        throw ServiceErrors.Forbidden(
            "CLUB_CHAT_MEMBERSHIP_REQUIRED",
            "Bạn cần tham gia câu lạc bộ để sử dụng phòng trò chuyện.");
    }

    private ClubChatUnreadDto BuildUnreadDto(
        Guid userId,
        Guid clubId,
        BookClubMember membership,
        ClubChatReadState? knownState = null)
    {
        var state = knownState ??
                    db.ClubChatReadStates.FirstOrDefault(x => x.MembershipId == membership.Id);
        var lastReadAt = state?.LastReadAt ?? membership.CreatedAt;
        var lastReadMessageId = state?.LastReadMessageId ?? Guid.Empty;
        var hiddenUserIds = UserSafetyPolicy.HiddenUserIds(db, userId);
        var count = db.ClubChatMessages.Count(x =>
            x.ClubId == clubId &&
            x.SenderId != userId &&
            !hiddenUserIds.Contains(x.SenderId) &&
            (x.CreatedAt > lastReadAt ||
             (x.CreatedAt == lastReadAt && x.Id.CompareTo(lastReadMessageId) > 0)));
        return new ClubChatUnreadDto(
            clubId,
            count,
            state?.LastReadMessageId,
            state?.LastReadAt);
    }

    private IReadOnlyList<ClubChatMessageDto> MapMessages(
        IReadOnlyCollection<ClubChatMessage> messages)
    {
        var senderIds = messages.Select(x => x.SenderId).Distinct().ToList();
        var senders = db.Users
            .Where(x => senderIds.Contains(x.Id))
            .ToList()
            .ToDictionary(x => x.Id);
        return messages
            .Select(message => MapMessage(
                message,
                senders.TryGetValue(message.SenderId, out var sender)
                    ? sender
                    : throw ServiceErrors.NotFound(
                        "USER_NOT_FOUND",
                        "Không tìm thấy người gửi tin nhắn.")))
            .ToList();
    }

    private static ClubChatMessageDto MapMessage(ClubChatMessage message, User sender) =>
        new(
            message.Id,
            message.ClubId,
            new UserSummary(
                sender.Id,
                null,
                sender.DisplayName,
                sender.AvatarUrl,
                sender.Role),
            message.Content,
            message.CreatedAt);

    private static int ComparePosition(
        DateTimeOffset leftAt,
        Guid leftId,
        DateTimeOffset rightAt,
        Guid rightId)
    {
        var timeComparison = leftAt.CompareTo(rightAt);
        return timeComparison != 0 ? timeComparison : leftId.CompareTo(rightId);
    }

    private static string EncodeCursor(ClubChatMessage message)
    {
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"{message.CreatedAt.UtcDateTime.Ticks}|{message.Id:N}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static ChatCursor? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var normalized = cursor.Trim().Replace('-', '+').Replace('_', '/');
            normalized = (normalized.Length % 4) switch
            {
                2 => $"{normalized}==",
                3 => $"{normalized}=",
                _ => normalized
            };
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            var parts = raw.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                ticks < DateTimeOffset.MinValue.UtcDateTime.Ticks ||
                ticks > DateTimeOffset.MaxValue.UtcDateTime.Ticks ||
                !Guid.TryParseExact(parts[1], "N", out var messageId))
            {
                throw new FormatException();
            }

            return new ChatCursor(new DateTimeOffset(ticks, TimeSpan.Zero), messageId);
        }
        catch (Exception exception) when (
            exception is FormatException or ArgumentException)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_CHAT_CURSOR",
                "Con trỏ phân trang tin nhắn không hợp lệ.");
        }
    }

    private sealed record ChatCursor(DateTimeOffset CreatedAt, Guid MessageId);

    private sealed record MessageDispatch(
        ClubChatMessageDto Message,
        IReadOnlyList<Guid> ActiveMemberIds);
}
