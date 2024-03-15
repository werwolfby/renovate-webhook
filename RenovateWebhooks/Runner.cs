using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace RenovateWebhooks;

public interface IRunner : IHostedService
{
    void Trigger();
}

public class Runner(IOptions<RunnerOptions> runnerOptions, IExecutor executor, ILogger<Runner> logger) : BackgroundService, IRunner
{
    private static readonly object Event = new();
    private readonly RunnerOptions _runnerOptions = runnerOptions.Value;

    private readonly Channel<object> _executeChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false,
    });

    public void Trigger() => _executeChannel.Writer.TryWrite(Event);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Runner started");

        var cronJobTask = CronJobProducer(stoppingToken);

        await ExecuteJob(() => executor.Run(stoppingToken));

        await cronJobTask;

        logger.LogInformation("Runner finished");
    }

    private async Task ExecuteJob(Func<Task> run)
    {
        await foreach (var _ in _executeChannel.Reader.ReadAllAsync())
        {
            try
            {
                logger.LogInformation("Exec started");
                await run();
                logger.LogInformation("Exec completed");
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Job cancelled");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error running job");
            }
        }

        logger.LogInformation("Job execution finished");
    }

    private async Task CronJobProducer(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _executeChannel.Writer.WriteAsync(Event, cancellationToken);
                await Task.Delay(_runnerOptions.Schedule, cancellationToken);
            }
        }
        catch (OperationCanceledException e)
        {
            if (e.CancellationToken != cancellationToken)
            {
                throw;
            }

            logger.LogInformation("CronJobProducer cancelled");
        }
        finally
        {
            _executeChannel.Writer.Complete();
        }
    }
}
