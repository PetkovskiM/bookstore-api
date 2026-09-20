using Bookstore.Auth.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bookstore.Auth.Health;

public sealed class DatabaseHealthCheck(AuthDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database is unavailable.");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("Database is unavailable.");
        }
    }
}
