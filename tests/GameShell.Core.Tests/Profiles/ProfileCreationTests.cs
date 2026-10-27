using GameShell.Core.Displays;
using GameShell.Core.Settings;
using GameShell.Desktop.Bridge;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ProfileCreationTests
{
    [Fact]
    public void ProfilesCanShareOneDisplayLayoutAndHaveIndependentApplications()
    {
        var display = new DisplaySettings();
        var layout = new DisplayLayoutSnapshot { Displays = [new DisplayPathSnapshot { IsActive = true, IsPrimary = true, Width = 1920, Height = 1080 }] };
        foreach (var name in new[] { "Football", "Console", "PC" })
            WebViewDisplayCommandHandler.UpsertGamingProfile(display, name, layout);
        Assert.Equal(3, display.GamingProfiles.Count);
        Assert.All(display.GamingProfileConfigurations, configuration =>
        {
            Assert.Equal("None", configuration.Launcher.Type);
            Assert.False(configuration.Behaviour.StartLauncher);
            Assert.False(configuration.Behaviour.HideDesktopIcons);
        });
        display.GamingProfileConfigurations[1].Launcher.Type = "Playnite";
        display.GamingProfileConfigurations[1].Behaviour.StartLauncher = true;
        display.GamingProfileConfigurations[2].Launcher.Type = "Steam";
        WebViewDisplayCommandHandler.UpsertGamingProfile(display, "Console", layout);
        Assert.Equal(3, display.GamingProfiles.Count);
        Assert.Equal("Playnite", display.GamingProfileConfigurations[1].Launcher.Type);
        Assert.Equal("None", display.GamingProfileConfigurations[0].Launcher.Type);
        Assert.Equal("Steam", display.GamingProfileConfigurations[2].Launcher.Type);
    }
}
