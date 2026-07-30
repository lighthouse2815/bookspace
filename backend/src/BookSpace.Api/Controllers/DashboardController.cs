using BookSpace.Api.Common;
using BookSpace.Application.Abstractions;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Route("api")]
public sealed class DashboardController(
    IDashboardService dashboardService,
    IExternalCatalogService externalCatalogService) : ApiControllerBase
{
    [Authorize]
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> Dashboard(
        CancellationToken cancellationToken) =>
        OkData(await dashboardService.GetAsync(CurrentUserId, cancellationToken));

    [AllowAnonymous]
    [HttpGet("external-books/search")]
    public async Task<ActionResult<ApiResponse<ExternalBookSearchResult>>> ExternalBooks(
        [FromQuery] string query,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default) =>
        OkData(await externalCatalogService.SearchAsync(query, limit, cancellationToken));
}
