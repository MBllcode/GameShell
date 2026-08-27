using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

/// <summary>
/// Provides Windows integrations that are unavailable or incomplete when
/// GameShell replaces Explorer as the user's shell.
/// </summary>
public interface IConsoleShellCompatibilityService
{
    Task InitializeAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task<bool> OpenGameBarAsync(CancellationToken cancellationToken = default);
}
