using GameShell.Core.Models;

namespace GameShell.Core.Services;

/// <summary>
/// Resolves the effective gaming profile and its runtime settings without I/O.
/// Keeping this policy separate makes profile selection independently testable.
/// </summary>
public sealed class GamingProfileRuntimeSettingsResolver
{
    private const string LegacyGamingProfileSentinel = "Gaming";

    public DisplayLayoutProfile? ResolveProfile(DisplaySettings display, string? requestedProfileName)
    {
        var resolvedProfileName = ResolveProfileName(display, requestedProfileName);
        return FindProfile(display, resolvedProfileName);
    }

    public string? ResolveProfileName(DisplaySettings display, string? requestedProfileName)
    {
        if (IsNamedProfileRequest(display, requestedProfileName))
        {
            return requestedProfileName;
        }

        if (HasProfile(display, display.ActiveGamingProfileName))
        {
            return display.ActiveGamingProfileName;
        }

        return display.GamingProfiles.FirstOrDefault()?.Name;
    }

    public string? ResolveProfileId(DisplaySettings display, string? requestedProfileName)
    {
        var profile = FindProfileByNameOrId(display, requestedProfileName)
            ?? FindProfile(display, ResolveProfileName(display, requestedProfileName));

        return profile is null
            ? null
            : string.IsNullOrWhiteSpace(profile.Id) ? profile.Name : profile.Id;
    }

    public DisplayLayoutSnapshot? ResolveLayout(DisplaySettings display, string? requestedProfileName)
    {
        var profile = ResolveProfile(display, requestedProfileName);
        return profile is not null
            ? ResolveProfileLayout(profile)
            : display.GamingLayout;
    }

    public GamingProfileConfiguration? ResolveConfiguration(DisplaySettings display, string? requestedProfileName)
    {
        return FindConfiguration(display, ResolveProfileName(display, requestedProfileName));
    }

    public AppSettings BuildEffectiveSettings(AppSettings settings, string? requestedProfileName)
    {
        var profileName = ResolveProfileName(settings.Display, requestedProfileName);
        var configuration = FindConfiguration(settings.Display, profileName);

        return new AppSettings
        {
            Display = settings.Display,
            Launcher = configuration?.Launcher ?? settings.Launcher,
            GameShell = settings.GameShell,
            Gaming = configuration?.Behaviour ?? settings.Gaming,
            WorkMode = settings.WorkMode,
            Controller = settings.Controller,
            Hotkeys = settings.Hotkeys,
            Startup = settings.Startup,
            Ui = settings.Ui,
            RemoteControl = settings.RemoteControl
        };
    }

    public GamingSettings ResolveBehaviour(
        DisplaySettings display,
        GamingSettings defaultBehaviour,
        string? requestedProfileName)
    {
        var profileName = ResolveProfileName(display, requestedProfileName);
        var configuration = FindConfiguration(display, profileName);
        if (configuration is not null)
        {
            return configuration.Behaviour;
        }

        if (string.IsNullOrWhiteSpace(profileName))
        {
            return defaultBehaviour;
        }

        return display.GamingProfileBehaviours
            .FirstOrDefault(candidate => NamesEqual(candidate.ProfileName, profileName))
            ?.Behaviour ?? defaultBehaviour;
    }

    private static bool IsNamedProfileRequest(DisplaySettings display, string? requestedProfileName)
    {
        return !string.IsNullOrWhiteSpace(requestedProfileName)
            && !string.Equals(requestedProfileName, LegacyGamingProfileSentinel, StringComparison.OrdinalIgnoreCase)
            && HasProfile(display, requestedProfileName);
    }

    private static bool HasProfile(DisplaySettings display, string? profileName)
    {
        return !string.IsNullOrWhiteSpace(profileName)
            && display.GamingProfiles.Any(profile => NamesEqual(profile.Name, profileName));
    }

    private static DisplayLayoutProfile? FindProfile(DisplaySettings display, string? profileName)
    {
        return string.IsNullOrWhiteSpace(profileName)
            ? null
            : display.GamingProfiles.FirstOrDefault(profile => NamesEqual(profile.Name, profileName));
    }

    private static DisplayLayoutProfile? FindProfileByNameOrId(DisplaySettings display, string? profileReference)
    {
        return string.IsNullOrWhiteSpace(profileReference)
            ? null
            : display.GamingProfiles.FirstOrDefault(profile =>
                NamesEqual(profile.Name, profileReference) || NamesEqual(profile.Id, profileReference));
    }

    private static GamingProfileConfiguration? FindConfiguration(DisplaySettings display, string? profileName)
    {
        return string.IsNullOrWhiteSpace(profileName)
            ? null
            : display.GamingProfileConfigurations.FirstOrDefault(configuration => NamesEqual(configuration.ProfileName, profileName));
    }

    private static DisplayLayoutSnapshot ResolveProfileLayout(DisplayLayoutProfile profile)
    {
        return profile.DisplayTopology is not null
            ? GamingProfileDisplayTopologyAdapter.ToLegacy(profile.DisplayTopology)
            : GamingProfileDisplayTopologyAdapter.ToLegacy(
                GamingProfileDisplayTopologyAdapter.FromLegacy(profile.Layout));
    }

    private static bool NamesEqual(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
