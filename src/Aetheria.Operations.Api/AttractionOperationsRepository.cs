using System.Text.Json;
using StackExchange.Redis;

namespace Aetheria.Operations.Api;

public sealed class AttractionOperationsRepository(
    IConnectionMultiplexer connection,
    ILogger<AttractionOperationsRepository> logger)
{
    private const string AttractionIndexKey = "aetheria:operations:attractions";

    private const string DemoLatencyKey = "aetheria:demo:latency";

    private static readonly AttractionOperationalState[] SeedData =
    [
        new(
            "clockwork-citadel",
            "Clockwork Citadel",
            25,
            "Open",
            DateTimeOffset.UtcNow),

        new(
            "stormwing",
            "Stormwing",
            45,
            "Open",
            DateTimeOffset.UtcNow),

        new(
            "deepwood-expedition",
            "Deepwood Expedition",
            15,
            "Open",
            DateTimeOffset.UtcNow),

        new(
            "orbitfall",
            "Orbitfall",
            60,
            "Open",
            DateTimeOffset.UtcNow)
    ];

    private IDatabase Database => connection.GetDatabase();

    public async Task SeedIfEmptyAsync()
    {
        var count = await Database.SetLengthAsync(AttractionIndexKey);

        if (count > 0)
        {
            logger.LogInformation("Redis already contains {AttractionCount} attractions", count);

            return;
        }

        logger.LogInformation("No operational attraction data found. Seeding Aetheria.");

        foreach (var attraction in SeedData)
        {
            await SaveAsync(attraction);
        }

        logger.LogInformation("Seeded {AttractionCount} attractions", SeedData.Length);
    }

    public async Task<IReadOnlyCollection<AttractionOperationalState>> GetAllAsync()
    {
        using var activity = ParkTelemetry.ActivitySource.StartActivity("Load all attraction operations");

        var members = await Database.SetMembersAsync(AttractionIndexKey);

        var tasks = members.Select(member => GetAsync(member.ToString()));

        var results = await Task.WhenAll(tasks);

        return results
            .OfType<AttractionOperationalState>()
            .OrderBy(x => x.Name)
            .ToArray();
    }

    public async Task<AttractionOperationalState?> GetAsync(string id)
    {
        using var activity = ParkTelemetry.ActivitySource.StartActivity("Load attraction operations");

        activity?.SetTag("attraction.id", id);

        var value = await Database.StringGetAsync(GetAttractionKey(id));

        if (value.IsNullOrEmpty)
        {
            logger.LogWarning("Operational data was requested for unknown attraction {AttractionId}", id);

            return null;
        }

        return JsonSerializer.Deserialize<AttractionOperationalState>(value.ToString());
    }

    public async Task<AttractionOperationalState?> UpdateWaitTimeAsync(string id, int waitTimeMinutes)
    {
        using var activity = ParkTelemetry.ActivitySource.StartActivity("Update attraction wait time");

        activity?.SetTag("attraction.id", id);

        activity?.SetTag("wait_time.minutes", waitTimeMinutes);

        var current = await GetAsync(id);

        if (current is null)
        {
            return null;
        }

        var updated = current with
        {
            WaitTimeMinutes = waitTimeMinutes,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await SaveAsync(updated);

        logger.LogInformation(
            "Wait time for {AttractionName} changed from {PreviousWaitTime} to {NewWaitTime} minutes",
            current.Name,
            current.WaitTimeMinutes,
            waitTimeMinutes);

        ParkTelemetry.WaitTimeUpdates.Add(1, new KeyValuePair<string, object?>("attraction.id", id));

        ParkTelemetry.WaitTime.Record(waitTimeMinutes, new KeyValuePair<string, object?>("attraction.id", id));

        return updated;
    }

    public async Task<AttractionOperationalState?> UpdateStatusAsync(string id, string status)
    {
        using var activity = ParkTelemetry.ActivitySource.StartActivity("Update attraction status");

        activity?.SetTag("attraction.id", id);

        activity?.SetTag("attraction.status", status);

        var current = await GetAsync(id);

        if (current is null)
        {
            return null;
        }

        var updated = current with
        {
            Status = status,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await SaveAsync(updated);

        logger.LogWarning(
            "Operational status for {AttractionName} changed from {PreviousStatus} to {NewStatus}",
            current.Name,
            current.Status,
            status);

        ParkTelemetry.StatusUpdates.Add(1, new KeyValuePair<string, object?>("attraction.id", id));

        return updated;
    }

    public async Task SetDemoLatencyAsync(int milliseconds)
    {
        await Database.StringSetAsync(DemoLatencyKey, milliseconds);

        logger.LogWarning(
            "Artificial Operations API latency set to {DelayMilliseconds} ms", milliseconds);
    }

    public async Task<int> GetDemoLatencyAsync()
    {
        var value = await Database.StringGetAsync( DemoLatencyKey);

        if (value.IsNullOrEmpty)
        {
            return 0;
        }

        return int.TryParse(value.ToString(), out var milliseconds)
            ? milliseconds
            : 0;
    }

    public async Task ResetAsync()
    {
        var members = await Database.SetMembersAsync(AttractionIndexKey);

        foreach (var member in members)
        {
            await Database.KeyDeleteAsync(GetAttractionKey(member.ToString()));
        }

        await Database.KeyDeleteAsync(AttractionIndexKey);

        await Database.KeyDeleteAsync(DemoLatencyKey);

        logger.LogWarning("Aetheria operational state was reset");

        await SeedIfEmptyAsync();
    }

    private async Task SaveAsync(AttractionOperationalState attraction)
    {
        var json = JsonSerializer.Serialize(attraction);

        await Database.StringSetAsync(GetAttractionKey(attraction.Id), json);

        await Database.SetAddAsync(AttractionIndexKey, attraction.Id);
    }

    private static string GetAttractionKey(string id) => $"aetheria:operations:attraction:{id}";
}