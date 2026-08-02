using BookSpace.Api.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Authorize]
[Route("api/reports")]
public sealed class ContentReportsController(
    IContentModerationService moderationService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ContentReportDto>>> Create(
        CreateContentReportRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await moderationService.CreateReportAsync(
                CurrentUserId,
                request,
                cancellationToken),
            "Đã gửi báo cáo. Đội ngũ quản trị sẽ xem xét nội dung này.");
}
