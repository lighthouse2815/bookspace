using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/admin")]
public sealed class AdminController(
    ICatalogService catalogService,
    IExternalCatalogService externalCatalogService,
    IChallengeService challengeService,
    IContentModerationService moderationService) : ApiControllerBase
{
    [HttpGet("authors")]
    public ActionResult<ApiResponse<PageResult<AuthorDto>>> Authors(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(catalogService.GetAdminAuthors(search, page, pageSize));

    [HttpGet("categories")]
    public ActionResult<ApiResponse<PageResult<CategoryDto>>> Categories(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(catalogService.GetAdminCategories(search, page, pageSize));

    [HttpPost("books")]
    public async Task<ActionResult<ApiResponse<BookDetail>>> CreateBook(
        SaveBookRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await catalogService.CreateBookAsync(request, cancellationToken),
            "Tạo sách thành công.");

    [HttpPatch("books/{id:guid}")]
    public async Task<ActionResult<ApiResponse<BookDetail>>> UpdateBook(
        Guid id,
        SaveBookRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await catalogService.UpdateBookAsync(id, request, cancellationToken),
            "Cập nhật sách thành công.");

    [HttpDelete("books/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteBook(
        Guid id,
        CancellationToken cancellationToken)
    {
        await catalogService.DeleteBookAsync(id, cancellationToken);
        return OkEmptyData("Đã xóa sách.");
    }

    [HttpPost("books/import")]
    public async Task<ActionResult<ApiResponse<ExternalBookImportResult>>> ImportBook(
        ImportExternalBookRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await externalCatalogService.ImportAsync(request, cancellationToken),
            "Đã xử lý import sách từ nguồn ngoài.");

    [HttpPost("authors")]
    public async Task<ActionResult<ApiResponse<AuthorDto>>> CreateAuthor(
        SaveAuthorRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await catalogService.CreateAuthorAsync(request, cancellationToken),
            "Tạo tác giả thành công.");

    [HttpPatch("authors/{id:guid}")]
    public async Task<ActionResult<ApiResponse<AuthorDto>>> UpdateAuthor(
        Guid id,
        SaveAuthorRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await catalogService.UpdateAuthorAsync(id, request, cancellationToken),
            "Cập nhật tác giả thành công.");

    [HttpDelete("authors/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteAuthor(
        Guid id,
        CancellationToken cancellationToken)
    {
        await catalogService.DeleteAuthorAsync(id, cancellationToken);
        return OkEmptyData("Đã xóa tác giả.");
    }

    [HttpPost("categories")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> CreateCategory(
        SaveCategoryRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await catalogService.CreateCategoryAsync(request, cancellationToken),
            "Tạo thể loại thành công.");

    [HttpPatch("categories/{id:guid}")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> UpdateCategory(
        Guid id,
        SaveCategoryRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await catalogService.UpdateCategoryAsync(id, request, cancellationToken),
            "Cập nhật thể loại thành công.");

    [HttpDelete("categories/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteCategory(
        Guid id,
        CancellationToken cancellationToken)
    {
        await catalogService.DeleteCategoryAsync(id, cancellationToken);
        return OkEmptyData("Đã xóa thể loại.");
    }

    [HttpPost("challenges")]
    public async Task<ActionResult<ApiResponse<ChallengeDto>>> CreateChallenge(
        SaveChallengeRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await challengeService.CreateAsync(CurrentUserId, request, cancellationToken),
            "Tạo thử thách thành công.");

    [HttpGet("challenges")]
    public ActionResult<ApiResponse<PageResult<ChallengeDto>>> Challenges(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50) =>
        OkData(challengeService.GetAdminChallenges(page, pageSize));

    [HttpPatch("challenges/{id:guid}")]
    public async Task<ActionResult<ApiResponse<ChallengeDto>>> UpdateChallenge(
        Guid id,
        SaveChallengeRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await challengeService.UpdateAsync(id, request, cancellationToken),
            "Cập nhật thử thách thành công.");

    [HttpPatch("challenges/{id:guid}/publish")]
    public async Task<ActionResult<ApiResponse<ChallengeDto>>> PublishChallenge(
        Guid id,
        PublishChallengeRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await challengeService.PublishAsync(id, request, cancellationToken),
            "Cập nhật trạng thái xuất bản thành công.");

    [HttpDelete("challenges/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteChallenge(
        Guid id,
        CancellationToken cancellationToken)
    {
        await challengeService.DeleteAsync(id, cancellationToken);
        return OkEmptyData("Đã xóa thử thách.");
    }

    [HttpGet("reports")]
    public ActionResult<ApiResponse<PageResult<ContentReportDto>>> Reports(
        [FromQuery] ContentReportStatus? status,
        [FromQuery] ContentReportTargetType? targetType,
        [FromQuery] ContentReportReason? reason,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(moderationService.GetReports(status, targetType, reason, page, pageSize));

    [HttpPatch("reports/{id:guid}/resolution")]
    public async Task<ActionResult<ApiResponse<ContentReportDto>>> ResolveReport(
        Guid id,
        ResolveContentReportRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await moderationService.ResolveReportAsync(
                CurrentUserId,
                id,
                request,
                cancellationToken),
            request.Status == ContentReportStatus.DISMISSED
                ? "Đã bác bỏ báo cáo."
                : "Đã xử lý báo cáo.");
}
