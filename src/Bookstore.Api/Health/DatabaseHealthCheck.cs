using Bookstore.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bookstore.Api.Health;

public sealed class DatabaseHealthCheck(BookstoreDbContext database) : IHealthCheck
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
            // Do not attach a raw database exception to health-check logs or responses.
            return HealthCheckResult.Unhealthy("Database is unavailable.");
        }
    }
}
