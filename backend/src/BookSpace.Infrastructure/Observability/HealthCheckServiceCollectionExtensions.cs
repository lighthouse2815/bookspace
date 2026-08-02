using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BookSpace.Infrastructure;

public static class HealthCheckServiceCollectionExtensions
{
    public const int DatabaseTimeoutSeconds = 5;

    public static IHealthChecksBuilder AddBookSpaceDatabaseHealthCheck(
        this IServiceCollection services) =>
        services
            .AddHealthChecks()
            .AddCheck<Observability.BookSpaceDatabaseHealthCheck>(
                "bookspace_database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "ready"],
                timeout: TimeSpan.FromSeconds(DatabaseTimeoutSeconds));
}
