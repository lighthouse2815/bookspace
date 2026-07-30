using BookSpace.Api.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken) =>
        CreatedData(
            await authService.RegisterAsync(request, cancellationToken),
            "Đăng ký tài khoản thành công.");

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        LoginRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await authService.LoginAsync(request, cancellationToken),
            "Đăng nhập thành công.");

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(
        RefreshRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await authService.RefreshAsync(request, cancellationToken),
            "Làm mới phiên đăng nhập thành công.");

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object?>>> Logout(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request, cancellationToken);
        return OkEmptyData("Đăng xuất thành công.");
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<ApiResponse<UserSummary>> Me() =>
        OkData(authService.GetMe(CurrentUserId));
}
