using System.ComponentModel.DataAnnotations;

namespace BookSpace.Application.Contracts;

public sealed record ConversationDto(
    Guid Id,
    UserSummary OtherParticipant,
    DirectMessageDto? LastMessage,
    int UnreadCount,
    bool CanSend,
    DateTimeOffset LastActivityAt,
    DateTimeOffset CreatedAt);

public sealed record ConversationPageDto(
    IReadOnlyList<ConversationDto> Items,
    string? NextCursor,
    bool HasMore);

public sealed record DirectMessageDto(
    Guid Id,
    Guid ConversationId,
    UserSummary Sender,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record DirectMessagePageDto(
    IReadOnlyList<DirectMessageDto> Items,
    string? NextCursor,
    bool HasMore);

public sealed record StartConversationRequest(Guid TargetUserId);

public sealed record SendDirectMessageRequest(
    [Required(ErrorMessage = "Nội dung tin nhắn không được để trống.")]
    [MaxLength(2000, ErrorMessage = "Nội dung tin nhắn không được vượt quá 2.000 ký tự.")]
    string Content);

public sealed record MarkDirectMessageReadRequest(
    [Required(ErrorMessage = "Mã tin nhắn cuối đã đọc không được để trống.")]
    Guid LastReadMessageId);

public sealed record DirectMessageReadStateDto(
    Guid ConversationId,
    int Count,
    Guid? LastReadMessageId,
    DateTimeOffset? LastReadAt);

public sealed record DirectMessageUnreadCountDto(int Count);
