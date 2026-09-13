using System.Diagnostics;
using System.Net.Http.Json;

namespace Aetheria.Web.Services;

public sealed class ParkOperationsClient(HttpClient httpClient, ILogger<ParkOperationsClient> logger)
{
    private static readonly ActivitySource ActivitySource = new("Aetheria.Web");

    public async Task<IReadOnlyCollection<AttractionOperationalState>> GetAttractionsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Load park operations");

        logger.LogInformation("Loading current Aetheria attraction operations");

        try
        {
            var result = await httpClient.GetFromJsonAsync<AttractionOperationalState[]>("/api/attractions", cancellationToken);

            logger.LogInformation("Loaded operational state for {AttractionCount} attractions", result?.Length ?? 0);

            return result ?? [];
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Aetheria Park Operations API is unavailable");

            return [];
        }
    }

    public async Task<AttractionOperationalState?> GetAttractionAsync(string attractionId, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Load live attraction operations");

        activity?.SetTag("attraction.id", attractionId);

        logger.LogInformation("Loading live operations for attraction {AttractionId}", attractionId);

        try
        {
            var result = await httpClient.GetFromJsonAsync<AttractionOperationalState>($"/api/attractions/{attractionId}", cancellationToken);

            logger.LogInformation(
                "Loaded operations for {AttractionId}: {Status}, {WaitTimeMinutes} minute wait",
                attractionId,
                result?.Status,
                result?.WaitTimeMinutes);

            return result;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not load live operations for attraction {AttractionId}", attractionId);

            return null;
        }
    }
}

public sealed record AttractionOperationalState(
    string Id,
    string Name,
    int WaitTimeMinutes,
    string Status,
    DateTimeOffset UpdatedAt);