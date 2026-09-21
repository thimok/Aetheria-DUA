namespace Aetheria.WaitTimeSimulator;

public sealed class SimulationWorker(
    SimulationState state,
    SimulationEngine engine,
    ILogger<SimulationWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Aetheria wait-time simulator started. Simulation is initially paused.");

        using var timer = new PeriodicTimer(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var snapshot = state.GetSnapshot();

            if (!snapshot.IsRunning)
            {
                continue;
            }

            try
            {
                await engine.AdvanceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "An error occurred while advancing the park simulation");
            }
        }
    }
}