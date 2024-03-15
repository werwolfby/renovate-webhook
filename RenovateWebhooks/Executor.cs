using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;

namespace RenovateWebhooks;

public interface IExecutor
{
    Task Run(CancellationToken cancellationToken);
}

public class Executor(IOptions<ExecutorOptions> executorOptions, ILogger<Executor> logger) : IExecutor
{
#if UNIX
    private const int SIGTERM = 15;

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);
#else
    private enum ConsoleCtrlEvent
    {
        CtrlC = 0,
        CtrlBreak = 1
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(ConsoleCtrlEvent sigEvent, int dwProcessGroupId);
#endif

    private readonly ExecutorOptions _executorOptions = executorOptions.Value;

    public async Task Run(CancellationToken cancellationToken)
    {
        logger.LogInformation("Running external executable");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _executorOptions.ExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var arg in _executorOptions.Arguments)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data is null)
                return;
            logger.LogInformation("Exec Output: {line}", args.Data);
        };
        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data is null)
                return;
            logger.LogInformation("Exec Error: {line}", args.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException e)
        {
            logger.LogInformation("External executable was cancelled");
            await TerminateProcess(process);
        }

        logger.LogInformation("External executable finished with exit code {ExitCode}", process.ExitCode);
    }

    private async Task TerminateProcess(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            logger.LogInformation("Sending Ctrl+C to process");
#if UNIX
            // Send SIGTERM to request a graceful shutdown
            kill(process.Id, SIGTERM);
#else
            if (!process.CloseMainWindow())
            {
                // If the main window is not responding, send a Ctrl+C event
                GenerateConsoleCtrlEvent(ConsoleCtrlEvent.CtrlBreak, 0);
            }
#endif
            if (process.HasExited)
            {
                return;
            }

            var gracefulTimeout = TimeSpan.FromSeconds(5);
            var cts = new CancellationTokenSource();
            cts.CancelAfter(gracefulTimeout);
            try
            {
                logger.LogInformation("Waiting for process to exit gracefully for {Timeout}", gracefulTimeout);
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Process did not exit gracefully, killing process");
                process.Kill();
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to terminate process");
        }
    }
}
