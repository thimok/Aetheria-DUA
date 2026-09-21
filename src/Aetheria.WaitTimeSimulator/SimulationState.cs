namespace Aetheria.WaitTimeSimulator;

public enum ParkScenario
{
    Quiet,
    Normal,
    Peak,
    Rain,
    Closing
}

public sealed record SimulationSnapshot(
    TimeOnly SimulatedTime,
    ParkScenario Scenario,
    bool IsRunning);

public sealed class SimulationState
{
    private static readonly TimeOnly OpeningTime = new(9, 0);

    private static readonly TimeOnly ClosingTime = new(21, 0);

    private readonly object _sync = new();

    private TimeOnly _simulatedTime = OpeningTime;
    private ParkScenario _scenario = ParkScenario.Normal;
    private bool _isRunning;

    public SimulationSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return CreateSnapshot();
        }
    }

    public SimulationSnapshot Pause()
    {
        lock (_sync)
        {
            _isRunning = false;

            return CreateSnapshot();
        }
    }

    public SimulationSnapshot Resume()
    {
        lock (_sync)
        {
            _isRunning = true;

            return CreateSnapshot();
        }
    }

    public SimulationSnapshot SetScenario(ParkScenario scenario)
    {
        lock (_sync)
        {
            _scenario = scenario;

            return CreateSnapshot();
        }
    }

    public SimulationSnapshot AdvanceBy(TimeSpan amount)
    {
        lock (_sync)
        {
            var next = _simulatedTime.AddMinutes(amount.TotalMinutes);

            if (next >= ClosingTime || next < OpeningTime)
            {
                next = OpeningTime;
            }

            _simulatedTime = next;

            return CreateSnapshot();
        }
    }

    public SimulationSnapshot Reset()
    {
        lock (_sync)
        {
            _simulatedTime = OpeningTime;
            _scenario = ParkScenario.Normal;
            _isRunning = false;

            return CreateSnapshot();
        }
    }

    private SimulationSnapshot CreateSnapshot() => new(_simulatedTime, _scenario, _isRunning);
}