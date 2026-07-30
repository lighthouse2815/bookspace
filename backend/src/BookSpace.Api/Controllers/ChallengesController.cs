using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Route("api/challenges")]
public sealed class ChallengesController(IChallengeService challengeService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public ActionResult<ApiResponse<PageResult<ChallengeDto>>> Challenges(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(challengeService.GetChallenges(OptionalUserId, page, pageSize));

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public ActionResult<ApiResponse<ChallengeDto>> Challenge(Guid id) =>
        OkData(challengeService.GetPublic(id, OptionalUserId));

    [Authorize]
    [HttpGet("mine")]
    [HttpGet("my")]
    public ActionResult<ApiResponse<PageResult<ChallengeDto>>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(challengeService.GetMine(CurrentUserId, page, pageSize));

    [Authorize]
    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<ApiResponse<ChallengeDto>>> Join(
        Guid id,
        CancellationToken cancellationToken)
    {
        await challengeService.JoinAsync(CurrentUserId, id, cancellationToken);
        return OkData(challengeService.GetPublic(id, CurrentUserId), "Tham gia thử thách thành công.");
    }

    [Authorize]
    [HttpDelete("{id:guid}/join")]
    public async Task<ActionResult<ApiResponse<ChallengeDto>>> Leave(
        Guid id,
        CancellationToken cancellationToken)
    {
        await challengeService.LeaveAsync(CurrentUserId, id, cancellationToken);
        return OkData(challengeService.GetPublic(id, CurrentUserId), "Đã rời thử thách.");
    }

    [Authorize]
    [HttpPatch("{id:guid}/progress")]
    public async Task<ActionResult<ApiResponse<ChallengeDto>>> Progress(
        Guid id,
        UpdateChallengeProgressRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await challengeService.UpdateProgressAsync(CurrentUserId, id, request, cancellationToken),
            "Cập nhật tiến độ thử thách thành công.");
}
