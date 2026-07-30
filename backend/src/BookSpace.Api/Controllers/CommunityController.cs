using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Route("api")]
public sealed class CommunityController(ICommunityService communityService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("reviews")]
    public ActionResult<ApiResponse<PageResult<ReviewDto>>> Reviews(
        [FromQuery] Guid bookId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(communityService.GetBookReviews(bookId, OptionalUserId, page, pageSize));

    [AllowAnonymous]
    [HttpGet("books/{bookId:guid}/reviews")]
    public ActionResult<ApiResponse<PageResult<ReviewDto>>> BookReviews(
        Guid bookId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(communityService.GetBookReviews(bookId, OptionalUserId, page, pageSize));

    [Authorize]
    [HttpPost("reviews")]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> CreateReview(
        CreateReviewRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await communityService.CreateReviewAsync(CurrentUserId, request, cancellationToken),
            "Đăng đánh giá thành công.");

    [Authorize]
    [HttpPost("books/{bookId:guid}/reviews")]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> CreateBookReview(
        Guid bookId,
        SaveReviewRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await communityService.CreateReviewAsync(
                CurrentUserId,
                new CreateReviewRequest(bookId, request.Rating, request.Content, request.ContainsSpoilers),
                cancellationToken),
            "Đăng đánh giá thành công.");

    [Authorize]
    [HttpPut("reviews/{reviewId:guid}")]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> UpdateReview(
        Guid reviewId,
        SaveReviewRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await communityService.UpdateReviewAsync(
                CurrentUserId,
                IsAdmin,
                reviewId,
                request,
                cancellationToken),
            "Cập nhật đánh giá thành công.");

    [Authorize]
    [HttpDelete("reviews/{reviewId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteReview(
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        await communityService.DeleteReviewAsync(CurrentUserId, IsAdmin, reviewId, cancellationToken);
        return OkEmptyData("Đã xóa đánh giá.");
    }

    [Authorize]
    [HttpPost("reviews/{reviewId:guid}/like")]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> Like(
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        await communityService.LikeReviewAsync(CurrentUserId, reviewId, cancellationToken);
        return OkData(communityService.GetReview(reviewId, CurrentUserId), "Đã thích đánh giá.");
    }

    [Authorize]
    [HttpDelete("reviews/{reviewId:guid}/like")]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> Unlike(
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        await communityService.UnlikeReviewAsync(CurrentUserId, reviewId, cancellationToken);
        return OkData(communityService.GetReview(reviewId, CurrentUserId), "Đã bỏ thích đánh giá.");
    }

    [AllowAnonymous]
    [HttpGet("reviews/{reviewId:guid}/comments")]
    public ActionResult<ApiResponse<PageResult<ReviewCommentDto>>> Comments(
        Guid reviewId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50) =>
        OkData(communityService.GetComments(reviewId, page, pageSize));

    [Authorize]
    [HttpPost("reviews/{reviewId:guid}/comments")]
    public async Task<ActionResult<ApiResponse<ReviewCommentDto>>> AddComment(
        Guid reviewId,
        CreateCommentRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await communityService.AddCommentAsync(CurrentUserId, reviewId, request, cancellationToken),
            "Đã thêm bình luận.");

    [Authorize]
    [HttpDelete("review-comments/{commentId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteComment(
        Guid commentId,
        CancellationToken cancellationToken)
    {
        await communityService.DeleteCommentAsync(CurrentUserId, IsAdmin, commentId, cancellationToken);
        return OkEmptyData("Đã xóa bình luận.");
    }

    [Authorize]
    [HttpGet("feed")]
    public ActionResult<ApiResponse<PageResult<FeedItem>>> Feed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(communityService.GetFeed(CurrentUserId, page, pageSize));
}
