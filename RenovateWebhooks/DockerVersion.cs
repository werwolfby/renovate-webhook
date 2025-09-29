namespace RenovateWebhooks;

public class DockerVersion
{
    public required Client Client { get; set; }
    public required Server Server { get; set; }
}

public class Client
{
    public required string Version { get; set; }
    public required string ApiVersion { get; set; }
    public required string DefaultAPIVersion { get; set; }
    public required string GitCommit { get; set; }
    public required string GoVersion { get; set; }
    public required string Os { get; set; }
    public required string Arch { get; set; }
    public required string BuildTime { get; set; }
    public required string Context { get; set; }
}

public class Server
{
    public required Platform Platform { get; set; }
    public required Components[] Components { get; set; }
    public required string Version { get; set; }
    public required string ApiVersion { get; set; }
    public required string MinAPIVersion { get; set; }
    public required string GitCommit { get; set; }
    public required string GoVersion { get; set; }
    public required string Os { get; set; }
    public required string Arch { get; set; }
    public required string KernelVersion { get; set; }
    public required string BuildTime { get; set; }
}

public class Platform
{
    public required string Name { get; set; }
}

public class Components
{
    public required string Name { get; set; }
    public required string Version { get; set; }
    public required Details Details { get; set; }
}

public class Details
{
    public string? ApiVersion { get; set; }
    public string? Arch { get; set; }
    public string? BuildTime { get; set; }
    public string? Experimental { get; set; }
    public string? GitCommit { get; set; }
    public string? GoVersion { get; set; }
    public string? KernelVersion { get; set; }
    public string? MinAPIVersion { get; set; }
    public string? Os { get; set; }
}
