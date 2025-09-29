using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RenovateWebhooks;

public class DockerHealthCheck(ILogger<DockerHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "version -f json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                logger.LogError("Docker version command failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                return HealthCheckResult.Unhealthy($"Docker command failed with exit code {process.ExitCode}", new Exception(error));
            }

            var dockerVersion = JsonSerializer.Deserialize(output, JsonSerializationContext.Default.DockerVersion);

            if (dockerVersion?.Server == null)
            {
                logger.LogWarning("Docker server is not available");
                return HealthCheckResult.Unhealthy("Docker server is not available");
            }

            logger.LogInformation("Docker client version: {ClientVersion}, server version: {ServerVersion}",
                dockerVersion.Client?.Version ?? "unknown",
                dockerVersion.Server.Version);

            return HealthCheckResult.Healthy($"Docker is running (Server: {dockerVersion.Server.Version})");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Docker health check failed");
            return HealthCheckResult.Unhealthy("Docker is not running or not accessible", ex);
        }
    }
}
