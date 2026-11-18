namespace PickUp.Server;

public sealed class GameClockService(
    GameCoordinator coordinator,
    RoomNotifier notifier,
    ILogger<GameClockService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var code in coordinator.Tick())
                    await notifier.PublishAsync(code, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fehler im Pick-Up-Spiel-Takt.");
            }
        }
    }
}
