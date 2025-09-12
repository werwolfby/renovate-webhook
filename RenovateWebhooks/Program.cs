using RenovateWebhooks;

var builder = WebApplication.CreateSlimBuilder(args);

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
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/healthz");

app.MapGet("/", () => "Hello, World!");
app.MapPost("/trigger", (IRunner runner) =>
{
    runner.Trigger();
    return Results.Accepted();
});

app.Run();
