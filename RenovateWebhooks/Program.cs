using RenovateWebhooks;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOptions<RunnerOptions>()
    .BindConfiguration("Runner");

builder.Services.AddSingleton<IRunner, Runner>();
builder.Services.AddHostedService<IRunner>(p => p.GetRequiredService<IRunner>());

var app = builder.Build();

app.MapGet("/", () => "Hello, World!");
app.MapPost("/trigger", (IRunner runner) =>
{
    runner.Trigger();
    return Results.Accepted();
});

app.Run();
