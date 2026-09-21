namespace Aetheria.Operations.Api;

public sealed record AttractionOperationalState(
    string Id,
    string Name,
    int WaitTimeMinutes,
    string Status,
    DateTimeOffset UpdatedAt);