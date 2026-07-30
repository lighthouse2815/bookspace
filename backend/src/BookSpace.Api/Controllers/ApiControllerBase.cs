using System.Security.Claims;
using BookSpace.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace BookSpace.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                        User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id)
                ? id
                : throw new UnauthorizedAccessException("Không xác định được người dùng.");
        }
    }

    protected Guid? OptionalUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                        User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    protected bool IsAdmin => User.IsInRole("ADMIN");

    protected static ActionResult<ApiResponse<T>> OkData<T>(T data, string message = "Thành công.") =>
        new OkObjectResult(ApiResponse<T>.Ok(data, message));

    protected static ActionResult<ApiResponse<T>> CreatedData<T>(T data, string message) =>
        new ObjectResult(ApiResponse<T>.Ok(data, message))
        {
            StatusCode = StatusCodes.Status201Created
        };

    protected static ActionResult<ApiResponse<object?>> OkEmptyData(string message = "Thành công.") =>
        new OkObjectResult(ApiResponse<object?>.Ok(null, message));
}
