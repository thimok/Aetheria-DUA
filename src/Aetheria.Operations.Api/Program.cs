using Aetheria.Operations.Api;
using OpenTelemetry.Metrics;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisClient("operations-cache");

builder.Services.AddOpenApi();

builder.Services.AddSingleton<AttractionOperationsRepository>();

builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(ParkTelemetry.MeterName);
    });

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Aetheria Park Operations API")
            .ForceDarkMode()
            .ShowOperationId();
    });
}

app.MapGet("/", () => Results.Redirect("/scalar"));

var repository = app.Services.GetRequiredService<AttractionOperationsRepository>();

await repository.SeedIfEmptyAsync();

var attractions = app
    .MapGroup("/api/attractions")
    .WithTags("Attractions");

attractions.MapGet("/", async (AttractionOperationsRepository operations, CancellationToken cancellationToken) =>
    {
        var latency = await operations.GetDemoLatencyAsync();

        if (latency > 0)
        {
            await Task.Delay(latency, cancellationToken);
        }

        return Results.Ok(await operations.GetAllAsync());
    })
    .WithName("GetAttractions")
    .WithSummary("Returns the current operational state of all attractions.");

attractions.MapGet("/{id}", async (string id, AttractionOperationsRepository operations, CancellationToken cancellationToken) =>
    {
        var latency = await operations.GetDemoLatencyAsync();

        if (latency > 0)
        {
            await Task.Delay(latency, cancellationToken);
        }

        var attraction = await operations.GetAsync(id);

        return attraction is null
            ? Results.NotFound()
            : Results.Ok(attraction);
    })
    .WithName("GetAttraction")
    .WithSummary("Returns the current operational state of an attraction.");

attractions.MapPut("/{id}/wait-time", async (string id, UpdateWaitTimeRequest request, AttractionOperationsRepository operations) =>
    {
        if (request.WaitTimeMinutes
            is < 0 or > 300)
        {
            return Results.BadRequest(
                "Wait time must be between 0 and 300 minutes.");
        }

        var attraction =
            await operations
                .UpdateWaitTimeAsync(
                    id,
                    request.WaitTimeMinutes);

        return attraction is null
            ? Results.NotFound()
            : Results.Ok(attraction);
    })
    .WithName("UpdateWaitTime")
    .WithSummary("Changes the current wait time of an attraction.");

attractions.MapPut("/{id}/status", async (string id, UpdateStatusRequest request, AttractionOperationsRepository operations) =>
    {
        string[] allowedStatuses =
        [
            "Open",
            "Closed",
            "Temporarily Closed",
            "Delayed Opening"
        ];

        var status = allowedStatuses.FirstOrDefault(value => value.Equals(request.Status, StringComparison.OrdinalIgnoreCase));

        if (status is null)
        {
            return Results.BadRequest($"Status must be one of: {string.Join(", ", allowedStatuses)}");
        }

        var attraction = await operations.UpdateStatusAsync(id, status);

        return attraction is null
            ? Results.NotFound()
            : Results.Ok(attraction);
    })
    .WithName("UpdateAttractionStatus")
    .WithSummary("Changes the operational status of an attraction.");

var demo = app
    .MapGroup("/api/demo")
    .WithTags("Demo");

demo.MapPut("/latency", async (DemoLatencyRequest request, AttractionOperationsRepository operations) =>
    {
        if (request.DelayMilliseconds is < 0 or > 10_000)
        {
            return Results.BadRequest("Delay must be between 0 and 10000 milliseconds.");
        }

        await operations.SetDemoLatencyAsync(request.DelayMilliseconds);

        return Results.Ok(
            new
            {
                request.DelayMilliseconds
            });
    })
    .WithName("SetDemoLatency")
    .WithSummary("Adds artificial latency for an observability demonstration.");

demo.MapPost("/reset",async (AttractionOperationsRepository operations) =>
    {
        await operations.ResetAsync();

        return Results.Ok(new
        {
            status = "Reset",
            message = "Aetheria park operations restored to defaults."
        });
    })
    .WithName("ResetOperations")
    .WithSummary("Restores Aetheria's operational data to its demo defaults.");

demo.MapPut("/wait-time", async (SetWaitTimeCommandRequest request, AttractionOperationsRepository operations) =>
    {
        if (request.WaitTimeMinutes is < 0 or > 300)
        {
            return Results.BadRequest("Wait time must be between 0 and 300 minutes.");
        }

        var attraction = await operations.UpdateWaitTimeAsync(request.AttractionId, request.WaitTimeMinutes);

        return attraction is null
            ? Results.NotFound()
            : Results.Ok(attraction);
    })
    .WithName("SetDemoAttractionWaitTime")
    .WithSummary("Sets an attraction wait time through developer tooling.");

await app.RunAsync();