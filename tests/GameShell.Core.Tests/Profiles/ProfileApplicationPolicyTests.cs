using GameShell.Core.Profiles;
using GameShell.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ProfileApplicationPolicyTests
{
    [Theory]
    [InlineData("None", true, false)]
    [InlineData("none", true, false)]
    [InlineData("Playnite", false, false)]
    [InlineData("Playnite", true, true)]
    [InlineData("Steam", true, true)]
    [InlineData("XboxApp", true, true)]
    [InlineData("Unknown", true, false)]
    public void OnlyEnabledSupportedApplicationsHaveGameSessions(string type, bool start, bool expected)
    {
        Assert.Equal(expected, ProfileApplicationPolicy.ShouldMonitor(new LauncherSettings { Type = type }, new GamingSettings { StartLauncher = start }));
    }

    [Fact]
    public async Task NoneSelection_RoundTripsAndDisablesStaleLegacyLaunchFlags()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GameShell-neutral-" + Guid.NewGuid());
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var settings = new AppSettings
            {
                Display = new DisplaySettings
                {
                    GamingProfiles = [new DisplayLayoutProfile { Name = "Football" }, new DisplayLayoutProfile { Name = "Console" }, new DisplayLayoutProfile { Name = "PC" }],
                    GamingProfileConfigurations =
                    [
                        new GamingProfileConfiguration { ProfileName = "Football", Launcher = new LauncherSettings { Type = "None", ExecutablePath = "old.exe" } },
                        new GamingProfileConfiguration { ProfileName = "Console", Launcher = new LauncherSettings { Type = "Playnite" } },
                        new GamingProfileConfiguration { ProfileName = "PC", Launcher = new LauncherSettings { Type = "Steam" } }
                    ]
                }
            };
            await new SettingsService(NullLogger<SettingsService>.Instance, path).SaveAsync(settings);
            var loaded = await new SettingsService(NullLogger<SettingsService>.Instance, path).LoadAsync();
            var resolver = new ProfileSettingsResolver();
            var football = resolver.BuildEffectiveSettings(loaded, "Football");
            Assert.Equal("None", football.Launcher.Type);
            Assert.Empty(football.Launcher.ExecutablePath);
            Assert.False(football.Gaming.StartLauncher);
            Assert.False(football.Gaming.BringLauncherToForeground);
            Assert.Equal("Playnite", resolver.BuildEffectiveSettings(loaded, "Console").Launcher.Type);
            Assert.Equal("Steam", resolver.BuildEffectiveSettings(loaded, "PC").Launcher.Type);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void AProfileNamedGaming_IsAnOrdinaryNamedProfile()
    {
        var display = new DisplaySettings
        {
            ActiveGamingProfileName = "Football",
            GamingProfiles = [new DisplayLayoutProfile { Name = "Football" }, new DisplayLayoutProfile { Name = "Gaming" }]
        };
        Assert.Equal("Gaming", new ProfileSettingsResolver().ResolveProfileName(display, "Gaming"));
    }
}
