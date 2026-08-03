using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Route("api/book-lists")]
public sealed class BookListsController(IBookListService bookListService) : ApiControllerBase
{
    [Authorize]
    [HttpGet]
    public ActionResult<ApiResponse<PageResult<BookListSummaryDto>>> Mine(
        [FromQuery] BookListVisibility? visibility,
        [FromQuery] Guid? bookId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(bookListService.GetMine(CurrentUserId, visibility, bookId, page, pageSize));

    [AllowAnonymous]
    [HttpGet("/api/users/{userId:guid}/book-lists")]
    public ActionResult<ApiResponse<PageResult<BookListSummaryDto>>> PublicByUser(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(bookListService.GetPublicByUser(userId, OptionalUserId, page, pageSize));

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public ActionResult<ApiResponse<BookListDetailDto>> Get(Guid id) =>
        OkData(bookListService.Get(id, OptionalUserId));

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BookListDetailDto>>> Create(
        CreateBookListRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await bookListService.CreateAsync(CurrentUserId, request, cancellationToken),
            "Đã tạo bộ sưu tập.");

    [Authorize]
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BookListDetailDto>>> Update(
        Guid id,
        UpdateBookListRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await bookListService.UpdateAsync(CurrentUserId, id, request, cancellationToken),
            "Đã cập nhật bộ sưu tập.");

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await bookListService.DeleteAsync(CurrentUserId, id, cancellationToken);
        return OkEmptyData("Đã xóa bộ sưu tập.");
    }

    [Authorize]
    [HttpPost("{id:guid}/books")]
    public async Task<ActionResult<ApiResponse<BookListDetailDto>>> AddBook(
        Guid id,
        AddBookToListRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await bookListService.AddBookAsync(CurrentUserId, id, request, cancellationToken),
            "Đã thêm sách vào bộ sưu tập.");

    [Authorize]
    [HttpDelete("{id:guid}/books/{bookId:guid}")]
    public async Task<ActionResult<ApiResponse<BookListDetailDto>>> RemoveBook(
        Guid id,
        Guid bookId,
        CancellationToken cancellationToken) =>
        OkData(
            await bookListService.RemoveBookAsync(CurrentUserId, id, bookId, cancellationToken),
            "Đã bỏ sách khỏi bộ sưu tập.");

    [Authorize]
    [HttpPut("{id:guid}/books/reorder")]
    public async Task<ActionResult<ApiResponse<BookListDetailDto>>> Reorder(
        Guid id,
        ReorderBookListRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await bookListService.ReorderAsync(CurrentUserId, id, request, cancellationToken),
            "Đã cập nhật thứ tự sách.");
}
