using System.ComponentModel.DataAnnotations;

namespace BookSpace.Application.Contracts;

public sealed record ClubChatMessageDto(
    Guid Id,
    Guid ClubId,
    UserSummary Sender,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record ClubChatMessagePageDto(
    IReadOnlyList<ClubChatMessageDto> Items,
    string? NextCursor,
    bool HasMore);

public sealed record SendClubChatMessageRequest(
    [Required(ErrorMessage = "Nội dung tin nhắn không được để trống.")]
    [MaxLength(2000, ErrorMessage = "Nội dung tin nhắn không được vượt quá 2.000 ký tự.")]
    string Content);

public sealed record MarkClubChatReadRequest(
    [Required(ErrorMessage = "Mã tin nhắn cuối đã đọc không được để trống.")]
    Guid LastReadMessageId);

public sealed record ClubChatUnreadDto(
    Guid ClubId,
    int Count,
    Guid? LastReadMessageId,
    DateTimeOffset? LastReadAt);
