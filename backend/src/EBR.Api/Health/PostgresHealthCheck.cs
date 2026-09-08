using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace EBR.Api.Health;

public sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("EbrDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy("No se configuró la conexión EbrDatabase");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL disponible");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL no disponible", exception);
        }
    }
}
