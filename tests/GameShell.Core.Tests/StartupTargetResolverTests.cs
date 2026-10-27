using GameShell.Core.Models;
using GameShell.Core.Services;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class StartupTargetResolverTests
{
    [Fact]
    public void RestoreLegacyProfileState_UsesActiveProfileWhenStableIdWasNotPersisted()
    {
        var settings = CreateSettings(StartupBehavior.RestoreLastActiveMode);
        settings.Display.ActiveGamingProfileName = "CONSOLE";
        settings.Display.GamingProfiles[0].Id = "profile-console";
        var target = new StartupTargetResolver().Resolve(settings, new RuntimeState { LastActiveMode = "Profile" }, false);
        Assert.Equal(StartupTargetKind.Profile, target.Kind);
        Assert.Equal("profile-console", target.ProfileId);
    }

    [Fact]
    public void RestoreDeletedProfile_DoesNotSilentlyActivateAnotherProfile()
    {
        var settings = CreateSettings(StartupBehavior.RestoreLastActiveMode);
        settings.Display.ActiveGamingProfileName = "CONSOLE";
        var target = new StartupTargetResolver().Resolve(settings,
            new RuntimeState { LastActiveMode = "Profile", LastActiveProfileId = "deleted" }, false);
        Assert.Equal(StartupTargetKind.Work, target.Kind);
    }

    [Fact]
    public void RenamedProfile_IsRestoredByStableId()
    {
        var settings = CreateSettings(StartupBehavior.RestoreLastActiveMode);
        settings.Display.GamingProfiles[0].Id = "stable-id";
        settings.Display.GamingProfiles[0].Name = "NEW NAME";
        var target = new StartupTargetResolver().Resolve(settings,
            new RuntimeState { LastActiveMode = "Profile", LastActiveProfileId = " stable-id " }, false);
        Assert.Equal(StartupTargetKind.Profile, target.Kind);
        Assert.Equal("NEW NAME", new StartupTargetResolver().ResolveProfileName(settings, target.ProfileId));
    }

    private readonly StartupTargetResolver _resolver = new();

    [Fact]
    public void NormalMode_ResolvesConfiguredStartupProfile()
    {
        var settings = CreateSettings(StartupBehavior.AlwaysProfile);
        settings.Startup.StartupProfileId = "profile-console";
        settings.Display.GamingProfiles[0].Id = "profile-console";

        var target = _resolver.Resolve(settings, new RuntimeState(), safeMode: false);

        Assert.Equal(StartupTargetKind.Profile, target.Kind);
        Assert.Equal("profile-console", target.ProfileId);
    }

    [Fact]
    public void AlwaysProfile_ResolvesConfiguredProfile()
    {
        var settings = CreateSettings(StartupBehavior.AlwaysProfile);
        settings.Display.GamingProfiles[0].Id = "profile-console";
        settings.Startup.StartupProfileId = "profile-console";

        var target = _resolver.Resolve(settings, new RuntimeState(), safeMode: false);

        Assert.Equal(StartupTargetKind.Profile, target.Kind);
        Assert.Equal("profile-console", target.ProfileId);
    }

    [Fact]
    public void MissingAlwaysProfile_FallsBackToWork()
    {
        var settings = CreateSettings(StartupBehavior.AlwaysProfile);
        settings.Startup.StartupProfileId = "MISSING";

        var target = _resolver.Resolve(settings, new RuntimeState(), safeMode: false);

        Assert.Equal(StartupTargetKind.Work, target.Kind);
        Assert.Equal("ProfileUnavailable", target.Reason);
    }

    [Theory]
    [InlineData("Work", null, StartupTargetKind.Work)]
    [InlineData("Console", null, StartupTargetKind.Console)]
    [InlineData("Profile", "CONSOLE", StartupTargetKind.Profile)]
    public void RestoreLastActiveMode_ResolvesCommittedTarget(
        string lastMode,
        string? profileId,
        StartupTargetKind expected)
    {
        var settings = CreateSettings(StartupBehavior.RestoreLastActiveMode);
        settings.Display.GamingProfiles[0].Id = "profile-console";
        var runtime = new RuntimeState { LastActiveMode = lastMode, LastActiveProfileId = profileId == "CONSOLE" ? "profile-console" : profileId };

        var target = _resolver.Resolve(settings, runtime, safeMode: false);

        Assert.Equal(expected, target.Kind);
        Assert.Equal(profileId == "CONSOLE" ? "profile-console" : profileId, target.ProfileId);
    }

    [Fact]
    public void LegacyProfileNameReference_ResolvesToStableId()
    {
        var settings = CreateSettings(StartupBehavior.AlwaysProfile);
        settings.Display.GamingProfiles[0].Id = "profile-console";
        settings.Startup.StartupProfileId = "CONSOLE";

        var target = _resolver.Resolve(settings, new RuntimeState(), safeMode: false);

        Assert.Equal(StartupTargetKind.Profile, target.Kind);
        Assert.Equal("profile-console", target.ProfileId);
        Assert.Equal("CONSOLE", _resolver.ResolveProfileName(settings, target.ProfileId));
    }

    [Fact]
    public void SafeMode_AlwaysBypassesGamingStartup()
    {
        var settings = CreateSettings(StartupBehavior.AlwaysProfile);
        settings.Startup.StartupProfileId = "CONSOLE";

        var target = _resolver.Resolve(settings, new RuntimeState(), safeMode: true);

        Assert.Equal(StartupTargetKind.Work, target.Kind);
        Assert.Equal("SafeMode", target.Reason);
    }

    [Fact]
    public void ExistingSettings_DefaultStartupTargetToWork()
    {
        var settings = AppSettingsValidator.Validate(new AppSettings());

        Assert.Equal(StartupBehavior.AlwaysWorkMode, settings.Startup.StartupBehavior);
    }

    private static AppSettings CreateSettings(StartupBehavior behavior) => new()
    {
        Startup = new StartupSettings
        {
            StartupBehavior = behavior
        },
        Display = new DisplaySettings
        {
            GamingProfiles = [new DisplayLayoutProfile { Name = "CONSOLE" }]
        }
    };
}
