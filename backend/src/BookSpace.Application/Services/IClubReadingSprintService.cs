using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public interface IClubReadingSprintService
{
    PageResult<ReadingSprintSummaryDto> GetSprints(
        Guid clubId,
        Guid? viewerId,
        ReadingSprintStatus? status,
        int page,
        int pageSize);

    ReadingSprintDetailDto GetSprint(Guid clubId, Guid sprintId, Guid? viewerId);

    Task<ReadingSprintDetailDto> CreateAsync(
        Guid actorId,
        Guid clubId,
        SaveReadingSprintRequest request,
        CancellationToken cancellationToken);

    Task<ReadingSprintDetailDto> UpdateAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        SaveReadingSprintRequest request,
        CancellationToken cancellationToken);

    Task<ReadingSprintParticipantDto> JoinAsync(
        Guid userId,
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken);

    Task<ReadingSprintParticipantDto> LeaveAsync(
        Guid userId,
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken);

    Task<ReadingSprintParticipantDto> UpdateProgressAsync(
        Guid userId,
        Guid clubId,
        Guid sprintId,
        UpdateReadingSprintProgressRequest request,
        CancellationToken cancellationToken);

    PageResult<ReadingSprintParticipantDto> GetLeaderboard(
        Guid clubId,
        Guid sprintId,
        Guid? viewerId,
        int page,
        int pageSize);

    PageResult<ReadingSprintCheckInDto> GetTimeline(
        Guid clubId,
        Guid sprintId,
        Guid? viewerId,
        int page,
        int pageSize);

    Task<ReadingSprintMilestoneDto> CreateMilestoneAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        SaveReadingSprintMilestoneRequest request,
        CancellationToken cancellationToken);

    Task<ReadingSprintMilestoneDto> UpdateMilestoneAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        Guid milestoneId,
        SaveReadingSprintMilestoneRequest request,
        CancellationToken cancellationToken);

    Task DeleteMilestoneAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        Guid milestoneId,
        CancellationToken cancellationToken);

    PageResult<ReadingSprintMilestoneResponseDto> GetMilestoneResponses(
        Guid clubId,
        Guid sprintId,
        Guid milestoneId,
        Guid? viewerId,
        int page,
        int pageSize);

    Task<ReadingSprintMilestoneResponseDto> AddMilestoneResponseAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        Guid milestoneId,
        CreateReadingSprintMilestoneResponseRequest request,
        CancellationToken cancellationToken);

    Task DeleteMilestoneResponseAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        Guid responseId,
        CancellationToken cancellationToken);

    Task<ReadingSprintDetailDto> SendReminderAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken);

    Task<ReadingSprintDetailDto> CompleteAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken);

    Task<ReadingSprintDetailDto> CancelAsync(
        Guid actorId,
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken);
}
