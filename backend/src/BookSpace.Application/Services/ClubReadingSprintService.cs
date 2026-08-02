using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ClubReadingSprintService(
    IBookSpaceDbContext db,
    TimeProvider timeProvider) : IClubReadingSprintService
{
    private readonly ServiceMapper _mapper = new(db);

    public PageResult<ReadingSprintSummaryDto> GetSprints(
        Guid clubId,
        Guid? viewerId,
        ReadingSprintStatus? status,
        int page,
        int pageSize)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, viewerId);
        var now = timeProvider.GetUtcNow();
        var query = db.ClubReadingSprints.Where(x => x.ClubId == clubId);
        if (status.HasValue)
        {
            if (!Enum.IsDefined(status.Value))
            {
                throw ServiceErrors.BadRequest(
                    "INVALID_READING_SPRINT_STATUS",
                    "Trạng thái đợt đọc không hợp lệ.");
            }

            query = status.Value switch
            {
                ReadingSprintStatus.PLANNED => query.Where(x =>
                    x.CompletedAt == null &&
                    x.CancelledAt == null &&
                    x.StartsAt > now),
                ReadingSprintStatus.ACTIVE => query.Where(x =>
                    x.CompletedAt == null &&
                    x.CancelledAt == null &&
                    x.StartsAt <= now &&
                    x.EndsAt > now),
                ReadingSprintStatus.ENDED => query.Where(x =>
                    x.CompletedAt == null &&
                    x.CancelledAt == null &&
                    x.EndsAt <= now),
                ReadingSprintStatus.COMPLETED => query.Where(x =>
                    x.CompletedAt != null &&
                    x.CancelledAt == null),
                ReadingSprintStatus.CANCELLED => query.Where(x =>
                    x.CancelledAt != null),
                _ => query
            };
        }

        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(x => MapSummary(x, viewerId, now))
            .ToList();
        return PageResult<ReadingSprintSummaryDto>.Create(
            items,
            normalizedPage,
            size,
            total);
    }

    public ReadingSprintDetailDto GetSprint(Guid clubId, Guid sprintId, Guid? viewerId)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, viewerId);
        var sprint = FindSprint(clubId, sprintId);
        return MapDetail(sprint, viewerId, timeProvider.GetUtcNow());
    }

    public async Task<ReadingSprintDetailDto> CreateAsync(
        Guid actorId,
        Guid clubId,
        SaveReadingSprintRequest request,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, actorId);
        EnsureManager(clubId, actorId);
        var now = timeProvider.GetUtcNow();
        var book = FindAndValidateBook(request.BookId, request.TargetUnit, request.TargetValue);
        EnsurePeriodCanBeCreated(request.StartsAt, request.EndsAt, now);

        var sprint = new ClubReadingSprint(
            clubId,
            book.Id,
            actorId,
            request.Title,
            request.Description,
            request.StartsAt,
            request.EndsAt,
            request.TargetUnit,
            request.TargetValue,
            now);
        db.Add(sprint);
        AddClubMemberNotifications(
            clubId,
            actorId,
            "Đợt đọc chung mới",
            $"{club.Name} vừa mở đợt đọc “{sprint.Title}”.",
            SprintLink(clubId, sprint.Id));
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(sprint, actorId, now);
    }

    public async Task<ReadingSprintDetailDto> UpdateAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        SaveReadingSprintRequest request,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, actorId);
        EnsureManager(clubId, actorId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        if (sprint.GetStatus(now) != ReadingSprintStatus.PLANNED)
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_UPDATE_NOT_ALLOWED",
                "Chỉ có thể chỉnh sửa đợt đọc trước thời điểm bắt đầu.");
        }

        var book = FindAndValidateBook(request.BookId, request.TargetUnit, request.TargetValue);
        EnsurePeriodCanBeCreated(request.StartsAt, request.EndsAt, now);
        var hasParticipants = db.ClubReadingSprintParticipants.Any(x => x.SprintId == sprintId);
        var hasMilestones = db.ClubReadingSprintMilestonesIncludingDeleted.Any(
            x => x.SprintId == sprintId);
        if (request.TargetUnit != sprint.TargetUnit && (hasParticipants || hasMilestones))
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_TARGET_UNIT_LOCKED",
                "Không thể đổi đơn vị mục tiêu khi đợt đọc đã có người tham gia hoặc cột mốc.");
        }

        var greatestProgress = db.ClubReadingSprintParticipants
            .Where(x => x.SprintId == sprintId)
            .Select(x => (int?)x.ProgressValue)
            .Max() ?? 0;
        if (request.TargetValue < greatestProgress)
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_TARGET_BELOW_PROGRESS",
                "Mục tiêu mới không thể thấp hơn tiến độ hiện tại của thành viên.");
        }

        var greatestMilestone = db.ClubReadingSprintMilestones
            .Where(x => x.SprintId == sprintId)
            .Select(x => (int?)x.TargetValue)
            .Max() ?? 0;
        if (request.TargetValue < greatestMilestone)
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_TARGET_BELOW_MILESTONE",
                "Mục tiêu mới không thể thấp hơn cột mốc đã tạo.");
        }

        sprint.Update(
            book.Id,
            request.Title,
            request.Description,
            request.StartsAt,
            request.EndsAt,
            request.TargetUnit,
            request.TargetValue,
            now);
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(sprint, actorId, now);
    }

    public async Task<ReadingSprintParticipantDto> JoinAsync(
        Guid userId,
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, userId);
        var membership = FindActiveClubMembership(clubId, userId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        EnsureParticipationMutable(sprint, now);
        var participant = db.ClubReadingSprintParticipants.FirstOrDefault(x =>
            x.SprintId == sprintId &&
            x.UserId == userId);
        var changed = false;
        if (participant is null)
        {
            participant = new ClubReadingSprintParticipant(sprintId, userId, now);
            db.Add(participant);
            changed = true;
        }
        else
        {
            changed = participant.Rejoin(now);
        }

        if (changed)
        {
            sprint.RecordActivity(now);
            membership.RecordActivity(now);
            await db.SaveChangesAsync(cancellationToken);
        }

        return MapParticipant(participant, sprint.TargetValue, RankFor(participant, sprintId));
    }

    public async Task<ReadingSprintParticipantDto> LeaveAsync(
        Guid userId,
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, userId);
        EnsureActiveClubMember(clubId, userId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        EnsureParticipationMutable(sprint, now);
        var participant = FindParticipant(sprintId, userId);
        if (participant.Leave(now))
        {
            sprint.RecordActivity(now);
            await db.SaveChangesAsync(cancellationToken);
        }

        return MapParticipant(participant, sprint.TargetValue, 0);
    }

    public async Task<ReadingSprintParticipantDto> UpdateProgressAsync(
        Guid userId,
        Guid clubId,
        Guid sprintId,
        UpdateReadingSprintProgressRequest request,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, userId);
        EnsureActiveClubMember(clubId, userId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        EnsureActive(sprint, now);
        var participant = FindActiveParticipant(sprintId, userId);
        if (request.ProgressValue < participant.ProgressValue)
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_PROGRESS_CANNOT_DECREASE",
                "Tiến độ đợt đọc không thể giảm.");
        }

        if (request.ProgressValue > sprint.TargetValue)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_READING_SPRINT_PROGRESS",
                $"Tiến độ phải từ 0 đến {sprint.TargetValue}.");
        }

        if (request.ProgressValue == participant.ProgressValue)
        {
            return MapParticipant(
                participant,
                sprint.TargetValue,
                RankFor(participant, sprintId));
        }

        participant.UpdateProgress(request.ProgressValue, sprint.TargetValue, now);
        sprint.RecordActivity(now);
        db.Add(new ClubReadingSprintCheckIn(
            participant.Id,
            sprintId,
            userId,
            request.ProgressValue,
            request.Note,
            now));
        await db.SaveChangesAsync(cancellationToken);
        return MapParticipant(participant, sprint.TargetValue, RankFor(participant, sprintId));
    }

    public PageResult<ReadingSprintParticipantDto> GetLeaderboard(
        Guid clubId,
        Guid sprintId,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, viewerId);
        var sprint = FindSprint(clubId, sprintId);
        var ranked = ActiveParticipants(sprintId)
            .OrderByDescending(x => x.ProgressValue)
            .ThenBy(x => x.CompletedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.LastCheckInAt ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.JoinedAt)
            .ThenBy(x => x.Id)
            .ToList();
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var items = ranked
            .Skip(skip)
            .Take(size)
            .Select((participant, index) =>
                MapParticipant(participant, sprint.TargetValue, skip + index + 1))
            .ToList();
        return PageResult<ReadingSprintParticipantDto>.Create(
            items,
            normalizedPage,
            size,
            ranked.Count);
    }

    public PageResult<ReadingSprintCheckInDto> GetTimeline(
        Guid clubId,
        Guid sprintId,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, viewerId);
        var sprint = FindSprint(clubId, sprintId);
        var query = db.ClubReadingSprintCheckIns.Where(x => x.SprintId == sprintId);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(x => MapCheckIn(x, sprint.TargetValue))
            .ToList();
        return PageResult<ReadingSprintCheckInDto>.Create(
            items,
            normalizedPage,
            size,
            total);
    }

    public async Task<ReadingSprintMilestoneDto> CreateMilestoneAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        SaveReadingSprintMilestoneRequest request,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, actorId);
        EnsureManager(clubId, actorId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        EnsureMilestoneMutable(sprint, now);
        EnsureMilestoneTarget(request.TargetValue, sprint.TargetValue);
        var milestone = new ClubReadingSprintMilestone(
            sprintId,
            actorId,
            request.Title,
            request.Description,
            request.TargetValue,
            sprint.TargetValue,
            now);
        db.Add(milestone);
        sprint.RecordActivity(now);
        AddParticipantNotifications(
            clubId,
            sprintId,
            actorId,
            onlyIncomplete: false,
            sprint.TargetValue,
            "Cột mốc thảo luận mới",
            $"Đợt đọc “{sprint.Title}” vừa có cột mốc “{milestone.Title}”.",
            SprintLink(clubId, sprintId));
        await db.SaveChangesAsync(cancellationToken);
        return MapMilestone(milestone, actorId);
    }

    public async Task<ReadingSprintMilestoneDto> UpdateMilestoneAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        Guid milestoneId,
        SaveReadingSprintMilestoneRequest request,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, actorId);
        EnsureManager(clubId, actorId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        EnsureMilestoneMutable(sprint, now);
        EnsureMilestoneTarget(request.TargetValue, sprint.TargetValue);
        var milestone = FindMilestone(sprintId, milestoneId);
        milestone.Update(
            request.Title,
            request.Description,
            request.TargetValue,
            sprint.TargetValue);
        sprint.RecordActivity(now);
        await db.SaveChangesAsync(cancellationToken);
        return MapMilestone(milestone, actorId);
    }

    public async Task DeleteMilestoneAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, actorId);
        EnsureManager(clubId, actorId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        EnsureMilestoneMutable(sprint, now);
        var milestone = FindMilestone(sprintId, milestoneId);
        milestone.SoftDelete();
        sprint.RecordActivity(now);
        await db.SaveChangesAsync(cancellationToken);
    }

    public PageResult<ReadingSprintMilestoneResponseDto> GetMilestoneResponses(
        Guid clubId,
        Guid sprintId,
        Guid milestoneId,
        Guid? viewerId,
        int page,
        int pageSize)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, viewerId);
        var sprint = FindSprint(clubId, sprintId);
        FindMilestone(sprintId, milestoneId);
        var query = db.ClubReadingSprintMilestoneResponses
            .Where(x => x.MilestoneId == milestoneId && x.DeletedAt == null);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var total = query.LongCount();
        var items = query
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(size)
            .ToList()
            .Select(x => MapMilestoneResponse(x, clubId, sprint, viewerId))
            .ToList();
        return PageResult<ReadingSprintMilestoneResponseDto>.Create(
            items,
            normalizedPage,
            size,
            total);
    }

    public async Task<ReadingSprintMilestoneResponseDto> AddMilestoneResponseAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        Guid milestoneId,
        CreateReadingSprintMilestoneResponseRequest request,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, actorId);
        EnsureActiveClubMember(clubId, actorId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        EnsureActive(sprint, now);
        EnsureDiscussionParticipant(sprintId, actorId);
        FindMilestone(sprintId, milestoneId);
        var response = new ClubReadingSprintMilestoneResponse(
            milestoneId,
            actorId,
            request.Content,
            now);
        db.Add(response);
        sprint.RecordActivity(now);
        await db.SaveChangesAsync(cancellationToken);
        return MapMilestoneResponse(response, clubId, sprint, actorId);
    }

    public async Task DeleteMilestoneResponseAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        Guid responseId,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, actorId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        EnsureActive(sprint, now);
        var response = db.ClubReadingSprintMilestoneResponses.FirstOrDefault(x =>
                           x.Id == responseId &&
                           x.DeletedAt == null)
                       ?? throw ServiceErrors.NotFound(
                           "READING_SPRINT_RESPONSE_NOT_FOUND",
                           "Không tìm thấy phản hồi thảo luận.");
        var milestoneExists = db.ClubReadingSprintMilestones.Any(x =>
            x.Id == response.MilestoneId &&
            x.SprintId == sprintId);
        if (!milestoneExists)
        {
            throw ServiceErrors.NotFound(
                "READING_SPRINT_RESPONSE_NOT_FOUND",
                "Không tìm thấy phản hồi thảo luận.");
        }

        if (response.AuthorId != actorId && !IsManager(clubId, actorId))
        {
            throw ServiceErrors.Forbidden(
                "READING_SPRINT_RESPONSE_DELETE_FORBIDDEN",
                "Bạn không có quyền xóa phản hồi thảo luận này.");
        }

        response.SoftDelete();
        sprint.RecordActivity(now);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReadingSprintDetailDto> SendReminderAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, actorId);
        EnsureManager(clubId, actorId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        EnsureActive(sprint, now);
        if (!sprint.MarkReminderSent(now))
        {
            return MapDetail(sprint, actorId, now);
        }

        AddParticipantNotifications(
            clubId,
            sprintId,
            actorId,
            onlyIncomplete: true,
            sprint.TargetValue,
            "Nhắc tiến độ đợt đọc",
            $"Hãy tiếp tục tiến độ của bạn trong “{sprint.Title}”.",
            SprintLink(clubId, sprintId));
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(sprint, actorId, now);
    }

    public async Task<ReadingSprintDetailDto> CompleteAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, actorId);
        EnsureManager(clubId, actorId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        if (sprint.CompletedAt.HasValue)
        {
            return MapDetail(sprint, actorId, now);
        }

        if (sprint.CancelledAt.HasValue)
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_ALREADY_CANCELLED",
                "Đợt đọc đã bị hủy nên không thể hoàn thành.");
        }

        if (sprint.GetStatus(now) == ReadingSprintStatus.PLANNED)
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_NOT_STARTED",
                "Đợt đọc chưa bắt đầu nên chưa thể hoàn thành.");
        }

        sprint.Complete(now);
        AddParticipantNotifications(
            clubId,
            sprintId,
            actorId,
            onlyIncomplete: false,
            sprint.TargetValue,
            "Đợt đọc đã hoàn thành",
            $"Đợt đọc “{sprint.Title}” đã được tổng kết.",
            SprintLink(clubId, sprintId));
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(sprint, actorId, now);
    }

    public async Task<ReadingSprintDetailDto> CancelAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        var club = FindClub(clubId);
        EnsureCanView(club, actorId);
        EnsureManager(clubId, actorId);
        var sprint = FindSprint(clubId, sprintId);
        var now = timeProvider.GetUtcNow();
        if (sprint.CancelledAt.HasValue)
        {
            return MapDetail(sprint, actorId, now);
        }

        if (sprint.CompletedAt.HasValue)
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_ALREADY_COMPLETED",
                "Đợt đọc đã hoàn thành nên không thể hủy.");
        }

        sprint.Cancel(now);
        AddParticipantNotifications(
            clubId,
            sprintId,
            actorId,
            onlyIncomplete: false,
            sprint.TargetValue,
            "Đợt đọc đã bị hủy",
            $"Đợt đọc “{sprint.Title}” đã được hủy.",
            SprintLink(clubId, sprintId));
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(sprint, actorId, now);
    }

    private ReadingSprintDetailDto MapDetail(
        ClubReadingSprint sprint,
        Guid? viewerId,
        DateTimeOffset now)
    {
        var summary = MapSummary(sprint, viewerId, now);
        var milestones = db.ClubReadingSprintMilestones
            .Where(x => x.SprintId == sprint.Id && x.DeletedAt == null)
            .OrderBy(x => x.TargetValue)
            .ThenBy(x => x.CreatedAt)
            .ToList()
            .Select(x => MapMilestone(x, viewerId))
            .ToList();
        return new ReadingSprintDetailDto(
            summary.Id,
            summary.ClubId,
            summary.Title,
            summary.Description,
            summary.Book,
            summary.StartsAt,
            summary.EndsAt,
            summary.TargetUnit,
            summary.TargetValue,
            summary.Status,
            summary.ParticipantCount,
            summary.CompletedCount,
            summary.AverageProgressPercent,
            summary.ViewerParticipation,
            summary.Permissions,
            summary.CreatedBy,
            summary.CompletedAt,
            summary.CancelledAt,
            summary.LastReminderAt,
            summary.CreatedAt,
            milestones);
    }

    private ReadingSprintSummaryDto MapSummary(
        ClubReadingSprint sprint,
        Guid? viewerId,
        DateTimeOffset now)
    {
        var book = db.Books.FirstOrDefault(x => x.Id == sprint.BookId)
                   ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
        var participants = ActiveParticipants(sprint.Id).ToList();
        var viewerParticipation = viewerId.HasValue
            ? db.ClubReadingSprintParticipants.FirstOrDefault(x =>
                x.SprintId == sprint.Id &&
                x.UserId == viewerId.Value)
            : null;
        var status = sprint.GetStatus(now);
        var isClubMember = viewerId.HasValue && HasActiveClubMembership(sprint.ClubId, viewerId.Value);
        var isManager = viewerId.HasValue && IsManager(sprint.ClubId, viewerId.Value);
        var activeViewerParticipation = viewerParticipation?.IsActive == true;
        var participationMutable = status is ReadingSprintStatus.PLANNED or ReadingSprintStatus.ACTIVE;
        var averageProgress = participants.Count == 0
            ? 0
            : (int)Math.Round(
                participants.Average(x => x.ProgressValue * 100d / sprint.TargetValue),
                MidpointRounding.AwayFromZero);
        var completedCount = participants.Count(x => x.ProgressValue >= sprint.TargetValue);
        return new ReadingSprintSummaryDto(
            sprint.Id,
            sprint.ClubId,
            sprint.Title,
            sprint.Description,
            _mapper.Book(book, viewerId),
            sprint.StartsAt,
            sprint.EndsAt,
            sprint.TargetUnit,
            sprint.TargetValue,
            status,
            participants.Count,
            completedCount,
            Math.Clamp(averageProgress, 0, 100),
            viewerParticipation is null
                ? null
                : MapParticipant(
                    viewerParticipation,
                    sprint.TargetValue,
                    RankFor(viewerParticipation, sprint.Id)),
            new ReadingSprintPermissionsDto(
                isManager && status is not ReadingSprintStatus.COMPLETED and not ReadingSprintStatus.CANCELLED,
                isClubMember && !activeViewerParticipation && participationMutable,
                isClubMember && activeViewerParticipation && participationMutable,
                isClubMember &&
                activeViewerParticipation &&
                status == ReadingSprintStatus.ACTIVE &&
                viewerParticipation!.ProgressValue < sprint.TargetValue,
                isClubMember && activeViewerParticipation && status == ReadingSprintStatus.ACTIVE,
                isManager &&
                status == ReadingSprintStatus.ACTIVE &&
                sprint.LastReminderAt?.UtcDateTime.Date != now.UtcDateTime.Date),
            _mapper.User(sprint.CreatedById),
            sprint.CompletedAt,
            sprint.CancelledAt,
            sprint.LastReminderAt,
            sprint.CreatedAt);
    }

    private ReadingSprintParticipantDto MapParticipant(
        ClubReadingSprintParticipant participant,
        int targetValue,
        int rank) =>
        new(
            participant.Id,
            _mapper.User(participant.UserId),
            participant.ProgressValue,
            Math.Clamp(
                (int)Math.Round(
                    participant.ProgressValue * 100d / targetValue,
                    MidpointRounding.AwayFromZero),
                0,
                100),
            participant.IsActive ? rank : 0,
            participant.JoinedAt,
            participant.LeftAt,
            participant.CompletedAt,
            participant.LastCheckInAt,
            participant.IsActive);

    private ReadingSprintCheckInDto MapCheckIn(
        ClubReadingSprintCheckIn checkIn,
        int targetValue) =>
        new(
            checkIn.Id,
            _mapper.User(checkIn.UserId),
            checkIn.ProgressValue,
            Math.Clamp(
                (int)Math.Round(
                    checkIn.ProgressValue * 100d / targetValue,
                    MidpointRounding.AwayFromZero),
                0,
                100),
            checkIn.Note,
            checkIn.CreatedAt);

    private ReadingSprintMilestoneDto MapMilestone(
        ClubReadingSprintMilestone milestone,
        Guid? viewerId)
    {
        var viewerProgress = viewerId.HasValue
            ? db.ClubReadingSprintParticipants
                .Where(x =>
                    x.SprintId == milestone.SprintId &&
                    x.UserId == viewerId.Value &&
                    x.LeftAt == null)
                .Select(x => (int?)x.ProgressValue)
                .FirstOrDefault()
            : null;
        return new ReadingSprintMilestoneDto(
            milestone.Id,
            milestone.Title,
            milestone.Description,
            milestone.TargetValue,
            viewerProgress.HasValue && viewerProgress.Value >= milestone.TargetValue,
            db.ClubReadingSprintMilestoneResponses.Count(x =>
                x.MilestoneId == milestone.Id &&
                x.DeletedAt == null),
            milestone.CreatedAt);
    }

    private ReadingSprintMilestoneResponseDto MapMilestoneResponse(
        ClubReadingSprintMilestoneResponse response,
        Guid clubId,
        ClubReadingSprint sprint,
        Guid? viewerId) =>
        new(
            response.Id,
            response.MilestoneId,
            _mapper.User(response.AuthorId),
            response.Content,
            sprint.GetStatus(timeProvider.GetUtcNow()) == ReadingSprintStatus.ACTIVE &&
            viewerId.HasValue &&
            (response.AuthorId == viewerId.Value || IsManager(clubId, viewerId.Value)),
            response.CreatedAt);

    private BookClub FindClub(Guid clubId) =>
        db.BookClubs.FirstOrDefault(x => x.Id == clubId)
        ?? throw ServiceErrors.NotFound(
            "CLUB_NOT_FOUND",
            "Không tìm thấy câu lạc bộ.");

    private ClubReadingSprint FindSprint(Guid clubId, Guid sprintId) =>
        db.ClubReadingSprints.FirstOrDefault(x =>
            x.Id == sprintId &&
            x.ClubId == clubId)
        ?? throw ServiceErrors.NotFound(
            "READING_SPRINT_NOT_FOUND",
            "Không tìm thấy đợt đọc của câu lạc bộ.");

    private ClubReadingSprintParticipant FindParticipant(Guid sprintId, Guid userId) =>
        db.ClubReadingSprintParticipants.FirstOrDefault(x =>
            x.SprintId == sprintId &&
            x.UserId == userId)
        ?? throw ServiceErrors.NotFound(
            "READING_SPRINT_PARTICIPANT_NOT_FOUND",
            "Bạn chưa tham gia đợt đọc này.");

    private ClubReadingSprintParticipant FindActiveParticipant(Guid sprintId, Guid userId)
    {
        var participant = FindParticipant(sprintId, userId);
        if (!participant.IsActive)
        {
            throw ServiceErrors.Forbidden(
                "READING_SPRINT_PARTICIPATION_INACTIVE",
                "Bạn cần tham gia lại đợt đọc trước khi cập nhật tiến độ.");
        }

        return participant;
    }

    private void EnsureDiscussionParticipant(Guid sprintId, Guid userId)
    {
        var participant = db.ClubReadingSprintParticipants.FirstOrDefault(x =>
            x.SprintId == sprintId &&
            x.UserId == userId);
        if (participant?.IsActive != true)
        {
            throw ServiceErrors.Forbidden(
                "READING_SPRINT_PARTICIPATION_REQUIRED",
                "Bạn cần tham gia đợt đọc để thảo luận tại các cột mốc.");
        }
    }

    private ClubReadingSprintMilestone FindMilestone(Guid sprintId, Guid milestoneId) =>
        db.ClubReadingSprintMilestones.FirstOrDefault(x =>
            x.Id == milestoneId &&
            x.SprintId == sprintId &&
            x.DeletedAt == null)
        ?? throw ServiceErrors.NotFound(
            "READING_SPRINT_MILESTONE_NOT_FOUND",
            "Không tìm thấy cột mốc của đợt đọc.");

    private Book FindAndValidateBook(
        Guid bookId,
        ReadingSprintTargetUnit targetUnit,
        int targetValue)
    {
        if (bookId == Guid.Empty)
        {
            throw ServiceErrors.BadRequest("INVALID_BOOK_ID", "Mã sách không hợp lệ.");
        }

        if (!Enum.IsDefined(targetUnit))
        {
            throw ServiceErrors.BadRequest(
                "INVALID_READING_SPRINT_TARGET_UNIT",
                "Đơn vị mục tiêu của đợt đọc không hợp lệ.");
        }

        var book = db.Books.FirstOrDefault(x => x.Id == bookId)
                   ?? throw ServiceErrors.NotFound("BOOK_NOT_FOUND", "Không tìm thấy sách.");
        if (targetValue <= 0)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_READING_SPRINT_TARGET",
                "Mục tiêu đợt đọc phải lớn hơn 0.");
        }

        if (targetUnit == ReadingSprintTargetUnit.PAGES && targetValue > book.PageCount)
        {
            throw ServiceErrors.BadRequest(
                "READING_SPRINT_TARGET_EXCEEDS_BOOK_PAGES",
                $"Mục tiêu số trang không được vượt quá {book.PageCount} trang của sách.");
        }

        if (targetUnit == ReadingSprintTargetUnit.CHAPTERS && targetValue > 500)
        {
            throw ServiceErrors.BadRequest(
                "READING_SPRINT_CHAPTER_TARGET_TOO_LARGE",
                "Mục tiêu số chương không được vượt quá 500.");
        }

        return book;
    }

    private static void EnsurePeriodCanBeCreated(
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset now)
    {
        if (endsAt.ToUniversalTime() <= startsAt.ToUniversalTime())
        {
            throw ServiceErrors.BadRequest(
                "INVALID_READING_SPRINT_PERIOD",
                "Thời điểm kết thúc phải ở sau thời điểm bắt đầu.");
        }

        if (endsAt.ToUniversalTime() <= now.ToUniversalTime())
        {
            throw ServiceErrors.BadRequest(
                "READING_SPRINT_END_MUST_BE_FUTURE",
                "Thời điểm kết thúc đợt đọc phải ở tương lai.");
        }
    }

    private static void EnsureMilestoneTarget(int targetValue, int sprintTargetValue)
    {
        if (targetValue <= 0 || targetValue > sprintTargetValue)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_READING_SPRINT_MILESTONE_TARGET",
                $"Mốc tiến độ phải từ 1 đến {sprintTargetValue}.");
        }
    }

    private void EnsureCanView(BookClub club, Guid? viewerId)
    {
        if (club.Visibility == ClubVisibility.PRIVATE &&
            (!viewerId.HasValue || !HasActiveClubMembership(club.Id, viewerId.Value)))
        {
            throw ServiceErrors.NotFound(
                "CLUB_NOT_FOUND",
                "Không tìm thấy câu lạc bộ.");
        }
    }

    private ClubMemberRole EnsureManager(Guid clubId, Guid actorId)
    {
        var role = db.BookClubMembers
            .Where(x =>
                x.ClubId == clubId &&
                x.UserId == actorId &&
                x.DeletedAt == null)
            .Select(x => (ClubMemberRole?)x.Role)
            .FirstOrDefault();
        if (role is not ClubMemberRole.OWNER and not ClubMemberRole.MODERATOR)
        {
            throw ServiceErrors.Forbidden(
                "CLUB_MANAGEMENT_FORBIDDEN",
                "Bạn không có quyền quản lý đợt đọc của câu lạc bộ.");
        }

        return role.Value;
    }

    private void EnsureActiveClubMember(Guid clubId, Guid userId)
    {
        if (!HasActiveClubMembership(clubId, userId))
        {
            throw ServiceErrors.Forbidden(
                "CLUB_MEMBERSHIP_REQUIRED",
                "Bạn cần là thành viên câu lạc bộ để thực hiện thao tác này.");
        }
    }

    private BookClubMember FindActiveClubMembership(Guid clubId, Guid userId) =>
        db.BookClubMembers.FirstOrDefault(x =>
            x.ClubId == clubId &&
            x.UserId == userId &&
            x.DeletedAt == null)
        ?? throw ServiceErrors.Forbidden(
            "CLUB_MEMBERSHIP_REQUIRED",
            "Bạn cần là thành viên câu lạc bộ để thực hiện thao tác này.");

    private bool HasActiveClubMembership(Guid clubId, Guid userId) =>
        db.BookClubMembers.Any(x =>
            x.ClubId == clubId &&
            x.UserId == userId &&
            x.DeletedAt == null);

    private bool IsManager(Guid clubId, Guid userId) =>
        db.BookClubMembers.Any(x =>
            x.ClubId == clubId &&
            x.UserId == userId &&
            x.DeletedAt == null &&
            (x.Role == ClubMemberRole.OWNER || x.Role == ClubMemberRole.MODERATOR));

    private static void EnsureParticipationMutable(
        ClubReadingSprint sprint,
        DateTimeOffset now)
    {
        if (sprint.GetStatus(now) is not ReadingSprintStatus.PLANNED and not ReadingSprintStatus.ACTIVE)
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_PARTICIPATION_NOT_ALLOWED",
                "Không thể thay đổi người tham gia ở trạng thái hiện tại của đợt đọc.");
        }
    }

    private static void EnsureActive(ClubReadingSprint sprint, DateTimeOffset now)
    {
        if (sprint.GetStatus(now) != ReadingSprintStatus.ACTIVE)
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_NOT_ACTIVE",
                "Thao tác này chỉ thực hiện được khi đợt đọc đang diễn ra.");
        }
    }

    private static void EnsureMilestoneMutable(
        ClubReadingSprint sprint,
        DateTimeOffset now)
    {
        if (sprint.GetStatus(now) is not ReadingSprintStatus.PLANNED and not ReadingSprintStatus.ACTIVE)
        {
            throw ServiceErrors.Conflict(
                "READING_SPRINT_MILESTONE_MUTATION_NOT_ALLOWED",
                "Không thể thay đổi cột mốc ở trạng thái hiện tại của đợt đọc.");
        }
    }

    private IQueryable<ClubReadingSprintParticipant> ActiveParticipants(Guid sprintId) =>
        db.ClubReadingSprintParticipants.Where(x =>
            x.SprintId == sprintId &&
            x.LeftAt == null);

    private int RankFor(ClubReadingSprintParticipant participant, Guid sprintId)
    {
        if (!participant.IsActive)
        {
            return 0;
        }

        var rankedIds = ActiveParticipants(sprintId)
            .OrderByDescending(x => x.ProgressValue)
            .ThenBy(x => x.CompletedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.LastCheckInAt ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.JoinedAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();
        var index = rankedIds.IndexOf(participant.Id);
        return index < 0 ? 0 : index + 1;
    }

    private void AddClubMemberNotifications(
        Guid clubId,
        Guid actorId,
        string title,
        string message,
        string link)
    {
        var recipientIds = db.BookClubMembers
            .Where(x =>
                x.ClubId == clubId &&
                x.UserId != actorId &&
                x.DeletedAt == null)
            .Select(x => x.UserId)
            .Distinct()
            .ToList();
        NotificationDelivery.AddRangeIfEnabled(
            db,
            recipientIds.Select(userId =>
                new Notification(userId, NotificationType.CLUB, title, message, link)),
            actorId);
    }

    private void AddParticipantNotifications(
        Guid clubId,
        Guid sprintId,
        Guid actorId,
        bool onlyIncomplete,
        int targetValue,
        string title,
        string message,
        string link)
    {
        var activeMemberIds = db.BookClubMembers
            .Where(x => x.ClubId == clubId && x.DeletedAt == null)
            .Select(x => x.UserId);
        var recipientIds = db.ClubReadingSprintParticipants
            .Where(x =>
                x.SprintId == sprintId &&
                x.LeftAt == null &&
                x.UserId != actorId &&
                activeMemberIds.Contains(x.UserId) &&
                (!onlyIncomplete || x.ProgressValue < targetValue))
            .Select(x => x.UserId)
            .Distinct()
            .ToList();
        NotificationDelivery.AddRangeIfEnabled(
            db,
            recipientIds.Select(userId =>
                new Notification(userId, NotificationType.CLUB, title, message, link)),
            actorId);
    }

    private static string SprintLink(Guid clubId, Guid sprintId) =>
        $"/clubs/{clubId}/sprints/{sprintId}";
}
