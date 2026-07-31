using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Route("api/users")]
public sealed class UsersController(IUserService userService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResult<UserDiscoveryItem>>>> Search(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        OkData(
            await userService.SearchAsync(
                search,
                OptionalUserId,
                page,
                pageSize,
                cancellationToken));

    [Authorize]
    [HttpGet("suggestions")]
    public async Task<ActionResult<ApiResponse<PageResult<UserDiscoveryItem>>>> Suggestions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        OkData(
            await userService.GetSuggestionsAsync(
                CurrentUserId,
                page,
                pageSize,
                cancellationToken));

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public ActionResult<ApiResponse<UserProfile>> Get(Guid id) =>
        OkData(userService.Get(id, OptionalUserId));

    [Authorize]
    [HttpPatch("me")]
    public async Task<ActionResult<ApiResponse<UserProfile>>> UpdateMe(
        UpdateProfileRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await userService.UpdateAsync(CurrentUserId, request, cancellationToken),
            "Cập nhật hồ sơ thành công.");

    [Authorize]
    [HttpPost("{id:guid}/follow")]
    public async Task<ActionResult<ApiResponse<UserProfile>>> Follow(
        Guid id,
        CancellationToken cancellationToken)
    {
        await userService.FollowAsync(CurrentUserId, id, cancellationToken);
        return OkData(userService.Get(id, CurrentUserId), "Đã theo dõi người dùng.");
    }

    [Authorize]
    [HttpDelete("{id:guid}/follow")]
    public async Task<ActionResult<ApiResponse<UserProfile>>> Unfollow(
        Guid id,
        CancellationToken cancellationToken)
    {
        await userService.UnfollowAsync(CurrentUserId, id, cancellationToken);
        return OkData(userService.Get(id, CurrentUserId), "Đã bỏ theo dõi người dùng.");
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/followers")]
    public ActionResult<ApiResponse<PageResult<UserSummary>>> Followers(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(userService.GetFollowers(id, page, pageSize));

    [AllowAnonymous]
    [HttpGet("{id:guid}/following")]
    public ActionResult<ApiResponse<PageResult<UserSummary>>> Following(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(userService.GetFollowing(id, page, pageSize));
}
