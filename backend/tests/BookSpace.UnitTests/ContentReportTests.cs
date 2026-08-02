using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.UnitTests;

public sealed class ContentReportTests
{
    [Fact]
    public void Report_rejects_own_content_and_invalid_resolution_combinations()
    {
        var userId = Guid.NewGuid();
        var ownContent = Assert.Throws<DomainException>(() => new ContentReport(
            userId,
            ContentReportTargetType.REVIEW,
            Guid.NewGuid(),
            userId,
            ContentReportReason.SPAM,
            null,
            "Nội dung",
            "/books/1"));
        Assert.Equal("CANNOT_REPORT_OWN_CONTENT", ownContent.Code);

        var report = CreateReport();
        var dismissedWithAction = Assert.Throws<DomainException>(() => report.Resolve(
            Guid.NewGuid(),
            ContentReportStatus.DISMISSED,
            ModerationAction.CONTENT_REMOVED,
            null,
            DateTimeOffset.UtcNow));
        Assert.Equal("DISMISSED_REPORT_CANNOT_MODERATE", dismissedWithAction.Code);

        var resolvedWithoutAction = Assert.Throws<DomainException>(() => report.Resolve(
            Guid.NewGuid(),
            ContentReportStatus.RESOLVED,
            ModerationAction.NONE,
            null,
            DateTimeOffset.UtcNow));
        Assert.Equal("RESOLVED_REPORT_REQUIRES_ACTION", resolvedWithoutAction.Code);
    }

    [Fact]
    public void Resolution_is_audited_and_exact_retry_is_idempotent()
    {
        var report = CreateReport();
        var moderatorId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 2, 8, 0, 0, TimeSpan.Zero);

        Assert.True(report.Resolve(
            moderatorId,
            ContentReportStatus.RESOLVED,
            ModerationAction.CONTENT_REMOVED,
            "Đã xác minh vi phạm.",
            now));
        Assert.False(report.Resolve(
            moderatorId,
            ContentReportStatus.RESOLVED,
            ModerationAction.CONTENT_REMOVED,
            "Đã xác minh vi phạm.",
            now.AddMinutes(1)));

        Assert.Equal(ContentReportStatus.RESOLVED, report.Status);
        Assert.Equal(ModerationAction.CONTENT_REMOVED, report.Action);
        Assert.Equal(moderatorId, report.ModeratorId);
        Assert.Equal(now, report.ResolvedAt);

        var conflictingRetry = Assert.Throws<DomainException>(() => report.Resolve(
            moderatorId,
            ContentReportStatus.DISMISSED,
            ModerationAction.NONE,
            null,
            now.AddMinutes(2)));
        Assert.Equal("CONTENT_REPORT_ALREADY_REVIEWED", conflictingRetry.Code);
    }

    private static ContentReport CreateReport() => new(
        Guid.NewGuid(),
        ContentReportTargetType.REVIEW,
        Guid.NewGuid(),
        Guid.NewGuid(),
        ContentReportReason.HARASSMENT,
        "Chi tiết báo cáo",
        "Nội dung cần xem xét",
        "/books/1");
}
