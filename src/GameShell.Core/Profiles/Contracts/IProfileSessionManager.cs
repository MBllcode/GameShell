using GameShell.Core.Runtime;

namespace GameShell.Core.Profiles;

public interface IProfileSessionManager
{
    ProfileSessionSnapshot GetSnapshot();
    Task<ModeResult> EnterSelectedProfileAsync(CancellationToken cancellationToken = default);
    Task<ModeResult> EnterProfileAsync(string profileName, CancellationToken cancellationToken = default);
    Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default);
}
