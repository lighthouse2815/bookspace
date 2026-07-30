using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace BookSpace.Api.Common;

public static class JwtResponseEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object?>.Failure("Bạn cần đăng nhập để tiếp tục.", "UNAUTHORIZED"));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object?>.Failure("Bạn không có quyền thực hiện thao tác này.", "FORBIDDEN"));
        }
    };
}
