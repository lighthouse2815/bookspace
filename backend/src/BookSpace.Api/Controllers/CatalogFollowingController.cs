using BookSpace.Api.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Authorize]
[Route("api/catalog-follows")]
public sealed class CatalogFollowingController(
    ICatalogFollowingService catalogFollowingService) : ApiControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<CatalogFollowingDto>> Mine() =>
        OkData(catalogFollowingService.GetMine(CurrentUserId));

    [HttpPut("authors/{authorId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> FollowAuthor(
        Guid authorId,
        CancellationToken cancellationToken)
    {
        await catalogFollowingService.FollowAuthorAsync(CurrentUserId, authorId, cancellationToken);
        return OkEmptyData("Đã theo dõi tác giả.");
    }

    [HttpDelete("authors/{authorId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> UnfollowAuthor(
        Guid authorId,
        CancellationToken cancellationToken)
    {
        await catalogFollowingService.UnfollowAuthorAsync(CurrentUserId, authorId, cancellationToken);
        return OkEmptyData("Đã bỏ theo dõi tác giả.");
    }

    [HttpPut("categories/{categoryId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> FollowCategory(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        await catalogFollowingService.FollowCategoryAsync(CurrentUserId, categoryId, cancellationToken);
        return OkEmptyData("Đã theo dõi thể loại.");
    }

    [HttpDelete("categories/{categoryId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> UnfollowCategory(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        await catalogFollowingService.UnfollowCategoryAsync(CurrentUserId, categoryId, cancellationToken);
        return OkEmptyData("Đã bỏ theo dõi thể loại.");
    }
}
