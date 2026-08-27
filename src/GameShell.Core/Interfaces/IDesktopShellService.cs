using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

/// <summary>Best-effort lifecycle for the Explorer desktop in optional shell mode.</summary>
public interface IDesktopShellService
{
    bool IsExplorerShellRunning();
    Task<bool> EnsureDesktopAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task PrepareGamingAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
