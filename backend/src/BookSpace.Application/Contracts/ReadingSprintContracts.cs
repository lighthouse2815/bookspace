using System.ComponentModel.DataAnnotations;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Contracts;

public sealed record SaveReadingSprintRequest(
    Guid BookId,
    [Required(ErrorMessage = "Tên đợt đọc không được để trống.")]
    [MaxLength(200, ErrorMessage = "Tên đợt đọc không được vượt quá 200 ký tự.")]
    string Title,
    [MaxLength(2000, ErrorMessage = "Mô tả đợt đọc không được vượt quá 2000 ký tự.")]
    string? Description,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    ReadingSprintTargetUnit TargetUnit,
    [Range(1, 1_000_000, ErrorMessage = "Mục tiêu đợt đọc phải lớn hơn 0.")]
    int TargetValue);

public sealed record UpdateReadingSprintProgressRequest(
    [Range(0, 1_000_000, ErrorMessage = "Tiến độ không được nhỏ hơn 0.")]
    int ProgressValue,
    [MaxLength(1000, ErrorMessage = "Ghi chú tiến độ không được vượt quá 1000 ký tự.")]
    string? Note);

public sealed record SaveReadingSprintMilestoneRequest(
    [Required(ErrorMessage = "Tên cột mốc không được để trống.")]
    [MaxLength(150, ErrorMessage = "Tên cột mốc không được vượt quá 150 ký tự.")]
    string Title,
    [MaxLength(2000, ErrorMessage = "Mô tả cột mốc không được vượt quá 2000 ký tự.")]
    string? Description,
    [Range(1, 1_000_000, ErrorMessage = "Mốc tiến độ phải lớn hơn 0.")]
    int TargetValue);

public sealed record CreateReadingSprintMilestoneResponseRequest(
    [Required(ErrorMessage = "Nội dung thảo luận không được để trống.")]
    [MaxLength(2000, ErrorMessage = "Nội dung thảo luận không được vượt quá 2000 ký tự.")]
    string Content);

public sealed record ReadingSprintPermissionsDto(
    bool CanManage,
    bool CanJoin,
    bool CanLeave,
    bool CanCheckIn,
    bool CanDiscuss,
    bool CanSendReminder);

public sealed record ReadingSprintParticipantDto(
    Guid Id,
    UserSummary User,
    int ProgressValue,
    int ProgressPercent,
    int Rank,
    DateTimeOffset JoinedAt,
    DateTimeOffset? LeftAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? LastCheckInAt,
    bool IsActive);

public sealed record ReadingSprintCheckInDto(
    Guid Id,
    UserSummary User,
    int ProgressValue,
    int ProgressPercent,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record ReadingSprintMilestoneResponseDto(
    Guid Id,
    Guid MilestoneId,
    UserSummary Author,
    string Content,
    bool CanDelete,
    DateTimeOffset CreatedAt);

public sealed record ReadingSprintMilestoneDto(
    Guid Id,
    string Title,
    string? Description,
    int TargetValue,
    bool ReachedByViewer,
    int ResponseCount,
    DateTimeOffset CreatedAt);

public sealed record ReadingSprintSummaryDto(
    Guid Id,
    Guid ClubId,
    string Title,
    string? Description,
    BookSummary Book,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    ReadingSprintTargetUnit TargetUnit,
    int TargetValue,
    ReadingSprintStatus Status,
    int ParticipantCount,
    int CompletedCount,
    int AverageProgressPercent,
    ReadingSprintParticipantDto? ViewerParticipation,
    ReadingSprintPermissionsDto Permissions,
    UserSummary CreatedBy,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? LastReminderAt,
    DateTimeOffset CreatedAt);

public sealed record ReadingSprintDetailDto(
    Guid Id,
    Guid ClubId,
    string Title,
    string? Description,
    BookSummary Book,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    ReadingSprintTargetUnit TargetUnit,
    int TargetValue,
    ReadingSprintStatus Status,
    int ParticipantCount,
    int CompletedCount,
    int AverageProgressPercent,
    ReadingSprintParticipantDto? ViewerParticipation,
    ReadingSprintPermissionsDto Permissions,
    UserSummary CreatedBy,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? LastReminderAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReadingSprintMilestoneDto> Milestones);
