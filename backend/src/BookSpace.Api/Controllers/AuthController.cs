using BookSpace.Api.Common;
using BookSpace.Application.Contracts;
using BookSpace.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        LoginRequest request,
        CancellationToken cancellationToken) =>
        OkData(
            await authService.LoginAsync(request, cancellationToken),
            "Đăng nhập thành công.");

    [AllowAnonymous]
    [HttpPost("password-reset/request")]
    [EnableRateLimiting(AuthRateLimitPolicies.PasswordResetRequest)]
    public async Task<ActionResult<ApiResponse<object?>>> RequestPasswordReset(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        await authService.RequestPasswordResetAsync(request, cancellationToken);
        return OkEmptyData(
            "Nếu email thuộc tài khoản BookSpace, hướng dẫn đặt lại mật khẩu đã được gửi.");
    }

    [AllowAnonymous]
    [HttpPost("password-reset/confirm")]
    [EnableRateLimiting(AuthRateLimitPolicies.PasswordResetConfirm)]
    public async Task<ActionResult<ApiResponse<object?>>> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await authService.ResetPasswordAsync(request, cancellationToken);
        return OkEmptyData("Mật khẩu đã được đặt lại. Vui lòng đăng nhập lại.");
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
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
