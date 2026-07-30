using BookSpace.Api.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Authorize]
[Route("api/insights")]
public sealed class ReadingInsightsController(
    IReadingInsightsService readingInsightsService) : ApiControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<ReadingInsightsOverviewDto>>> GetOverview(
        [FromQuery] int days = 30,
        [FromQuery] int utcOffsetMinutes = 0,
        CancellationToken cancellationToken = default) =>
        OkData(await readingInsightsService.GetOverviewAsync(
            CurrentUserId,
            days,
            utcOffsetMinutes,
            cancellationToken));

    [HttpGet("calendar")]
    public async Task<ActionResult<ApiResponse<ReadingCalendarDto>>> GetCalendar(
        [FromQuery] int? year = null,
        [FromQuery] int days = 365,
        [FromQuery] int utcOffsetMinutes = 0,
        CancellationToken cancellationToken = default) =>
        OkData(await readingInsightsService.GetCalendarAsync(
            CurrentUserId,
            year,
            days,
            utcOffsetMinutes,
            cancellationToken));

    [HttpGet("weekly")]
    public async Task<ActionResult<ApiResponse<ReadingWeeklyInsightsDto>>> GetWeekly(
        [FromQuery] int weeks = 12,
        [FromQuery] int utcOffsetMinutes = 0,
        CancellationToken cancellationToken = default) =>
        OkData(await readingInsightsService.GetWeeklyAsync(
            CurrentUserId,
            weeks,
            utcOffsetMinutes,
            cancellationToken));

    [HttpGet("monthly")]
    public async Task<ActionResult<ApiResponse<ReadingMonthlyInsightsDto>>> GetMonthly(
        [FromQuery] int months = 12,
        [FromQuery] int utcOffsetMinutes = 0,
        CancellationToken cancellationToken = default) =>
        OkData(await readingInsightsService.GetMonthlyAsync(
            CurrentUserId,
            months,
            utcOffsetMinutes,
            cancellationToken));
}
