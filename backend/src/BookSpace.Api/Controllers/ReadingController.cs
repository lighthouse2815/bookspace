using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Authorize]
[Route("api")]
public sealed class ReadingController(IReadingService readingService) : ApiControllerBase
{
    [HttpGet("library")]
    public ActionResult<ApiResponse<PageResult<LibraryItemDto>>> Library(
        [FromQuery] LibraryStatus? shelf,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(readingService.GetLibrary(CurrentUserId, shelf, page, pageSize));

    [HttpPost("library")]
    public async Task<ActionResult<ApiResponse<LibraryItemDto>>> AddLibraryItem(
        AddLibraryItemRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await readingService.AddLibraryItemAsync(CurrentUserId, request, cancellationToken),
            "Đã thêm sách vào thư viện.");

    [HttpPatch("library/{itemId:guid}")]
    public async Task<ActionResult<ApiResponse<LibraryItemDto>>> UpdateLibraryItem(
        Guid itemId,
        UpdateLibraryItemRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await readingService.UpdateLibraryItemAsync(CurrentUserId, itemId, request, cancellationToken),
            "Cập nhật thư viện thành công.");

    [HttpPatch("library/{itemId:guid}/progress")]
    public async Task<ActionResult<ApiResponse<LibraryItemDto>>> UpdateProgress(
        Guid itemId,
        UpdateProgressRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await readingService.UpdateProgressAsync(CurrentUserId, itemId, request, cancellationToken),
            "Cập nhật tiến độ đọc thành công.");

    [HttpDelete("library/{itemId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> RemoveLibraryItem(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        await readingService.RemoveLibraryItemAsync(CurrentUserId, itemId, cancellationToken);
        return OkEmptyData("Đã xóa sách khỏi thư viện.");
    }

    [HttpGet("reading-sessions")]
    public ActionResult<ApiResponse<PageResult<ReadingSessionDto>>> Sessions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(readingService.GetSessions(CurrentUserId, page, pageSize));

    [HttpPost("reading-sessions")]
    public async Task<ActionResult<ApiResponse<ReadingSessionDto>>> AddSession(
        CreateReadingSessionRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await readingService.AddSessionAsync(CurrentUserId, request, cancellationToken),
            "Đã ghi nhận phiên đọc.");

    [HttpPatch("reading-sessions/{sessionId:guid}")]
    public async Task<ActionResult<ApiResponse<ReadingSessionDto>>> CorrectSession(
        Guid sessionId,
        CorrectReadingSessionRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await readingService.CorrectSessionAsync(
                CurrentUserId,
                sessionId,
                request,
                cancellationToken),
            "Đã sửa phiên đọc.");

    [HttpGet("reading-sessions/active")]
    public ActionResult<ApiResponse<ActiveReadingSessionDto?>> ActiveSession() =>
        OkData(readingService.GetActiveSession(CurrentUserId));

    [HttpPost("reading-sessions/active")]
    public async Task<ActionResult<ApiResponse<ActiveReadingSessionDto>>> StartActiveSession(
        StartActiveReadingSessionRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await readingService.StartActiveSessionAsync(
                CurrentUserId,
                request,
                cancellationToken),
            "Đã bắt đầu phiên đọc tập trung.");

    [HttpPost("reading-sessions/active/pause")]
    public async Task<ActionResult<ApiResponse<ActiveReadingSessionDto>>> PauseActiveSession(
        CancellationToken cancellationToken) =>
        OkData(
            await readingService.PauseActiveSessionAsync(CurrentUserId, cancellationToken),
            "Đã tạm dừng phiên đọc tập trung.");

    [HttpPost("reading-sessions/active/resume")]
    public async Task<ActionResult<ApiResponse<ActiveReadingSessionDto>>> ResumeActiveSession(
        CancellationToken cancellationToken) =>
        OkData(
            await readingService.ResumeActiveSessionAsync(CurrentUserId, cancellationToken),
            "Đã tiếp tục phiên đọc tập trung.");

    [HttpPost("reading-sessions/active/finish")]
    public async Task<ActionResult<ApiResponse<ReadingSessionDto>>> FinishActiveSession(
        FinishActiveReadingSessionRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await readingService.FinishActiveSessionAsync(
                CurrentUserId,
                request,
                cancellationToken),
            "Đã hoàn tất phiên đọc tập trung.");

    [HttpDelete("reading-sessions/active")]
    public async Task<ActionResult<ApiResponse<object?>>> CancelActiveSession(
        CancellationToken cancellationToken)
    {
        await readingService.CancelActiveSessionAsync(CurrentUserId, cancellationToken);
        return OkEmptyData("Đã hủy phiên đọc tập trung.");
    }
}
