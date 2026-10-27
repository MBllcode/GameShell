using GameShell.Core.Settings;

namespace GameShell.Core.Profiles;

public static class ProfileApplicationPolicy
{
    public static bool ShouldLaunch(LauncherSettings launcher, GameShellSettings behaviour) =>
        behaviour.StartLauncher && !string.Equals(launcher.Type, "None", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldMonitor(LauncherSettings launcher, GameShellSettings behaviour) =>
        ShouldLaunch(launcher, behaviour)
        && (string.Equals(launcher.Type, "Playnite", StringComparison.OrdinalIgnoreCase)
            || string.Equals(launcher.Type, "Steam", StringComparison.OrdinalIgnoreCase)
            || string.Equals(launcher.Type, "XboxApp", StringComparison.OrdinalIgnoreCase));
}
