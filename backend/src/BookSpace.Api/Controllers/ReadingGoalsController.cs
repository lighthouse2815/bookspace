using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Authorize]
[Route("api/reading-goals")]
public sealed class ReadingGoalsController(IReadingGoalService readingGoalService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResult<ReadingGoalDto>>>> GetGoals(
        [FromQuery] ReadingGoalStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        OkData(
            await readingGoalService.GetGoalsAsync(
                CurrentUserId,
                status,
                page,
                pageSize,
                cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReadingGoalDto>>> GetGoal(
        Guid id,
        CancellationToken cancellationToken) =>
        OkData(await readingGoalService.GetGoalAsync(CurrentUserId, id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReadingGoalDto>>> Create(
        CreateReadingGoalRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await readingGoalService.CreateAsync(CurrentUserId, request, cancellationToken),
            "Đã tạo mục tiêu đọc.");

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReadingGoalDto>>> Update(
        Guid id,
        UpdateReadingGoalRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await readingGoalService.UpdateAsync(CurrentUserId, id, request, cancellationToken),
            "Đã cập nhật mục tiêu đọc.");

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await readingGoalService.DeleteAsync(CurrentUserId, id, cancellationToken);
        return OkEmptyData("Đã xóa mục tiêu đọc.");
    }
}
