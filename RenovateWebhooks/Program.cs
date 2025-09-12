using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Docker.DotNet;
using RenovateWebhooks;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure JSON serialization for AOT compatibility
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = JsonSerializationContext.Default;
});

ConfigureDockerJsonSerializer();

builder.Logging
    .AddSimpleConsole(c =>
    {
        c.SingleLine = true;
    });

builder.Services.AddOptions<RunnerOptions>()
    .BindConfiguration("Runner");
builder.Services.AddOptions<ExecutorOptions>()
    .BindConfiguration("Executor");

builder.Services
    .AddSingleton<IExecutor, Executor>()
    .AddSingleton<IRunner, Runner>()
    ;
builder.Services.AddHostedService<IRunner>(p => p.GetRequiredService<IRunner>());

builder.Services.AddTransient<IDockerClient, DockerClient>(p => new DockerClientConfiguration().CreateClient());

builder.Services.AddHealthChecks()
    .AddCheck<DockerHealthCheck>("docker");

var app = builder.Build();

app.MapHealthChecks("/healthz");

app.MapGet("/", () => "Hello, World!");
app.MapPost("/trigger", (IRunner runner) =>
{
    runner.Trigger();
    return Results.Accepted();
});

app.Run();

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Docker.DotNet.JsonSerializer is preserved by configuration")]
[UnconditionalSuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicProperties' in call to 'System.Type.GetProperty(String, BindingFlags)'", Justification = "Docker.DotNet.JsonSerializer properties are preserved by configuration")]
static void ConfigureDockerJsonSerializer()
{
    var dockerDotNetJsonSerializerType = typeof(DockerClient).Assembly.GetType("Docker.DotNet.JsonSerializer", true)!;
    var instanceProperty = dockerDotNetJsonSerializerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("Docker.DotNet.JsonSerializer.Instance property not found");
    var optionsField = dockerDotNetJsonSerializerType.GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Docker.DotNet.JsonSerializer._options field not found");
    var dockerDotNetJsonSerializer = instanceProperty.GetValue(null);
    JsonSerializerOptions options = (JsonSerializerOptions)optionsField.GetValue(dockerDotNetJsonSerializer)!;
    options.TypeInfoResolver = JsonSerializationContext.Default;
}
