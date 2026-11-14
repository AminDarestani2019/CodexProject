using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SecureLogin.Infrastructure.Persistence;

namespace SecureLogin.Infrastructure.Health;

public sealed class DatabaseHealthCheck(SecureLoginDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy("Database is not reachable.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("Database is not reachable.");
        }
    }
}
