using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BookSpace.Api.Common;

public static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Refresh = "auth-refresh";
    public const string PasswordResetRequest = "auth-password-reset-request";
    public const string PasswordResetConfirm = "auth-password-reset-confirm";
}

public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Authentication";

    public AuthEndpointRateLimitOptions Login { get; init; } = new()
    {
        PermitLimit = 5,
        WindowSeconds = 60,
        SegmentsPerWindow = 6
    };

    public AuthEndpointRateLimitOptions Refresh { get; init; } = new()
    {
        PermitLimit = 20,
        WindowSeconds = 60,
        SegmentsPerWindow = 6
    };

    public AuthEndpointRateLimitOptions PasswordResetRequest { get; init; } = new()
    {
        PermitLimit = 5,
        WindowSeconds = 900,
        SegmentsPerWindow = 15
    };

    public AuthEndpointRateLimitOptions PasswordResetConfirm { get; init; } = new()
    {
        PermitLimit = 10,
        WindowSeconds = 900,
        SegmentsPerWindow = 15
    };
}

public sealed class AuthEndpointRateLimitOptions
{
    public int PermitLimit { get; init; }
    public int WindowSeconds { get; init; }
    public int SegmentsPerWindow { get; init; }

    internal bool IsValid() =>
        PermitLimit > 0 &&
        WindowSeconds > 0 &&
        SegmentsPerWindow > 0;
}

public static class AuthRateLimitingServiceCollectionExtensions
{
    private const string RateLimitMessage =
        "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.";

    public static IServiceCollection AddBookSpaceAuthRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AuthRateLimitOptions>()
            .Bind(configuration.GetSection(AuthRateLimitOptions.SectionName))
            .Validate(
                settings =>
                    settings.Login.IsValid() &&
                    settings.Refresh.IsValid() &&
                    settings.PasswordResetRequest.IsValid() &&
                    settings.PasswordResetConfirm.IsValid(),
                "Cấu hình giới hạn tần suất xác thực phải sử dụng các giá trị nguyên dương.")
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectedResponseAsync;
            options.AddPolicy(
                AuthRateLimitPolicies.Login,
                context => CreatePartition(context, static settings => settings.Login));
            options.AddPolicy(
                AuthRateLimitPolicies.Refresh,
                context => CreatePartition(context, static settings => settings.Refresh));
            options.AddPolicy(
                AuthRateLimitPolicies.PasswordResetRequest,
                context => CreatePartition(
                    context,
                    static settings => settings.PasswordResetRequest));
            options.AddPolicy(
                AuthRateLimitPolicies.PasswordResetConfirm,
                context => CreatePartition(
                    context,
                    static settings => settings.PasswordResetConfirm));
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(
        HttpContext context,
        Func<AuthRateLimitOptions, AuthEndpointRateLimitOptions> selectSettings)
    {
        var settings = selectSettings(
            context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value);
        return RateLimitPartition.GetSlidingWindowLimiter(
            GetClientPartitionKey(context),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = settings.PermitLimit,
                Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                SegmentsPerWindow = settings.SegmentsPerWindow,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }

    private static string GetClientPartitionKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true)
        {
            address = address.MapToIPv4();
        }

        return address?.ToString() ?? IPAddress.None.ToString();
    }

    private static async ValueTask WriteRejectedResponseAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/json; charset=utf-8";
        response.Headers.CacheControl = "no-store";

        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var leaseRetryAfter)
            ? leaseRetryAfter
            : GetConfiguredRetryAfter(context.HttpContext);
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        await response.WriteAsJsonAsync(
            ApiResponse<object?>.Failure(RateLimitMessage, "RATE_LIMITED"),
            cancellationToken);
    }

    private static TimeSpan GetConfiguredRetryAfter(HttpContext context)
    {
        var policyName = context.GetEndpoint()?
            .Metadata
            .GetMetadata<EnableRateLimitingAttribute>()?
            .PolicyName;
        var settings = context.RequestServices
            .GetRequiredService<IOptions<AuthRateLimitOptions>>()
            .Value;
        var endpointSettings = policyName switch
        {
            AuthRateLimitPolicies.Login => settings.Login,
            AuthRateLimitPolicies.PasswordResetRequest => settings.PasswordResetRequest,
            AuthRateLimitPolicies.PasswordResetConfirm => settings.PasswordResetConfirm,
            _ => settings.Refresh
        };
        return TimeSpan.FromSeconds(endpointSettings.WindowSeconds);
    }
}
