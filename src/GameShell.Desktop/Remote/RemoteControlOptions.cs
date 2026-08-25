using GameShell.Core.Models;

namespace GameShell.Desktop.Remote;

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
