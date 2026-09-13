namespace Aetheria.Operations.Api;

public sealed record SetWaitTimeCommandRequest(
    string AttractionId,
    int WaitTimeMinutes);