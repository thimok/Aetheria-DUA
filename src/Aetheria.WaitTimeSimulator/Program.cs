using Aetheria.WaitTimeSimulator;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder
    .Services
    .AddHttpClient<ParkOperationsClient>(client =>
    {
        client.BaseAddress =
            new Uri(
                "https+http://operations-api");
    });

builder.Services.AddSingleton<SimulationState>();

builder.Services.AddSingleton<SimulationEngine>();

builder.Services.AddHostedService<SimulationWorker>();

builder
    .Services
    .AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(SimulatorTelemetry.MeterName);
    });

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", (SimulationState state) => Results.Ok(state.GetSnapshot()));

app.MapGet("/api/simulation", (SimulationState state) => Results.Ok(state.GetSnapshot()));

app.MapPost("/api/simulation/pause", (SimulationState state, ILogger<Program> logger) =>
{
    var snapshot = state.Pause();

    logger.LogInformation("Park simulation paused at {SimulationTime}", snapshot.SimulatedTime.ToString("HH:mm"));

    return Results.Ok(snapshot);
});

app.MapPost("/api/simulation/resume", (SimulationState state, ILogger<Program> logger) =>
{
    var snapshot = state.Resume();

    logger.LogInformation("Park simulation resumed at {SimulationTime} using {Scenario} scenario", snapshot.SimulatedTime.ToString("HH:mm"), snapshot.Scenario);

    return Results.Ok(snapshot);
});

app.MapPost("/api/simulation/advance", async (SimulationEngine engine, CancellationToken cancellationToken) =>
{
    var snapshot = await engine.AdvanceAsync(cancellationToken);

    return Results.Ok(snapshot);
});

app.MapPut("/api/simulation/scenario", (ChangeScenarioRequest request, SimulationState state, ILogger<Program> logger) =>
{
    if (!Enum.TryParse<ParkScenario>(request.Scenario, ignoreCase: true, out var scenario))
    {
        return Results.BadRequest(new
            {
                error = $"Unknown scenario '{request.Scenario}'.",
                allowed = Enum.GetNames<ParkScenario>()
            });
    }

    var snapshot = state.SetScenario(scenario);

    logger.LogWarning("Park simulation scenario changed to {Scenario} at {SimulationTime}", scenario, snapshot.SimulatedTime.ToString("HH:mm"));

    return Results.Ok(snapshot);
});


app.MapPost("/api/simulation/reset", (SimulationState state, ILogger<Program> logger) =>
{
    var snapshot = state.Reset();

    logger.LogWarning("Park simulation reset to 09:00 Normal and paused");

    return Results.Ok(snapshot);
});

await app.RunAsync();

public sealed record ChangeScenarioRequest(string Scenario);