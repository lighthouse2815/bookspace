using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Authorize]
[Route("api/reading-notes")]
public sealed class ReadingNotesController(IReadingNoteService readingNoteService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PageResult<ReadingNoteDto>>>> GetNotes(
        [FromQuery] Guid? bookId,
        [FromQuery] string? tag,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        OkData(
            await readingNoteService.GetNotesAsync(
                CurrentUserId,
                bookId,
                tag,
                search,
                page,
                pageSize,
                cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReadingNoteDto>>> GetNote(
        Guid id,
        CancellationToken cancellationToken) =>
        OkData(await readingNoteService.GetNoteAsync(CurrentUserId, id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReadingNoteDto>>> Create(
        CreateReadingNoteRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await readingNoteService.CreateAsync(CurrentUserId, request, cancellationToken),
            "Đã lưu ghi chú đọc.");

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReadingNoteDto>>> Update(
        Guid id,
        UpdateReadingNoteRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await readingNoteService.UpdateAsync(CurrentUserId, id, request, cancellationToken),
            "Đã cập nhật ghi chú đọc.");

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await readingNoteService.DeleteAsync(CurrentUserId, id, cancellationToken);
        return OkEmptyData("Đã xóa ghi chú đọc.");
    }
}
