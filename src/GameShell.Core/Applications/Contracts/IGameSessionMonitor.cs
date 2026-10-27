namespace GameShell.Core.Applications;


public interface IGameSessionMonitor
{
    event EventHandler<GameSessionEventArgs>? GameDetected;
    event EventHandler<GameSessionEventArgs>? GameExited;

    Task StartAsync(IReadOnlyCollection<string> processNames, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
