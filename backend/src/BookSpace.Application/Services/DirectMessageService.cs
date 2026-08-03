using System.Globalization;
using System.Text;
using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class DirectMessageService(
    IBookSpaceDbContext db,
    IDirectMessageRealtimePublisher realtimePublisher,
    IDirectMessageMutationBoundary mutationBoundary,
    TimeProvider timeProvider) : IDirectMessageService
{
    private const int DefaultConversationPageSize = 20;
    private const int DefaultMessagePageSize = 30;

    public ConversationPageDto GetConversations(
        Guid userId,
        string? cursor,
        int pageSize)
    {
        EnsureActiveUser(userId);
        var normalizedSize = pageSize <= 0
            ? DefaultConversationPageSize
            : Math.Clamp(pageSize, 1, 100);
        var blockedUserIds = UserSafetyPolicy.BlockedUserIds(db, userId);
        var query = db.Conversations.Where(conversation =>
            (conversation.UserOneId == userId && !blockedUserIds.Contains(conversation.UserTwoId)) ||
            (conversation.UserTwoId == userId && !blockedUserIds.Contains(conversation.UserOneId)));
        var decodedCursor = DecodeConversationCursor(cursor);
        if (decodedCursor is not null)
        {
            query = query.Where(conversation =>
                conversation.LastActivityAt < decodedCursor.LastActivityAt ||
                (conversation.LastActivityAt == decodedCursor.LastActivityAt &&
                 conversation.Id.CompareTo(decodedCursor.ConversationId) < 0));
        }

        var conversations = query
            .OrderByDescending(x => x.LastActivityAt)
            .ThenByDescending(x => x.Id)
            .Take(normalizedSize + 1)
            .ToList();
        var hasMore = conversations.Count > normalizedSize;
        if (hasMore)
        {
            conversations.RemoveAt(conversations.Count - 1);
        }

        return new ConversationPageDto(
            conversations.Select(conversation => MapConversation(userId, conversation)).ToList(),
            hasMore && conversations.Count > 0
                ? EncodeConversationCursor(conversations[^1])
                : null,
            hasMore);
    }

    public ConversationDto GetConversation(Guid userId, Guid conversationId) =>
        MapConversation(userId, FindAccessibleConversation(userId, conversationId));

    public Task<ConversationDto> StartConversationAsync(
        Guid userId,
        StartConversationRequest request,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            operationCancellationToken => StartConversationCoreAsync(
                userId,
                request,
                operationCancellationToken),
            cancellationToken);

    private async Task<ConversationDto> StartConversationCoreAsync(
        Guid userId,
        StartConversationRequest request,
        CancellationToken cancellationToken)
    {
        EnsureActiveUser(userId);
        if (request.TargetUserId == Guid.Empty || request.TargetUserId == userId)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_CONVERSATION_PARTICIPANT",
                "Người nhận cuộc trò chuyện không hợp lệ.");
        }

        _ = db.Users.FirstOrDefault(x => x.Id == request.TargetUserId && !x.IsLocked)
            ?? throw ServiceErrors.NotFound(
                "USER_NOT_FOUND",
                "Không tìm thấy người dùng.");
        UserSafetyPolicy.EnsureCanInteract(db, userId, request.TargetUserId);
        EnsureMutualFollow(userId, request.TargetUserId);
        var (userOneId, userTwoId) = NormalizeParticipants(userId, request.TargetUserId);
        var conversation = db.Conversations.FirstOrDefault(x =>
            x.UserOneId == userOneId && x.UserTwoId == userTwoId);
        if (conversation is null)
        {
            conversation = new Conversation(userOneId, userTwoId, timeProvider.GetUtcNow());
            db.Add(conversation);
            await db.SaveChangesAsync(cancellationToken);
        }

        return MapConversation(userId, conversation);
    }

    public DirectMessagePageDto GetMessages(
        Guid userId,
        Guid conversationId,
        string? cursor,
        int pageSize)
    {
        _ = FindAccessibleConversation(userId, conversationId);
        var normalizedSize = pageSize <= 0
            ? DefaultMessagePageSize
            : Math.Clamp(pageSize, 1, 100);
        var hiddenUserIds = UserSafetyPolicy.HiddenUserIds(db, userId);
        var query = db.DirectMessages.Where(message =>
            message.ConversationId == conversationId &&
            !hiddenUserIds.Contains(message.SenderId));
        var decodedCursor = DecodeMessageCursor(cursor);
        if (decodedCursor is not null)
        {
            query = query.Where(message =>
                message.CreatedAt < decodedCursor.CreatedAt ||
                (message.CreatedAt == decodedCursor.CreatedAt &&
                 message.Id.CompareTo(decodedCursor.MessageId) < 0));
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

        return new DirectMessagePageDto(
            MapMessages(messages),
            hasMore && messages.Count > 0 ? EncodeMessageCursor(messages[^1]) : null,
            hasMore);
    }

    public async Task<DirectMessageDto> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        SendDirectMessageRequest request,
        CancellationToken cancellationToken)
    {
        var dispatch = await mutationBoundary.ExecuteAsync(
            operationCancellationToken => PersistMessageAsync(
                userId,
                conversationId,
                request,
                operationCancellationToken),
            cancellationToken);
        await realtimePublisher.PublishMessageCreatedAsync(
            dispatch.Message,
            dispatch.RealtimeRecipientIds,
            cancellationToken);
        return dispatch.Message;
    }

    private async Task<MessageDispatch> PersistMessageAsync(
        Guid userId,
        Guid conversationId,
        SendDirectMessageRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = FindAccessibleConversation(userId, conversationId);
        var recipientId = conversation.OtherParticipantId(userId);
        EnsureMutualFollow(userId, recipientId);
        var sender = EnsureActiveUser(userId);
        var now = timeProvider.GetUtcNow();
        var message = new DirectMessage(conversationId, userId, request.Content, now);
        conversation.MarkActivity(now);
        db.Add(message);

        var preview = message.Content.Length <= 180
            ? message.Content
            : $"{message.Content[..177]}...";
        NotificationDelivery.AddIfEnabled(
            db,
            new Notification(
                recipientId,
                NotificationType.DIRECT_MESSAGE,
                $"Tin nhắn mới từ {sender.DisplayName}",
                preview,
                $"/messages/{conversationId}",
                $"direct-message:{message.Id:N}:{recipientId:N}"),
            userId);

        await db.SaveChangesAsync(cancellationToken);
        var realtimeRecipientIds = new List<Guid> { userId };
        if (!UserSafetyPolicy.IsHiddenFrom(db, recipientId, userId))
        {
            realtimeRecipientIds.Add(recipientId);
        }

        return new MessageDispatch(
            MapMessage(message, sender),
            realtimeRecipientIds);
    }

    public DirectMessageUnreadCountDto GetUnreadCount(Guid userId)
    {
        EnsureActiveUser(userId);
        var blockedUserIds = UserSafetyPolicy.BlockedUserIds(db, userId);
        var conversations = db.Conversations
            .Where(conversation =>
                (conversation.UserOneId == userId && !blockedUserIds.Contains(conversation.UserTwoId)) ||
                (conversation.UserTwoId == userId && !blockedUserIds.Contains(conversation.UserOneId)))
            .ToList();
        return new DirectMessageUnreadCountDto(
            conversations.Sum(conversation => BuildUnreadDto(userId, conversation).Count));
    }

    public Task<DirectMessageReadStateDto> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        MarkDirectMessageReadRequest request,
        CancellationToken cancellationToken) =>
        mutationBoundary.ExecuteAsync(
            operationCancellationToken => MarkReadCoreAsync(
                userId,
                conversationId,
                request,
                operationCancellationToken),
            cancellationToken);

    private async Task<DirectMessageReadStateDto> MarkReadCoreAsync(
        Guid userId,
        Guid conversationId,
        MarkDirectMessageReadRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = FindAccessibleConversation(userId, conversationId);
        if (request.LastReadMessageId == Guid.Empty)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_DIRECT_MESSAGE_ID",
                "Mã tin nhắn cuối đã đọc không hợp lệ.");
        }

        var hiddenUserIds = UserSafetyPolicy.HiddenUserIds(db, userId);
        var message = db.DirectMessages.FirstOrDefault(x =>
                          x.Id == request.LastReadMessageId &&
                          x.ConversationId == conversationId &&
                          !hiddenUserIds.Contains(x.SenderId))
                      ?? throw ServiceErrors.NotFound(
                          "DIRECT_MESSAGE_NOT_FOUND",
                          "Không tìm thấy tin nhắn trong cuộc trò chuyện.");
        var state = db.DirectMessageReadStates.FirstOrDefault(x =>
            x.ConversationId == conversationId && x.UserId == userId);
        if (state is null)
        {
            state = new DirectMessageReadState(conversationId, userId);
            db.Add(state);
        }

        if (state.Advance(message.Id, message.CreatedAt))
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return BuildUnreadDto(userId, conversation, state);
    }

    private Conversation FindAccessibleConversation(Guid userId, Guid conversationId)
    {
        EnsureActiveUser(userId);
        var conversation = db.Conversations.FirstOrDefault(x =>
                               x.Id == conversationId &&
                               (x.UserOneId == userId || x.UserTwoId == userId))
                           ?? throw ConversationNotFound();
        var otherParticipantId = conversation.OtherParticipantId(userId);
        if (UserSafetyPolicy.IsBlockedBetween(db, userId, otherParticipantId))
        {
            throw ConversationNotFound();
        }

        return conversation;
    }

    private User EnsureActiveUser(Guid userId) =>
        db.Users.FirstOrDefault(x => x.Id == userId && !x.IsLocked)
        ?? throw ServiceErrors.NotFound(
            "USER_NOT_FOUND",
            "Không tìm thấy người dùng.");

    private void EnsureMutualFollow(Guid firstUserId, Guid secondUserId)
    {
        var firstFollowsSecond = db.Follows.Any(x =>
            x.FollowerId == firstUserId && x.FollowingId == secondUserId);
        var secondFollowsFirst = db.Follows.Any(x =>
            x.FollowerId == secondUserId && x.FollowingId == firstUserId);
        if (!firstFollowsSecond || !secondFollowsFirst)
        {
            throw ServiceErrors.Forbidden(
                "DIRECT_MESSAGE_MUTUAL_FOLLOW_REQUIRED",
                "Hai người cần theo dõi lẫn nhau để nhắn tin riêng.");
        }
    }

    private ConversationDto MapConversation(Guid userId, Conversation conversation)
    {
        var otherParticipantId = conversation.OtherParticipantId(userId);
        var otherParticipant = db.Users.FirstOrDefault(x => x.Id == otherParticipantId && !x.IsLocked)
                               ?? throw ConversationNotFound();
        var hiddenUserIds = UserSafetyPolicy.HiddenUserIds(db, userId);
        var lastMessage = db.DirectMessages
            .Where(x =>
                x.ConversationId == conversation.Id &&
                !hiddenUserIds.Contains(x.SenderId))
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();
        var canSend = db.Follows.Any(x =>
                          x.FollowerId == userId && x.FollowingId == otherParticipantId) &&
                      db.Follows.Any(x =>
                          x.FollowerId == otherParticipantId && x.FollowingId == userId);
        return new ConversationDto(
            conversation.Id,
            ToSummary(otherParticipant),
            lastMessage is null ? null : MapMessage(lastMessage),
            BuildUnreadDto(userId, conversation).Count,
            canSend,
            conversation.LastActivityAt,
            conversation.CreatedAt);
    }

    private DirectMessageReadStateDto BuildUnreadDto(
        Guid userId,
        Conversation conversation,
        DirectMessageReadState? knownState = null)
    {
        var state = knownState ?? db.DirectMessageReadStates.FirstOrDefault(x =>
            x.ConversationId == conversation.Id && x.UserId == userId);
        var lastReadAt = state?.LastReadAt ?? conversation.CreatedAt;
        var lastReadMessageId = state?.LastReadMessageId ?? Guid.Empty;
        var hiddenUserIds = UserSafetyPolicy.HiddenUserIds(db, userId);
        var count = db.DirectMessages.Count(message =>
            message.ConversationId == conversation.Id &&
            message.SenderId != userId &&
            !hiddenUserIds.Contains(message.SenderId) &&
            (message.CreatedAt > lastReadAt ||
             (message.CreatedAt == lastReadAt && message.Id.CompareTo(lastReadMessageId) > 0)));
        return new DirectMessageReadStateDto(
            conversation.Id,
            count,
            state?.LastReadMessageId,
            state?.LastReadAt);
    }

    private IReadOnlyList<DirectMessageDto> MapMessages(
        IReadOnlyCollection<DirectMessage> messages)
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

    private DirectMessageDto MapMessage(DirectMessage message) =>
        MapMessage(message, EnsureActiveUser(message.SenderId));

    private static DirectMessageDto MapMessage(DirectMessage message, User sender) =>
        new(
            message.Id,
            message.ConversationId,
            ToSummary(sender),
            message.Content,
            message.CreatedAt);

    private static UserSummary ToSummary(User user) =>
        new(user.Id, null, user.DisplayName, user.AvatarUrl, user.Role);

    private static (Guid UserOneId, Guid UserTwoId) NormalizeParticipants(
        Guid firstUserId,
        Guid secondUserId) =>
        firstUserId.CompareTo(secondUserId) < 0
            ? (firstUserId, secondUserId)
            : (secondUserId, firstUserId);

    private static string EncodeConversationCursor(Conversation conversation) =>
        EncodeCursor(conversation.LastActivityAt, conversation.Id);

    private static string EncodeMessageCursor(DirectMessage message) =>
        EncodeCursor(message.CreatedAt, message.Id);

    private static string EncodeCursor(DateTimeOffset createdAt, Guid id)
    {
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"{createdAt.UtcDateTime.Ticks}|{id:N}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static ConversationCursor? DecodeConversationCursor(string? cursor)
    {
        var decoded = DecodeCursor(
            cursor,
            "INVALID_CONVERSATION_CURSOR",
            "Con trỏ phân trang cuộc trò chuyện không hợp lệ.");
        return decoded is null ? null : new ConversationCursor(decoded.CreatedAt, decoded.Id);
    }

    private static MessageCursor? DecodeMessageCursor(string? cursor)
    {
        var decoded = DecodeCursor(
            cursor,
            "INVALID_DIRECT_MESSAGE_CURSOR",
            "Con trỏ phân trang tin nhắn riêng không hợp lệ.");
        return decoded is null ? null : new MessageCursor(decoded.CreatedAt, decoded.Id);
    }

    private static CursorValue? DecodeCursor(
        string? cursor,
        string errorCode,
        string errorMessage)
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
                !Guid.TryParseExact(parts[1], "N", out var id))
            {
                throw new FormatException();
            }

            return new CursorValue(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw ServiceErrors.BadRequest(errorCode, errorMessage);
        }
    }

    private static BookSpace.Application.Common.UseCaseException ConversationNotFound() =>
        ServiceErrors.NotFound(
            "CONVERSATION_NOT_FOUND",
            "Không tìm thấy cuộc trò chuyện.");

    private sealed record CursorValue(DateTimeOffset CreatedAt, Guid Id);
    private sealed record ConversationCursor(DateTimeOffset LastActivityAt, Guid ConversationId);
    private sealed record MessageCursor(DateTimeOffset CreatedAt, Guid MessageId);
    private sealed record MessageDispatch(
        DirectMessageDto Message,
        IReadOnlyList<Guid> RealtimeRecipientIds);
}
