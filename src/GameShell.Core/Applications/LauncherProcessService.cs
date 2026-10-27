using GameShell.Core.Settings;

namespace GameShell.Core.Applications;

public sealed class LauncherProcessService : ILauncherProcessService
{
    private readonly IExternalProcessLauncher _externalProcessLauncher;

    public LauncherProcessService(IExternalProcessLauncher externalProcessLauncher)
    {
        _externalProcessLauncher = externalProcessLauncher;
    }

    public ExternalProcessLaunchResult StartStandardUser(LauncherSettings launcher)
    {
        return _externalProcessLauncher.StartStandardUser(LauncherProcessRequestFactory.Create(launcher));
    }

    public ExternalProcessLaunchResult StartCurrentContext(LauncherSettings launcher)
    {
        return _externalProcessLauncher.StartCurrentContext(LauncherProcessRequestFactory.Create(launcher));
    }
}
