using Docker.DotNet;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RenovateWebhooks;

public class DockerHealthCheck(ILogger<DockerHealthCheck> logger, IDockerClient dockerClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var version = await dockerClient.System.GetVersionAsync(cancellationToken);
            logger.LogInformation("Docker version: {Version}", version.Version);
            return HealthCheckResult.Healthy("Docker is running");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Docker health check failed");
            return HealthCheckResult.Unhealthy("Docker is not running", ex);
        }
    }
}
