using BookSpace.Application.Common;
using BookSpace.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Api.Common;

public sealed class ApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (status, code, message) = Map(exception);
            if (status >= 500)
            {
                logger.LogError(exception, "Unhandled API error {Code}", code);
            }
            else
            {
                logger.LogInformation(exception, "Handled API error {Code}", code);
            }

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(ApiResponse<object?>.Failure(message, code));
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        UseCaseException useCase => (useCase.StatusCode, useCase.Code, useCase.Message),
        DomainException domain when domain.Code == "READING_PROGRESS_CANNOT_DECREASE" =>
            (409, domain.Code, domain.Message),
        DomainException domain => (400, domain.Code, domain.Message),
        UnauthorizedAccessException => (401, "UNAUTHORIZED", "Bạn cần đăng nhập để tiếp tục."),
        DbUpdateException => (409, "DATA_CONFLICT", "Dữ liệu bị trùng hoặc đang được sử dụng."),
        BadHttpRequestException => (400, "INVALID_REQUEST", "Yêu cầu không hợp lệ."),
        OperationCanceledException => (499, "REQUEST_CANCELLED", "Yêu cầu đã bị hủy."),
        _ => (500, "INTERNAL_ERROR", "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")
    };
}
