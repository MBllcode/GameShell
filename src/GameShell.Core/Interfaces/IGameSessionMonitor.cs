namespace GameShell.Core.Interfaces;

using GameShell.Core.Models;

public interface IGameSessionMonitor
{
    event EventHandler<GameSessionEventArgs>? GameDetected;
    event EventHandler<GameSessionEventArgs>? GameExited;

    Task StartAsync(IReadOnlyCollection<string> processNames, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
