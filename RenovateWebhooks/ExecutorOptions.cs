namespace RenovateWebhooks;

public class ExecutorOptions
{
    public string ExecutablePath { get; set; } = "renovate";

    public string[] Arguments { get; set; } = Array.Empty<string>();
}
