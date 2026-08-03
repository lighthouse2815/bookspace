using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Common;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ContentModerationService(
    IBookSpaceDbContext db,
    TimeProvider timeProvider) : IContentModerationService
{
    public async Task<ContentReportDto> CreateReportAsync(
        Guid reporterId,
        CreateContentReportRequest request,
        CancellationToken cancellationToken)
    {
        EnsureDefined(request.TargetType, "INVALID_REPORT_TARGET_TYPE", "Loại nội dung báo cáo không hợp lệ.");
        EnsureDefined(request.Reason, "INVALID_REPORT_REASON", "Lý do báo cáo không hợp lệ.");
        if (request.TargetId == Guid.Empty)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_REPORT_TARGET_ID",
                "Mã nội dung cần báo cáo không hợp lệ.");
        }

        var reporter = db.Users.FirstOrDefault(x => x.Id == reporterId && !x.IsLocked)
                       ?? throw ServiceErrors.NotFound(
                           "USER_NOT_FOUND",
                           "Không tìm thấy người gửi báo cáo.");
        var target = ResolveTarget(reporterId, request.TargetType, request.TargetId);
        if (db.ContentReports.Any(x =>
                x.ReporterId == reporterId &&
                x.TargetType == request.TargetType &&
                x.TargetId == request.TargetId &&
                x.Status == ContentReportStatus.PENDING))
        {
            throw ServiceErrors.Conflict(
                "CONTENT_REPORT_ALREADY_PENDING",
                "Bạn đã gửi báo cáo cho nội dung này và báo cáo đang chờ xử lý.");
        }

        var report = new ContentReport(
            reporterId,
            request.TargetType,
            request.TargetId,
            target.Owner.Id,
            request.Reason,
            request.Details,
            target.Preview,
            target.Link);
        db.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        return Map(report, reporter, target.Owner, null);
    }

    public PageResult<ContentReportDto> GetReports(
        ContentReportStatus? status,
        ContentReportTargetType? targetType,
        ContentReportReason? reason,
        int page,
        int pageSize)
    {
        if (status.HasValue)
        {
            EnsureDefined(status.Value, "INVALID_REPORT_STATUS", "Trạng thái báo cáo không hợp lệ.");
        }

        if (targetType.HasValue)
        {
            EnsureDefined(targetType.Value, "INVALID_REPORT_TARGET_TYPE", "Loại nội dung báo cáo không hợp lệ.");
        }

        if (reason.HasValue)
        {
            EnsureDefined(reason.Value, "INVALID_REPORT_REASON", "Lý do báo cáo không hợp lệ.");
        }

        var query = db.ContentReports;
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (targetType.HasValue)
        {
            query = query.Where(x => x.TargetType == targetType.Value);
        }

        if (reason.HasValue)
        {
            query = query.Where(x => x.Reason == reason.Value);
        }

        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var reports = query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(size)
            .ToList();
        var userIds = reports
            .SelectMany(x => new[] { x.ReporterId, x.TargetOwnerId, x.ModeratorId ?? Guid.Empty })
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();
        var users = db.Users
            .Where(x => userIds.Contains(x.Id))
            .ToList()
            .ToDictionary(x => x.Id);
        var items = reports
            .Select(report => Map(
                report,
                FindSummaryOrPlaceholder(users, report.ReporterId),
                FindSummaryOrPlaceholder(users, report.TargetOwnerId),
                report.ModeratorId.HasValue
                    ? FindSummaryOrPlaceholder(users, report.ModeratorId.Value)
                    : null))
            .ToList();
        return PageResult<ContentReportDto>.Create(items, normalizedPage, size, total);
    }

    public async Task<ContentReportDto> ResolveReportAsync(
        Guid moderatorId,
        Guid reportId,
        ResolveContentReportRequest request,
        CancellationToken cancellationToken)
    {
        EnsureDefined(request.Status, "INVALID_REPORT_STATUS", "Trạng thái xử lý báo cáo không hợp lệ.");
        EnsureDefined(request.Action, "INVALID_MODERATION_ACTION", "Hành động kiểm duyệt không hợp lệ.");
        var moderator = db.Users.FirstOrDefault(x => x.Id == moderatorId && !x.IsLocked)
                        ?? throw ServiceErrors.NotFound(
                            "MODERATOR_NOT_FOUND",
                            "Không tìm thấy quản trị viên.");
        var report = db.ContentReports.FirstOrDefault(x => x.Id == reportId)
                     ?? throw ServiceErrors.NotFound(
                         "CONTENT_REPORT_NOT_FOUND",
                         "Không tìm thấy báo cáo nội dung.");
        var targetOwner = db.Users.FirstOrDefault(x => x.Id == report.TargetOwnerId)
                          ?? throw ServiceErrors.NotFound(
                              "REPORT_TARGET_OWNER_NOT_FOUND",
                              "Không tìm thấy chủ sở hữu nội dung bị báo cáo.");
        if (targetOwner.Id == moderatorId)
        {
            throw ServiceErrors.Forbidden(
                "CANNOT_MODERATE_OWN_CONTENT",
                "Bạn không thể tự xử lý báo cáo nhắm đến hồ sơ hoặc nội dung của mình.");
        }

        if (request.Action == ModerationAction.CONTENT_REMOVED)
        {
            RemoveTargetContent(report);
        }
        else if (request.Action == ModerationAction.USER_LOCKED)
        {
            if (targetOwner.Role == UserRole.ADMIN)
            {
                throw ServiceErrors.Forbidden(
                    "CANNOT_LOCK_ADMIN_ACCOUNT",
                    "Không thể khóa tài khoản quản trị viên qua hàng đợi kiểm duyệt.");
            }

            targetOwner.Lock();
        }

        var now = timeProvider.GetUtcNow();
        report.Resolve(
            moderatorId,
            request.Status,
            request.Action,
            request.ResolutionNote,
            now);
        if (request.Status == ContentReportStatus.RESOLVED &&
            request.Action == ModerationAction.CONTENT_REMOVED)
        {
            foreach (var sibling in db.ContentReports
                         .Where(x =>
                             x.Id != report.Id &&
                             x.TargetType == report.TargetType &&
                             x.TargetId == report.TargetId &&
                             x.Status == ContentReportStatus.PENDING)
                         .ToList())
            {
                sibling.Resolve(
                    moderatorId,
                    ContentReportStatus.RESOLVED,
                    ModerationAction.CONTENT_REMOVED,
                    "Nội dung đã được xử lý từ một báo cáo liên quan.",
                    now);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        var reporter = db.Users.FirstOrDefault(x => x.Id == report.ReporterId);
        return Map(
            report,
            reporter is null ? PlaceholderSummary(report.ReporterId) : Summary(reporter),
            Summary(targetOwner),
            Summary(moderator));
    }

    private ReportTarget ResolveTarget(
        Guid reporterId,
        ContentReportTargetType targetType,
        Guid targetId) => targetType switch
        {
            ContentReportTargetType.USER => ResolveUserTarget(targetId),
            ContentReportTargetType.REVIEW => ResolveReviewTarget(targetId),
            ContentReportTargetType.REVIEW_COMMENT => ResolveReviewCommentTarget(targetId),
            ContentReportTargetType.CLUB_POST => ResolveClubPostTarget(reporterId, targetId),
            ContentReportTargetType.CLUB_POST_COMMENT => ResolveClubPostCommentTarget(reporterId, targetId),
            ContentReportTargetType.CLUB_CHAT_MESSAGE => ResolveClubChatMessageTarget(reporterId, targetId),
            ContentReportTargetType.DIRECT_MESSAGE => ResolveDirectMessageTarget(reporterId, targetId),
            _ => throw ServiceErrors.BadRequest(
                "INVALID_REPORT_TARGET_TYPE",
                "Loại nội dung báo cáo không hợp lệ.")
        };

    private ReportTarget ResolveUserTarget(Guid targetId)
    {
        var user = db.Users.FirstOrDefault(x => x.Id == targetId && !x.IsLocked)
                   ?? throw TargetNotFound();
        return new ReportTarget(
            user,
            Preview($"Hồ sơ {user.DisplayName}: {user.Bio}"),
            $"/users/{user.Id}");
    }

    private ReportTarget ResolveReviewTarget(Guid targetId)
    {
        var review = db.Reviews.FirstOrDefault(x => x.Id == targetId)
                     ?? throw TargetNotFound();
        return new ReportTarget(
            FindTargetOwner(review.UserId),
            Preview(review.Content),
            $"/books/{review.BookId}");
    }

    private ReportTarget ResolveReviewCommentTarget(Guid targetId)
    {
        var comment = db.ReviewComments.FirstOrDefault(x => x.Id == targetId)
                      ?? throw TargetNotFound();
        var review = db.Reviews.FirstOrDefault(x => x.Id == comment.ReviewId)
                     ?? throw TargetNotFound();
        return new ReportTarget(
            FindTargetOwner(comment.UserId),
            Preview(comment.Content),
            $"/books/{review.BookId}");
    }

    private ReportTarget ResolveClubPostTarget(Guid reporterId, Guid targetId)
    {
        var post = db.ClubPosts.FirstOrDefault(x => x.Id == targetId)
                   ?? throw TargetNotFound();
        EnsureCanViewClub(reporterId, post.ClubId, false);
        return new ReportTarget(
            FindTargetOwner(post.AuthorId),
            Preview(post.Content),
            $"/clubs/{post.ClubId}");
    }

    private ReportTarget ResolveClubPostCommentTarget(Guid reporterId, Guid targetId)
    {
        var comment = db.ClubPostComments.FirstOrDefault(x => x.Id == targetId)
                      ?? throw TargetNotFound();
        var post = db.ClubPosts.FirstOrDefault(x => x.Id == comment.PostId)
                   ?? throw TargetNotFound();
        EnsureCanViewClub(reporterId, post.ClubId, false);
        return new ReportTarget(
            FindTargetOwner(comment.AuthorId),
            Preview(comment.Content),
            $"/clubs/{post.ClubId}");
    }

    private ReportTarget ResolveClubChatMessageTarget(Guid reporterId, Guid targetId)
    {
        var message = db.ClubChatMessages.FirstOrDefault(x => x.Id == targetId)
                      ?? throw TargetNotFound();
        EnsureCanViewClub(reporterId, message.ClubId, true);
        return new ReportTarget(
            FindTargetOwner(message.SenderId),
            Preview(message.Content),
            $"/clubs/{message.ClubId}?tab=chat");
    }

    private ReportTarget ResolveDirectMessageTarget(Guid reporterId, Guid targetId)
    {
        var message = db.DirectMessages.FirstOrDefault(x => x.Id == targetId)
                      ?? throw TargetNotFound();
        var conversation = db.Conversations.FirstOrDefault(x =>
                               x.Id == message.ConversationId &&
                               (x.UserOneId == reporterId || x.UserTwoId == reporterId))
                           ?? throw TargetNotFound();
        if (UserSafetyPolicy.IsHiddenFrom(db, reporterId, message.SenderId))
        {
            throw TargetNotFound();
        }

        return new ReportTarget(
            FindTargetOwner(message.SenderId),
            Preview(message.Content),
            $"/messages/{conversation.Id}");
    }

    private void EnsureCanViewClub(Guid userId, Guid clubId, bool membershipRequired)
    {
        var club = db.BookClubs.FirstOrDefault(x => x.Id == clubId)
                   ?? throw TargetNotFound();
        var isMember = db.BookClubMembers.Any(x => x.ClubId == clubId && x.UserId == userId);
        if ((membershipRequired || club.Visibility == ClubVisibility.PRIVATE) && !isMember)
        {
            throw TargetNotFound();
        }
    }

    private User FindTargetOwner(Guid userId) =>
        db.Users.FirstOrDefault(x => x.Id == userId && !x.IsLocked)
        ?? throw TargetNotFound();

    private void RemoveTargetContent(ContentReport report)
    {
        Entity? target = report.TargetType switch
        {
            ContentReportTargetType.REVIEW =>
                db.ReviewsIncludingDeleted.FirstOrDefault(x => x.Id == report.TargetId),
            ContentReportTargetType.REVIEW_COMMENT =>
                db.ReviewCommentsIncludingDeleted.FirstOrDefault(x => x.Id == report.TargetId),
            ContentReportTargetType.CLUB_POST =>
                db.ClubPostsIncludingDeleted.FirstOrDefault(x => x.Id == report.TargetId),
            ContentReportTargetType.CLUB_POST_COMMENT =>
                db.ClubPostCommentsIncludingDeleted.FirstOrDefault(x => x.Id == report.TargetId),
            ContentReportTargetType.CLUB_CHAT_MESSAGE =>
                db.ClubChatMessagesIncludingDeleted.FirstOrDefault(x => x.Id == report.TargetId),
            ContentReportTargetType.DIRECT_MESSAGE =>
                db.DirectMessagesIncludingDeleted.FirstOrDefault(x => x.Id == report.TargetId),
            ContentReportTargetType.USER => throw ServiceErrors.BadRequest(
                "PROFILE_CANNOT_BE_REMOVED_AS_CONTENT",
                "Hồ sơ người dùng không thể bị xử lý như một nội dung riêng lẻ."),
            _ => null
        };
        if (target is null)
        {
            throw ServiceErrors.NotFound(
                "REPORT_TARGET_NOT_FOUND",
                "Nội dung bị báo cáo không còn tồn tại.");
        }

        if (!target.IsDeleted)
        {
            target.SoftDelete();
        }
    }

    private static ContentReportDto Map(
        ContentReport report,
        User reporter,
        User targetOwner,
        User? moderator) => Map(
            report,
            Summary(reporter),
            Summary(targetOwner),
            moderator is null ? null : Summary(moderator));

    private static ContentReportDto Map(
        ContentReport report,
        UserSummary reporter,
        UserSummary targetOwner,
        UserSummary? moderator) => new(
            report.Id,
            reporter,
            report.TargetType,
            report.TargetId,
            targetOwner,
            report.Reason,
            report.Details,
            report.TargetPreview,
            report.TargetLink,
            report.Status,
            report.Action,
            moderator,
            report.ResolutionNote,
            report.ResolvedAt,
            report.CreatedAt);

    private static UserSummary Summary(User user) => new(
        user.Id,
        null,
        user.DisplayName,
        user.AvatarUrl,
        user.Role);

    private static UserSummary FindSummaryOrPlaceholder(
        IReadOnlyDictionary<Guid, User> users,
        Guid userId) => users.TryGetValue(userId, out var user)
        ? Summary(user)
        : PlaceholderSummary(userId);

    private static UserSummary PlaceholderSummary(Guid userId) => new(
        userId,
        null,
        "Tài khoản không còn hoạt động",
        null,
        UserRole.USER);

    private static string Preview(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "Nội dung không có mô tả."
            : value.Trim();
        return normalized.Length <= 500 ? normalized : $"{normalized[..497]}...";
    }

    private static UseCaseException TargetNotFound() => ServiceErrors.NotFound(
        "REPORT_TARGET_NOT_FOUND",
        "Không tìm thấy nội dung có thể báo cáo.");

    private static void EnsureDefined<TEnum>(TEnum value, string code, string message)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw ServiceErrors.BadRequest(code, message);
        }
    }

    private sealed record ReportTarget(User Owner, string Preview, string Link);
}
