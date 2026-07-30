using BookSpace.Api.Common;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using BookSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Route("api/clubs")]
public sealed class ClubsController(IClubService clubService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public ActionResult<ApiResponse<PageResult<ClubSummary>>> Clubs(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(clubService.GetClubs(OptionalUserId, search, page, pageSize));

    [Authorize]
    [HttpGet("invitations")]
    public async Task<ActionResult<ApiResponse<PageResult<ClubInvitationDto>>>> MyInvitations(
        [FromQuery] ClubInvitationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        OkData(await clubService.GetMyInvitationsAsync(
            CurrentUserId,
            status,
            page,
            pageSize,
            cancellationToken));

    [Authorize]
    [HttpPost("invitations/{invitationId:guid}/accept")]
    public async Task<ActionResult<ApiResponse<ClubMemberDto>>> AcceptInvitation(
        Guid invitationId,
        CancellationToken cancellationToken) =>
        OkData(
            await clubService.AcceptInvitationAsync(CurrentUserId, invitationId, cancellationToken),
            "Đã chấp nhận lời mời tham gia câu lạc bộ.");

    [Authorize]
    [HttpPost("invitations/{invitationId:guid}/decline")]
    public async Task<ActionResult<ApiResponse<ClubInvitationDto>>> DeclineInvitation(
        Guid invitationId,
        CancellationToken cancellationToken) =>
        OkData(
            await clubService.DeclineInvitationAsync(CurrentUserId, invitationId, cancellationToken),
            "Đã từ chối lời mời tham gia câu lạc bộ.");

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public ActionResult<ApiResponse<ClubSummary>> Club(Guid id) =>
        OkData(clubService.GetClub(id, OptionalUserId));

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ClubSummary>>> Create(
        CreateClubRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await clubService.CreateAsync(CurrentUserId, request, cancellationToken),
            "Tạo câu lạc bộ thành công.");

    [Authorize]
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ClubSummary>>> Update(
        Guid id,
        UpdateClubRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await clubService.UpdateAsync(CurrentUserId, id, request, cancellationToken),
            "Đã cập nhật câu lạc bộ.");

    [Authorize]
    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<ApiResponse<ClubSummary>>> Join(
        Guid id,
        CancellationToken cancellationToken)
    {
        await clubService.JoinAsync(CurrentUserId, id, cancellationToken);
        return OkData(clubService.GetClub(id, CurrentUserId), "Tham gia câu lạc bộ thành công.");
    }

    [Authorize]
    [HttpDelete("{id:guid}/join")]
    public async Task<ActionResult<ApiResponse<object?>>> Leave(
        Guid id,
        CancellationToken cancellationToken)
    {
        await clubService.LeaveAsync(CurrentUserId, id, cancellationToken);
        return OkEmptyData("Đã rời câu lạc bộ.");
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/members")]
    public ActionResult<ApiResponse<PageResult<ClubMemberDto>>> Members(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(clubService.GetMembers(id, OptionalUserId, page, pageSize));

    [Authorize]
    [HttpPatch("{id:guid}/members/{userId:guid}/role")]
    public async Task<ActionResult<ApiResponse<ClubMemberDto>>> UpdateMemberRole(
        Guid id,
        Guid userId,
        UpdateClubMemberRoleRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await clubService.UpdateMemberRoleAsync(
                CurrentUserId,
                id,
                userId,
                request,
                cancellationToken),
            "Đã cập nhật vai trò thành viên.");

    [Authorize]
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> RemoveMember(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await clubService.RemoveMemberAsync(CurrentUserId, id, userId, cancellationToken);
        return OkEmptyData("Đã đưa thành viên ra khỏi câu lạc bộ.");
    }

    [Authorize]
    [HttpGet("{id:guid}/invitations")]
    public async Task<ActionResult<ApiResponse<PageResult<ClubInvitationDto>>>> ClubInvitations(
        Guid id,
        [FromQuery] ClubInvitationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        OkData(await clubService.GetClubInvitationsAsync(
            CurrentUserId,
            id,
            status,
            page,
            pageSize,
            cancellationToken));

    [Authorize]
    [HttpPost("{id:guid}/invitations")]
    public async Task<ActionResult<ApiResponse<ClubInvitationDto>>> Invite(
        Guid id,
        InviteClubMemberRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await clubService.InviteAsync(CurrentUserId, id, request, cancellationToken),
            "Đã gửi lời mời tham gia câu lạc bộ.");

    [Authorize]
    [HttpDelete("{id:guid}/invitations/{invitationId:guid}")]
    public async Task<ActionResult<ApiResponse<ClubInvitationDto>>> RevokeInvitation(
        Guid id,
        Guid invitationId,
        CancellationToken cancellationToken) =>
        OkData(
            await clubService.RevokeInvitationAsync(
                CurrentUserId,
                id,
                invitationId,
                cancellationToken),
            "Đã thu hồi lời mời.");

    [Authorize]
    [HttpPut("{id:guid}/current-book")]
    public async Task<ActionResult<ApiResponse<ClubSummary>>> SetCurrentBook(
        Guid id,
        SetClubCurrentBookRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await clubService.SetCurrentBookAsync(CurrentUserId, id, request, cancellationToken),
            "Đã cập nhật sách đọc chung.");

    [Authorize]
    [HttpDelete("{id:guid}/current-book")]
    public async Task<ActionResult<ApiResponse<ClubSummary>>> ClearCurrentBook(
        Guid id,
        CancellationToken cancellationToken) =>
        OkData(
            await clubService.ClearCurrentBookAsync(CurrentUserId, id, cancellationToken),
            "Đã bỏ sách đọc chung.");

    [AllowAnonymous]
    [HttpGet("{id:guid}/posts")]
    public ActionResult<ApiResponse<PageResult<ClubPostDto>>> Posts(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        OkData(clubService.GetPosts(id, OptionalUserId, page, pageSize));

    [Authorize]
    [HttpPost("{id:guid}/posts")]
    public async Task<ActionResult<ApiResponse<ClubPostDto>>> AddPost(
        Guid id,
        CreateClubPostRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await clubService.AddPostAsync(CurrentUserId, id, request, cancellationToken),
            "Đăng bài thành công.");

    [Authorize]
    [HttpDelete("posts/{postId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeletePost(
        Guid postId,
        CancellationToken cancellationToken)
    {
        await clubService.DeletePostAsync(CurrentUserId, IsAdmin, postId, cancellationToken);
        return OkEmptyData("Đã xóa bài viết.");
    }

    [AllowAnonymous]
    [HttpGet("posts/{postId:guid}/comments")]
    public ActionResult<ApiResponse<PageResult<ClubPostCommentDto>>> PostComments(
        Guid postId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50) =>
        OkData(clubService.GetPostComments(postId, OptionalUserId, page, pageSize));

    [Authorize]
    [HttpPost("posts/{postId:guid}/comments")]
    public async Task<ActionResult<ApiResponse<ClubPostCommentDto>>> AddPostComment(
        Guid postId,
        CreateCommentRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await clubService.AddPostCommentAsync(CurrentUserId, postId, request, cancellationToken),
            "Đã thêm bình luận.");

    [Authorize]
    [HttpDelete("post-comments/{commentId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeletePostComment(
        Guid commentId,
        CancellationToken cancellationToken)
    {
        await clubService.DeletePostCommentAsync(CurrentUserId, IsAdmin, commentId, cancellationToken);
        return OkEmptyData("Đã xóa bình luận.");
    }
}
