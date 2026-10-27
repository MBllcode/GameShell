using GameShell.Core.Settings;

namespace GameShell.Core.Applications;

public interface IDependencyDetectionService
{
    Task<IReadOnlyList<DependencyStatus>> DetectAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);
}
