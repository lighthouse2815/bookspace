using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Route("api/clubs/{clubId:guid}/reading-sprints")]
public sealed class ClubReadingSprintsController(
    IClubReadingSprintService sprintService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public ActionResult<ApiResponse<PageResult<ReadingSprintSummaryDto>>> Sprints(
        Guid clubId,
        [FromQuery] ReadingSprintStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(sprintService.GetSprints(clubId, OptionalUserId, status, page, pageSize));

    [AllowAnonymous]
    [HttpGet("{sprintId:guid}")]
    public ActionResult<ApiResponse<ReadingSprintDetailDto>> Sprint(
        Guid clubId,
        Guid sprintId) =>
        OkData(sprintService.GetSprint(clubId, sprintId, OptionalUserId));

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReadingSprintDetailDto>>> Create(
        Guid clubId,
        SaveReadingSprintRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await sprintService.CreateAsync(
                CurrentUserId,
                clubId,
                request,
                cancellationToken),
            "Đã tạo đợt đọc chung.");

    [Authorize]
    [HttpPatch("{sprintId:guid}")]
    public async Task<ActionResult<ApiResponse<ReadingSprintDetailDto>>> Update(
        Guid clubId,
        Guid sprintId,
        SaveReadingSprintRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await sprintService.UpdateAsync(
                CurrentUserId,
                clubId,
                sprintId,
                request,
                cancellationToken),
            "Đã cập nhật đợt đọc chung.");

    [Authorize]
    [HttpPost("{sprintId:guid}/join")]
    public async Task<ActionResult<ApiResponse<ReadingSprintParticipantDto>>> Join(
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken) =>
        OkData(
            await sprintService.JoinAsync(
                CurrentUserId,
                clubId,
                sprintId,
                cancellationToken),
            "Đã tham gia đợt đọc.");

    [Authorize]
    [HttpDelete("{sprintId:guid}/join")]
    public async Task<ActionResult<ApiResponse<ReadingSprintParticipantDto>>> Leave(
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken) =>
        OkData(
            await sprintService.LeaveAsync(
                CurrentUserId,
                clubId,
                sprintId,
                cancellationToken),
            "Đã rời đợt đọc.");

    [Authorize]
    [HttpPut("{sprintId:guid}/progress")]
    public async Task<ActionResult<ApiResponse<ReadingSprintParticipantDto>>> UpdateProgress(
        Guid clubId,
        Guid sprintId,
        UpdateReadingSprintProgressRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await sprintService.UpdateProgressAsync(
                CurrentUserId,
                clubId,
                sprintId,
                request,
                cancellationToken),
            "Đã cập nhật tiến độ đợt đọc.");

    [AllowAnonymous]
    [HttpGet("{sprintId:guid}/leaderboard")]
    public ActionResult<ApiResponse<PageResult<ReadingSprintParticipantDto>>> Leaderboard(
        Guid clubId,
        Guid sprintId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(sprintService.GetLeaderboard(
            clubId,
            sprintId,
            OptionalUserId,
            page,
            pageSize));

    [AllowAnonymous]
    [HttpGet("{sprintId:guid}/timeline")]
    public ActionResult<ApiResponse<PageResult<ReadingSprintCheckInDto>>> Timeline(
        Guid clubId,
        Guid sprintId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(sprintService.GetTimeline(
            clubId,
            sprintId,
            OptionalUserId,
            page,
            pageSize));

    [Authorize]
    [HttpPost("{sprintId:guid}/milestones")]
    public async Task<ActionResult<ApiResponse<ReadingSprintMilestoneDto>>> CreateMilestone(
        Guid clubId,
        Guid sprintId,
        SaveReadingSprintMilestoneRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await sprintService.CreateMilestoneAsync(
                CurrentUserId,
                clubId,
                sprintId,
                request,
                cancellationToken),
            "Đã tạo cột mốc thảo luận.");

    [Authorize]
    [HttpPatch("{sprintId:guid}/milestones/{milestoneId:guid}")]
    public async Task<ActionResult<ApiResponse<ReadingSprintMilestoneDto>>> UpdateMilestone(
        Guid clubId,
        Guid sprintId,
        Guid milestoneId,
        SaveReadingSprintMilestoneRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await sprintService.UpdateMilestoneAsync(
                CurrentUserId,
                clubId,
                sprintId,
                milestoneId,
                request,
                cancellationToken),
            "Đã cập nhật cột mốc thảo luận.");

    [Authorize]
    [HttpDelete("{sprintId:guid}/milestones/{milestoneId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteMilestone(
        Guid clubId,
        Guid sprintId,
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        await sprintService.DeleteMilestoneAsync(
            CurrentUserId,
            clubId,
            sprintId,
            milestoneId,
            cancellationToken);
        return OkEmptyData("Đã xóa cột mốc thảo luận.");
    }

    [AllowAnonymous]
    [HttpGet("{sprintId:guid}/milestones/{milestoneId:guid}/responses")]
    public ActionResult<ApiResponse<PageResult<ReadingSprintMilestoneResponseDto>>>
        MilestoneResponses(
            Guid clubId,
            Guid sprintId,
            Guid milestoneId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20) =>
        OkData(sprintService.GetMilestoneResponses(
            clubId,
            sprintId,
            milestoneId,
            OptionalUserId,
            page,
            pageSize));

    [Authorize]
    [HttpPost("{sprintId:guid}/milestones/{milestoneId:guid}/responses")]
    public async Task<ActionResult<ApiResponse<ReadingSprintMilestoneResponseDto>>>
        AddMilestoneResponse(
            Guid clubId,
            Guid sprintId,
            Guid milestoneId,
            CreateReadingSprintMilestoneResponseRequest request,
            CancellationToken cancellationToken) =>
        CreatedData(
            await sprintService.AddMilestoneResponseAsync(
                CurrentUserId,
                clubId,
                sprintId,
                milestoneId,
                request,
                cancellationToken),
            "Đã thêm phản hồi thảo luận.");

    [Authorize]
    [HttpDelete("{sprintId:guid}/milestone-responses/{responseId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteMilestoneResponse(
        Guid clubId,
        Guid sprintId,
        Guid responseId,
        CancellationToken cancellationToken)
    {
        await sprintService.DeleteMilestoneResponseAsync(
            CurrentUserId,
            clubId,
            sprintId,
            responseId,
            cancellationToken);
        return OkEmptyData("Đã xóa phản hồi thảo luận.");
    }

    [Authorize]
    [HttpPost("{sprintId:guid}/reminders")]
    public async Task<ActionResult<ApiResponse<ReadingSprintDetailDto>>> SendReminder(
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken) =>
        OkData(
            await sprintService.SendReminderAsync(
                CurrentUserId,
                clubId,
                sprintId,
                cancellationToken),
            "Đã xử lý nhắc nhở tiến độ.");

    [Authorize]
    [HttpPost("{sprintId:guid}/complete")]
    public async Task<ActionResult<ApiResponse<ReadingSprintDetailDto>>> Complete(
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken) =>
        OkData(
            await sprintService.CompleteAsync(
                CurrentUserId,
                clubId,
                sprintId,
                cancellationToken),
            "Đã hoàn thành đợt đọc.");

    [Authorize]
    [HttpPost("{sprintId:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<ReadingSprintDetailDto>>> Cancel(
        Guid clubId,
        Guid sprintId,
        CancellationToken cancellationToken) =>
        OkData(
            await sprintService.CancelAsync(
                CurrentUserId,
                clubId,
                sprintId,
                cancellationToken),
            "Đã hủy đợt đọc.");
}
