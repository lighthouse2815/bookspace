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
    public async Task<ActionResult<ApiResponse<PageResult<ChallengeDto>>>> Challenges(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        OkData(await challengeService.GetChallengesAsync(
            OptionalUserId,
            page,
            pageSize,
            cancellationToken));

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ChallengeDto>>> Challenge(
        Guid id,
        CancellationToken cancellationToken) =>
        OkData(await challengeService.GetPublicAsync(id, OptionalUserId, cancellationToken));

    [Authorize]
    [HttpGet("mine")]
    [HttpGet("my")]
    public async Task<ActionResult<ApiResponse<PageResult<ChallengeDto>>>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        OkData(await challengeService.GetMineAsync(
            CurrentUserId,
            page,
            pageSize,
            cancellationToken));

    [Authorize]
    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<ApiResponse<ChallengeDto>>> Join(
        Guid id,
        CancellationToken cancellationToken)
    {
        await challengeService.JoinAsync(CurrentUserId, id, cancellationToken);
        return OkData(
            await challengeService.GetPublicAsync(id, CurrentUserId, cancellationToken),
            "Tham gia thử thách thành công.");
    }

    [Authorize]
    [HttpDelete("{id:guid}/join")]
    public async Task<ActionResult<ApiResponse<ChallengeDto>>> Leave(
        Guid id,
        CancellationToken cancellationToken)
    {
        await challengeService.LeaveAsync(CurrentUserId, id, cancellationToken);
        return OkData(
            await challengeService.GetPublicAsync(id, CurrentUserId, cancellationToken),
            "Đã rời thử thách.");
    }
}
