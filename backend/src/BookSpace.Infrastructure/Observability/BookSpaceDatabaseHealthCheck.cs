using BookSpace.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BookSpace.Infrastructure.Observability;

/// <summary>
/// Verifies that the configured BookSpace database can accept a connection.
/// Failure details stay inside the health report and are never written directly
/// to the public health endpoint response.
/// </summary>
public sealed class BookSpaceDatabaseHealthCheck(BookSpaceDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Cơ sở dữ liệu BookSpace đang sẵn sàng.")
                : HealthCheckResult.Unhealthy("Không thể kết nối cơ sở dữ liệu BookSpace.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy(
                "Không thể kết nối cơ sở dữ liệu BookSpace.");
        }
    }
}
