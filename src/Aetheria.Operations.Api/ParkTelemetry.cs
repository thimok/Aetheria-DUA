using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Aetheria.Operations.Api;

public static class ParkTelemetry
{
    public const string ActivitySourceName = "Aetheria.Operations.Api";

    public const string MeterName = "Aetheria.ParkOperations";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> WaitTimeUpdates =
        Meter.CreateCounter<long>("park.wait_time.updates", description: "Number of attraction wait-time updates.");

    public static readonly Histogram<int> WaitTime =
        Meter.CreateHistogram<int>("park.wait_time.minutes", unit: "min", description: "Reported attraction wait times.");

    public static readonly Counter<long> StatusUpdates =
        Meter.CreateCounter<long>("park.status.updates", description: "Number of attraction operational-status updates.");
}