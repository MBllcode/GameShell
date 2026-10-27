using GameShell.Core.Settings;

namespace GameShell.Desktop.RemoteControl;

public sealed record RemoteControlOptions(
    bool Enabled,
    int Port,
    string AuthenticationToken,
    bool AllowPrivateNetworkOnly)
{
    public static RemoteControlOptions FromSettings(RemoteControlSettings settings)
    {
        return new RemoteControlOptions(
            settings.Enabled,
            settings.Port,
            settings.AuthenticationToken,
            settings.AllowPrivateNetworkOnly);
    }
}
