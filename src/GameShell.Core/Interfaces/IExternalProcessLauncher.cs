using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

/// <summary>
/// Starts an external process in an explicitly selected Windows user context.
/// The implementation belongs to the platform integration layer.
/// </summary>
public interface IExternalProcessLauncher
{
    ExternalProcessLaunchResult StartStandardUser(ExternalProcessLaunchRequest request);

    ExternalProcessLaunchResult StartCurrentContext(ExternalProcessLaunchRequest request);
}
