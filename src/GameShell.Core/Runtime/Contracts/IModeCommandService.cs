
namespace GameShell.Core.Runtime;

public interface IModeCommandService
{
    Task<AppStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<ModeResult> EnterGameShellAsync(CancellationToken cancellationToken = default);
    Task<ModeResult> EnterGameShellWithDisplayProfileAsync(string profileName, CancellationToken cancellationToken = default);
    Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default);
}
