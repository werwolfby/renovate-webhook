using System.Reflection;
using System.Text.Json;
using HealthChecks.UI.Client;
using HealthChecks.UI.Core;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RenovateWebhooks;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure JSON serialization for AOT compatibility
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = JsonSerializationContext.Default;
});

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

builder.Services.AddHealthChecks()
    .AddCheck<DockerHealthCheck>("docker");

var app = builder.Build();

var healthzJsonOptions = typeof(UIResponseWriter).GetMethod("CreateJsonOptions", BindingFlags.NonPublic | BindingFlags.Static)
    ?.Invoke(null, []) as JsonSerializerOptions
    ?? throw new InvalidOperationException("Failed to get HealthChecks.UI.Client.UIResponseWriter.CreateJsonOptions");

healthzJsonOptions.TypeInfoResolver = JsonSerializationContext.Default;

var healthzJsonTypeInfo = healthzJsonOptions.TypeInfoResolver.GetTypeInfo(typeof(UIHealthReport), healthzJsonOptions)
    ?? throw new InvalidOperationException("Failed to get JsonTypeInfo for UIHealthReport");

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var uiReport = UIHealthReport.CreateFrom(report);

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Health check status: {Status}", uiReport.Status);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = report.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        await JsonSerializer.SerializeAsync(context.Response.Body, uiReport, healthzJsonTypeInfo).ConfigureAwait(false);
    }
});

app.MapPost("/trigger", (IRunner runner) =>
{
    runner.Trigger();
    return Results.Accepted();
});

app.Run();
