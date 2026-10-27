using GameShell.Core.Settings;

namespace GameShell.Core.Applications;

public interface ILauncherProcessService
{
    ExternalProcessLaunchResult StartStandardUser(LauncherSettings launcher);

    ExternalProcessLaunchResult StartCurrentContext(LauncherSettings launcher);
}
