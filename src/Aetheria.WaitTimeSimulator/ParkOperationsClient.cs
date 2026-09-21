using System.Net.Http.Json;

namespace Aetheria.WaitTimeSimulator;

public sealed class ParkOperationsClient(
    HttpClient httpClient,
    ILogger<ParkOperationsClient> logger)
{
    public async Task<IReadOnlyCollection<AttractionOperationalState>> GetAttractionsAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Loading current attraction state from Park Operations");

        var attractions = await httpClient.GetFromJsonAsync<AttractionOperationalState[]>("/api/attractions", cancellationToken);

        return attractions ?? [];
    }

    public async Task UpdateWaitTimeAsync(string attractionId, int waitTimeMinutes, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/attractions/{attractionId}/wait-time",
            new
            {
                WaitTimeMinutes = waitTimeMinutes
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}

public sealed record AttractionOperationalState(
    string Id,
    string Name,
    int WaitTimeMinutes,
    string Status,
    DateTimeOffset UpdatedAt);