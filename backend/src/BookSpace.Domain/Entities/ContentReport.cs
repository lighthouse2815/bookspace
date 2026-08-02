using BookSpace.Domain.Common;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class ContentReport : Entity
{
    private ContentReport() { }

    public ContentReport(
        Guid reporterId,
        ContentReportTargetType targetType,
        Guid targetId,
        Guid targetOwnerId,
        ContentReportReason reason,
        string? details,
        string targetPreview,
        string targetLink)
    {
        if (reporterId == Guid.Empty || targetId == Guid.Empty || targetOwnerId == Guid.Empty)
        {
            throw new DomainException("INVALID_CONTENT_REPORT", "Báo cáo nội dung không hợp lệ.");
        }

        if (reporterId == targetOwnerId)
        {
            throw new DomainException(
                "CANNOT_REPORT_OWN_CONTENT",
                "Bạn không thể báo cáo hồ sơ hoặc nội dung của chính mình.");
        }

        ReporterId = reporterId;
        TargetType = targetType;
        TargetId = targetId;
        TargetOwnerId = targetOwnerId;
        Reason = reason;
        Details = Guard.Optional(details, "Mô tả báo cáo", 1000);
        TargetPreview = Guard.Required(targetPreview, "Nội dung xem trước", 500);
        TargetLink = Guard.Required(targetLink, "Liên kết nội dung", 1000);
        Status = ContentReportStatus.PENDING;
        Action = ModerationAction.NONE;
    }

    public Guid ReporterId { get; private set; }
    public User Reporter { get; private set; } = null!;
    public ContentReportTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid TargetOwnerId { get; private set; }
    public User TargetOwner { get; private set; } = null!;
    public ContentReportReason Reason { get; private set; }
    public string? Details { get; private set; }
    public string TargetPreview { get; private set; } = string.Empty;
    public string TargetLink { get; private set; } = string.Empty;
    public ContentReportStatus Status { get; private set; }
    public ModerationAction Action { get; private set; }
    public Guid? ModeratorId { get; private set; }
    public User? Moderator { get; private set; }
    public string? ResolutionNote { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool Resolve(
        Guid moderatorId,
        ContentReportStatus status,
        ModerationAction action,
        string? resolutionNote,
        DateTimeOffset resolvedAt)
    {
        if (status == ContentReportStatus.PENDING)
        {
            throw new DomainException(
                "INVALID_REPORT_RESOLUTION_STATUS",
                "Trạng thái xử lý báo cáo không hợp lệ.");
        }

        if (status == ContentReportStatus.DISMISSED && action != ModerationAction.NONE)
        {
            throw new DomainException(
                "DISMISSED_REPORT_CANNOT_MODERATE",
                "Báo cáo bị bác bỏ không thể kèm hành động kiểm duyệt.");
        }

        if (status == ContentReportStatus.RESOLVED && action == ModerationAction.NONE)
        {
            throw new DomainException(
                "RESOLVED_REPORT_REQUIRES_ACTION",
                "Báo cáo xác nhận vi phạm phải có hành động xử lý.");
        }

        if (TargetType == ContentReportTargetType.USER && action == ModerationAction.CONTENT_REMOVED)
        {
            throw new DomainException(
                "PROFILE_CANNOT_BE_REMOVED_AS_CONTENT",
                "Hồ sơ người dùng không thể bị xử lý như một nội dung riêng lẻ.");
        }

        var normalizedNote = Guard.Optional(resolutionNote, "Ghi chú xử lý", 1000);
        if (Status != ContentReportStatus.PENDING)
        {
            if (Status == status && Action == action && ResolutionNote == normalizedNote)
            {
                return false;
            }

            throw new DomainException(
                "CONTENT_REPORT_ALREADY_REVIEWED",
                "Báo cáo này đã được xử lý trước đó.");
        }

        Status = status;
        Action = action;
        ModeratorId = moderatorId;
        ResolutionNote = normalizedNote;
        ResolvedAt = resolvedAt.ToUniversalTime();
        Touch();
        return true;
    }
}
