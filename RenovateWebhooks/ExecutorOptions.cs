namespace RenovateWebhooks;

public class ExecutorOptions
{
    public string ExecutablePath { get; set; } = null!;

    public string[] Arguments { get; set; } = Array.Empty<string>();
}
