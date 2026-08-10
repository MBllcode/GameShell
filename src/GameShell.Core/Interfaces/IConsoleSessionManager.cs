using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

public interface IConsoleSessionManager
{
    ConsoleSessionSnapshot GetSnapshot();
    Task<ModeResult> EnterConsoleAsync(CancellationToken cancellationToken = default);
    Task<ModeResult> EnterProfileAsync(string profileName, CancellationToken cancellationToken = default);
    Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default);
}
