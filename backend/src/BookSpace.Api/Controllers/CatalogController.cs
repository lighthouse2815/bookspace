using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Route("api")]
public sealed class CatalogController(ICatalogService catalogService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("books")]
    public ActionResult<ApiResponse<PageResult<BookSummary>>> Books(
        [FromQuery] string? search,
        [FromQuery] Guid? authorId,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(catalogService.GetBooks(search, authorId, categoryId, sort, OptionalUserId, page, pageSize));

    [Authorize]
    [HttpGet("books/recommendations")]
    public ActionResult<ApiResponse<PageResult<BookRecommendationDto>>> Recommendations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12) =>
        OkData(catalogService.GetRecommendations(CurrentUserId, page, pageSize));

    [AllowAnonymous]
    [HttpGet("books/{id:guid}")]
    public ActionResult<ApiResponse<BookDetail>> Book(Guid id) =>
        OkData(catalogService.GetBook(id, OptionalUserId));

    [AllowAnonymous]
    [HttpGet("authors")]
    public ActionResult<ApiResponse<PageResult<AuthorDto>>> Authors(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100) =>
        OkData(catalogService.GetAuthors(page, pageSize));

    [AllowAnonymous]
    [HttpGet("categories")]
    public ActionResult<ApiResponse<PageResult<CategoryDto>>> Categories(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100) =>
        OkData(catalogService.GetCategories(page, pageSize));
}
