using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

public interface ILauncherProcessService
{
    ExternalProcessLaunchResult StartStandardUser(LauncherSettings launcher);

    ExternalProcessLaunchResult StartCurrentContext(LauncherSettings launcher);
}
