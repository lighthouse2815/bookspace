using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Route("api/users")]
public sealed class UsersController(
    IUserService userService,
    IUserSafetyService userSafetyService,
    IReadingService readingService,
    ICommunityService communityService) : ApiControllerBase
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

    [Authorize]
    [HttpGet("me/safety")]
    public ActionResult<ApiResponse<PageResult<UserSafetyEntryDto>>> MySafetyList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(userSafetyService.GetMine(CurrentUserId, page, pageSize));

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
    [HttpPatch("me/privacy")]
    public async Task<ActionResult<ApiResponse<UserProfile>>> UpdatePrivacy(
        UpdateProfilePrivacyRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await userService.UpdatePrivacyAsync(CurrentUserId, request, cancellationToken),
            "Cập nhật quyền riêng tư hồ sơ thành công.");

    [AllowAnonymous]
    [HttpGet("{id:guid}/library")]
    public ActionResult<ApiResponse<PageResult<PublicLibraryItemDto>>> PublicLibrary(
        Guid id,
        [FromQuery] LibraryStatus? shelf,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12) =>
        OkData(readingService.GetPublicLibrary(id, OptionalUserId, shelf, page, pageSize));

    [AllowAnonymous]
    [HttpGet("{id:guid}/reviews")]
    public ActionResult<ApiResponse<PageResult<ReviewDto>>> Reviews(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10) =>
        OkData(communityService.GetUserReviews(id, OptionalUserId, page, pageSize));

    [AllowAnonymous]
    [HttpGet("{id:guid}/activity")]
    public ActionResult<ApiResponse<PageResult<FeedItem>>> Activity(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10) =>
        OkData(communityService.GetUserActivity(id, OptionalUserId, page, pageSize));

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

    [Authorize]
    [HttpPost("{id:guid}/block")]
    public async Task<ActionResult<ApiResponse<UserSafetyEntryDto>>> Block(
        Guid id,
        CancellationToken cancellationToken) =>
        OkData(
            await userSafetyService.BlockAsync(CurrentUserId, id, cancellationToken),
            "Đã chặn người dùng và gỡ kết nối theo dõi hai chiều.");

    [Authorize]
    [HttpDelete("{id:guid}/block")]
    public async Task<ActionResult<ApiResponse<object?>>> Unblock(
        Guid id,
        CancellationToken cancellationToken)
    {
        await userSafetyService.UnblockAsync(CurrentUserId, id, cancellationToken);
        return OkEmptyData("Đã bỏ chặn người dùng.");
    }

    [Authorize]
    [HttpPost("{id:guid}/mute")]
    public async Task<ActionResult<ApiResponse<UserSafetyEntryDto>>> Mute(
        Guid id,
        CancellationToken cancellationToken) =>
        OkData(
            await userSafetyService.MuteAsync(CurrentUserId, id, cancellationToken),
            "Đã ẩn nội dung từ người dùng này.");

    [Authorize]
    [HttpDelete("{id:guid}/mute")]
    public async Task<ActionResult<ApiResponse<object?>>> Unmute(
        Guid id,
        CancellationToken cancellationToken)
    {
        await userSafetyService.UnmuteAsync(CurrentUserId, id, cancellationToken);
        return OkEmptyData("Đã hiển thị lại nội dung từ người dùng này.");
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/followers")]
    public ActionResult<ApiResponse<PageResult<UserSummary>>> Followers(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(userService.GetFollowers(id, OptionalUserId, page, pageSize));

    [AllowAnonymous]
    [HttpGet("{id:guid}/following")]
    public ActionResult<ApiResponse<PageResult<UserSummary>>> Following(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(userService.GetFollowing(id, OptionalUserId, page, pageSize));
}
