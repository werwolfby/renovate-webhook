namespace RenovateWebhooks;

public class RunnerOptions
{
    public string ExecutablePath { get; set; } = null!;

    public string[] Arguments { get; set; } = Array.Empty<string>();

    public TimeSpan Schedule { get; set; } = TimeSpan.FromHours(1);
}
