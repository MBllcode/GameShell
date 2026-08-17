using GameShell.Core.Models;
using GameShell.Core.Services;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class GamingProfileRuntimeSettingsResolverTests
{
    private readonly GamingProfileRuntimeSettingsResolver _resolver = new();

    [Fact]
    public void ResolveProfileName_PrefersRequestedExistingProfile()
    {
        var display = new DisplaySettings
        {
            ActiveGamingProfileName = "Projector",
            GamingProfiles =
            [
                new DisplayLayoutProfile { Name = "TV" },
                new DisplayLayoutProfile { Name = "Projector" }
            ]
        };

        var result = _resolver.ResolveProfileName(display, "tv");

        Assert.Equal("tv", result);
    }

    [Fact]
    public void ResolveProfileName_UsesActiveProfileForLegacyGamingSentinel()
    {
        var display = new DisplaySettings
        {
            ActiveGamingProfileName = "Projector",
            GamingProfiles = [new DisplayLayoutProfile { Name = "Projector" }]
        };

        var result = _resolver.ResolveProfileName(display, "Gaming");

        Assert.Equal("Projector", result);
    }

    [Fact]
    public void ResolveLayout_UsesTopologyWhenProfileHasNewDisplayModel()
    {
        var display = new DisplaySettings
        {
            GamingProfiles =
            [
                new DisplayLayoutProfile
                {
                    Name = "TV",
                    DisplayTopology = new GamingProfileDisplayTopology
                    {
                        Displays =
                        [
                            new ProfileDisplayTarget
                            {
                                Identity = new DisplayIdentity { FriendlyName = "TV" },
                                Enabled = true,
                                IsPrimary = true,
                                Width = 3840,
                                Height = 2160
                            }
                        ]
                    }
                }
            ]
        };

        var result = _resolver.ResolveLayout(display, "TV");

        var target = Assert.Single(result!.Displays);
        Assert.Equal("TV", target.Identity.FriendlyName);
        Assert.Equal(3840, target.Width);
        Assert.True(target.IsPrimary);
    }

    [Fact]
    public void ResolveLayout_NormalizesLegacyProfileToAutoHighest()
    {
        var display = new DisplaySettings
        {
            GamingProfiles =
            [
                new DisplayLayoutProfile
                {
                    Name = "TV",
                    Layout = new DisplayLayoutSnapshot
                    {
                        Displays =
                        [
                            new DisplayPathSnapshot
                            {
                                Identity = new DisplayIdentity { FriendlyName = "TV" },
                                IsActive = true,
                                IsPrimary = true,
                                Width = 3840,
                                Height = 2160,
                                RefreshRateNumerator = 60,
                                RefreshRateDenominator = 1
                            }
                        ]
                    }
                }
            ]
        };

        var result = _resolver.ResolveLayout(display, "TV");

        var target = Assert.Single(result!.Displays);
        Assert.Equal(3840, target.Width);
        Assert.Equal(2160, target.Height);
        Assert.Equal(0u, target.RefreshRateNumerator);
        Assert.Equal(0u, target.RefreshRateDenominator);
    }

    [Fact]
    public void ResolveLayout_FallsBackToLegacyLayoutWhenNoProfileResolves()
    {
        var legacyLayout = new DisplayLayoutSnapshot
        {
            Displays = [new DisplayPathSnapshot { Identity = new DisplayIdentity { FriendlyName = "Legacy" } }]
        };
        var display = new DisplaySettings { GamingLayout = legacyLayout };

        var result = _resolver.ResolveLayout(display, "Missing");

        Assert.Same(legacyLayout, result);
    }

    [Fact]
    public void BuildEffectiveSettings_UsesProfileConfigurationAndKeepsGlobalSettings()
    {
        var defaultLauncher = new LauncherSettings { Type = "Steam" };
        var profileLauncher = new LauncherSettings { Type = "Playnite" };
        var profileBehaviour = new GamingSettings { CompletionDelaySeconds = 9 };
        var settings = new AppSettings
        {
            Launcher = defaultLauncher,
            Display =
            {
                GamingProfiles = [new DisplayLayoutProfile { Name = "TV" }],
                GamingProfileConfigurations =
                [
                    new GamingProfileConfiguration
                    {
                        ProfileName = "TV",
                        Launcher = profileLauncher,
                        Behaviour = profileBehaviour
                    }
                ]
            }
        };

        var result = _resolver.BuildEffectiveSettings(settings, "tv");

        Assert.Same(settings.Display, result.Display);
        Assert.Same(defaultLauncher, settings.Launcher);
        Assert.Same(profileLauncher, result.Launcher);
        Assert.Same(profileBehaviour, result.Gaming);
        Assert.Same(settings.WorkMode, result.WorkMode);
    }

    [Fact]
    public void ResolveBehaviour_UsesLegacyBehaviourWhenConfigurationIsMissing()
    {
        var legacyBehaviour = new GamingSettings { CompletionDelaySeconds = 7 };
        var display = new DisplaySettings
        {
            GamingProfiles = [new DisplayLayoutProfile { Name = "Projector" }],
            GamingProfileBehaviours =
            [new GamingProfileBehaviour { ProfileName = "Projector", Behaviour = legacyBehaviour }]
        };
        var defaultBehaviour = new GamingSettings { CompletionDelaySeconds = 2 };

        var result = _resolver.ResolveBehaviour(display, defaultBehaviour, "Projector");

        Assert.Same(legacyBehaviour, result);
    }
}
