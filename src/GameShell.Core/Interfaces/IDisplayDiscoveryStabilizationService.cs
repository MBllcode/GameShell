using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

/// <summary>
/// Exposes only the short-lived display discovery settling gate to Core
/// workflows. The discovery implementation and UI snapshot live at the edge.
/// </summary>
public interface IDisplayDiscoveryStabilizationService
{
    DisplayDiscoveryState State { get; }

    Task WaitForSettledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the coordinator's stable logical snapshot. Implementations may
    /// perform the coordinator's normal bounded refresh before returning it.
    /// A default empty result keeps Core test doubles source compatible.
    /// </summary>
    Task<IReadOnlyList<DisplayDeviceInfo>> GetStableDisplaysAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DisplayDeviceInfo>>(Array.Empty<DisplayDeviceInfo>());

    async Task WaitForStableAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        await WaitForSettledAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
    }

    async Task EnsureStableAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        await WaitForStableAsync(timeout, cancellationToken);
    }
}
