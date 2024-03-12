using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace RenovateWebhooks;

public class Runner(IOptions<RunnerOptions> runnerOptions, ILogger<Runner> logger) : BackgroundService
{
    private static readonly object Event = new();
    private readonly RunnerOptions _runnerOptions = runnerOptions.Value;

    private readonly Channel<object> _triggerChannel = Channel.CreateBounded<object>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false,
        FullMode = BoundedChannelFullMode.DropOldest,
    });
    private readonly Channel<object> _cronJobChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false,
    });
    private readonly Channel<object> _executeChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false,
    });

    public void Trigger() => _triggerChannel.Writer.TryWrite(Event);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = CronJobProducer(stoppingToken);
        _ = ExecuteProducer(stoppingToken);

        await foreach (var _ in _executeChannel.Reader.ReadAllAsync(stoppingToken))
        {
            await RunExternalExecutableAsync(stoppingToken);
        }
    }

    private async Task ExecuteProducer(CancellationToken cancellationToken)
    {
        var triggerReadTask = _triggerChannel.Reader.ReadAsync(cancellationToken).AsTask();
        var cronJobReadTask = _cronJobChannel.Reader.ReadAsync(cancellationToken).AsTask();

        while (!cancellationToken.IsCancellationRequested)
        {
            var completedTask = await Task.WhenAny(triggerReadTask, cronJobReadTask);

            if (completedTask == triggerReadTask)
            {
                await _executeChannel.Writer.WriteAsync(Event, cancellationToken);
                triggerReadTask = _triggerChannel.Reader.ReadAsync(cancellationToken).AsTask();
            }
            else if (completedTask == cronJobReadTask)
            {
                await _executeChannel.Writer.WriteAsync(Event, cancellationToken);
                cronJobReadTask = _cronJobChannel.Reader.ReadAsync(cancellationToken).AsTask();
            }
        }
    }

    private async Task CronJobProducer(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await _cronJobChannel.Writer.WriteAsync(Event, cancellationToken);
                await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _cronJobChannel.Writer.Complete();
        }
    }

    private async Task RunExternalExecutableAsync(CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _runnerOptions.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in _runnerOptions.Arguments)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        using var process = new Process();
        process.StartInfo = processStartInfo;
        process.OutputDataReceived += (sender, args) => {
            logger.LogInformation("Exec Output: {Data}", args.Data);
        };
        process.ErrorDataReceived += (sender, args) => {
            logger.LogInformation("Exec Error: {Data}", args.Data);
        };

        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
    }
}
