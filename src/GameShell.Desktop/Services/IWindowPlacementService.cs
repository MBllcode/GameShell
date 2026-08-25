using System.Windows;

namespace GameShell.Desktop.Services;

public interface IWindowPlacementService
{
    Task<WindowPlacementResult> PrepareForPresentationAsync(
        Window window,
        CancellationToken cancellationToken = default);

    Task<WindowPlacementResult> ReconcileAsync(
        Window window,
        CancellationToken cancellationToken = default);
}

public sealed record WindowPlacementResult(
    string Result,
    bool Repositioned = false,
    string? RecoveryMonitor = null,
    string? Message = null);
