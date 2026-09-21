using System.Diagnostics;

namespace Aetheria.WaitTimeSimulator;

public sealed class SimulationEngine(
    SimulationState state,
    ParkOperationsClient operationsClient,
    ILogger<SimulationEngine> logger)
{
    private static readonly IReadOnlyDictionary<string, AttractionProfile> AttractionProfiles = new Dictionary<string, AttractionProfile>(StringComparer.OrdinalIgnoreCase)
    {
        ["clockwork-citadel"] =
            new(
                Popularity: 0.95,
                IsIndoor: true),

        ["stormwing"] =
            new(
                Popularity: 1.20,
                IsIndoor: false),

        ["deepwood-expedition"] =
            new(
                Popularity: 0.70,
                IsIndoor: true),

        ["orbitfall"] =
            new(
                Popularity: 1.05,
                IsIndoor: false)
    };

    private readonly SemaphoreSlim _tickLock = new(1, 1);

    public async Task<SimulationSnapshot> AdvanceAsync(CancellationToken cancellationToken)
    {
        await _tickLock.WaitAsync(cancellationToken);

        try
        {
            var snapshot = state.AdvanceBy(TimeSpan.FromMinutes(15));

            var simulatedTime = snapshot.SimulatedTime.ToString("HH:mm");

            using var activity = SimulatorTelemetry.ActivitySource.StartActivity("Advance park simulation");

            activity?.SetTag("park.simulation.time", simulatedTime);

            activity?.SetTag("park.simulation.scenario", snapshot.Scenario.ToString());

            logger.LogInformation("Advancing Aetheria simulation to {SimulationTime} in {Scenario} scenario", simulatedTime, snapshot.Scenario);

            SimulatorTelemetry.SimulationTicks.Add(1, new KeyValuePair<string, object?>("park.simulation.scenario", snapshot.Scenario.ToString()));

            var attractions = await operationsClient.GetAttractionsAsync(cancellationToken);

            foreach (var attraction in attractions)
            {
                if (!string.Equals(attraction.Status, "Open", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var profile = GetProfile(attraction.Id);

                var targetWaitTime = CalculateTargetWaitTime(profile, snapshot);

                var newWaitTime = CalculateNextWaitTime(attraction.WaitTimeMinutes, targetWaitTime);

                if (newWaitTime == attraction.WaitTimeMinutes)
                {
                    continue;
                }

                using var attractionActivity = SimulatorTelemetry.ActivitySource.StartActivity("Simulate attraction wait time");

                attractionActivity?.SetTag("park.simulation.time", simulatedTime);

                attractionActivity?.SetTag("park.simulation.scenario", snapshot.Scenario.ToString());

                attractionActivity?.SetTag("park.attraction.id", attraction.Id);

                attractionActivity?.SetTag("park.attraction.name", attraction.Name);

                attractionActivity?.SetTag("park.wait.previous", attraction.WaitTimeMinutes);

                attractionActivity?.SetTag("park.wait.target", targetWaitTime);

                attractionActivity?.SetTag("park.wait.new", newWaitTime);

                await operationsClient.UpdateWaitTimeAsync(attraction.Id, newWaitTime, cancellationToken);

                logger.LogInformation("Simulated {SimulationTime}: {AttractionName} changed from {PreviousWaitTime} to {NewWaitTime} minutes (target {TargetWaitTime})",
                    simulatedTime,
                    attraction.Name,
                    attraction.WaitTimeMinutes,
                    newWaitTime,
                    targetWaitTime);

                SimulatorTelemetry.WaitTimeChanges.Add(
                    1,
                    new KeyValuePair<string, object?>("park.attraction.id", attraction.Id),
                    new KeyValuePair<string, object?>("park.simulation.scenario", snapshot.Scenario.ToString()));

                SimulatorTelemetry.SimulatedWaitTime.Record(
                    newWaitTime,
                    new KeyValuePair<string, object?>("park.attraction.id", attraction.Id));
            }

            return snapshot;
        }
        finally
        {
            _tickLock.Release();
        }
    }

    private static AttractionProfile GetProfile(string attractionId)
    {
        return AttractionProfiles.TryGetValue(attractionId, out var profile)
            ? profile
            : new AttractionProfile(
                Popularity: 1.0,
                IsIndoor: false);
    }

    internal static int CalculateTargetWaitTime(AttractionProfile profile, SimulationSnapshot snapshot)
    {
        var baseWait = GetBaseWaitTime(snapshot.SimulatedTime);

        var multiplier = snapshot.Scenario switch
        {
            ParkScenario.Quiet =>
                0.65,

            ParkScenario.Normal =>
                1.0,

            ParkScenario.Peak =>
                1.50,

            ParkScenario.Rain =>
                profile.IsIndoor
                    ? 1.30
                    : 0.65,

            ParkScenario.Closing =>
                0.45,

            _ =>
                1.0
        };

        var rawTarget = baseWait * profile.Popularity * multiplier;

        return RoundToFive(Math.Clamp((int)Math.Round(rawTarget), 5, 120));
    }

    internal static int GetBaseWaitTime(TimeOnly time)
    {
        var minutes = time.Hour * 60 + time.Minute;

        return minutes switch
        {
            < 10 * 60 => 15,
            < 11 * 60 => 25,
            < 12 * 60 => 35,
            < 13 * 60 => 45,
            < 14 * 60 => 55,
            < 15 * 60 => 60,
            < 16 * 60 => 55,
            < 17 * 60 => 50,
            < 18 * 60 => 45,
            < 19 * 60 => 35,
            < 20 * 60 => 25,
            _ => 15
        };
    }

    internal static int CalculateNextWaitTime(
        int current,
        int target)
    {
        if (current == target)
        {
            if (Random.Shared.NextDouble() >= 0.20)
            {
                return current;
            }

            return Math.Clamp(current + (Random.Shared.Next(0, 2) == 0 ? -5 : 5), 5, 120);
        }

        var difference = target - current;

        var step = Math.Abs(difference) >= 20
            ? 10
            : 5;

        var movement = Math.Sign(difference) * step;

        if (Random.Shared.NextDouble() < 0.20)
        {
            movement += Random.Shared.Next(-1, 2) * 5;
        }

        var result = current + movement;

        if (difference > 0 && result > target)
        {
            result = target;
        }
        else if (difference < 0 && result < target)
        {
            result = target;
        }

        return RoundToFive(Math.Clamp(result, 5, 120));
    }

    internal static int RoundToFive(int value) => (int)Math.Round(value / 5d) * 5;

    internal sealed record AttractionProfile(
        double Popularity,
        bool IsIndoor);
}