using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BookSpace.Api.Common;

public static class BookSpaceHealthEndpoint
{
    public static IEndpointConventionBuilder MapBookSpaceHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/health") =>
        endpoints.MapHealthChecks(pattern, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = WriteResponseAsync
        });

    public static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        var body = report.Status == HealthStatus.Healthy
            ? "Healthy"
            : "Unhealthy";
        return context.Response.WriteAsync(body, context.RequestAborted);
    }
}
