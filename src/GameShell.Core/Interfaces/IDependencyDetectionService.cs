using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

public interface IDependencyDetectionService
{
    Task<IReadOnlyList<DependencyStatus>> DetectAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);
}
