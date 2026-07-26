using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HomeOs.Platform.Persistence;

/// <summary>
/// Readiness probe: reports whether the MySQL database is reachable. Registered under the
/// <c>ready</c> tag so liveness (<c>/health</c>) stays green even while the DB is being provisioned.
/// </summary>
public sealed class DatabaseHealthCheck(PlatformDbContext db) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check threw.", ex);
        }
    }
}
