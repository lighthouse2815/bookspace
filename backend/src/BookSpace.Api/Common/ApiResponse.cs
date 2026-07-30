namespace BookSpace.Api.Common;

public sealed record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data,
    string? Code,
    DateTimeOffset Timestamp)
{
    public static ApiResponse<T> Ok(T? data, string message = "Thành công.") =>
        new(true, message, data, null, DateTimeOffset.UtcNow);

    public static ApiResponse<T> Failure(string message, string code, T? data = default) =>
        new(false, message, data, code, DateTimeOffset.UtcNow);
}
