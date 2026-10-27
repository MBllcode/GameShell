using GameShell.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Runtime;

public sealed class StartupTargetResolver
{
    private readonly ILogger<StartupTargetResolver>? _logger;

    public StartupTargetResolver(ILogger<StartupTargetResolver>? logger = null)
    {
        _logger = logger;
    }

    public StartupTarget Resolve(AppSettings settings, RuntimeState runtimeState, bool safeMode)
    {
        if (safeMode)
        {
            return StartupTarget.Work("SafeMode");
        }

        return settings.Startup.StartupBehavior switch
        {
            StartupBehavior.AlwaysProfile => ResolveConfiguredProfile(settings),
            StartupBehavior.RestoreLastActiveMode => ResolveLastActive(settings, runtimeState),
            _ => StartupTarget.Work("AlwaysWorkMode")
        };
    }

    private StartupTarget ResolveConfiguredProfile(AppSettings settings)
    {
        var configuredProfileId = settings.Startup.StartupProfileId?.Trim();
        var profile = FindProfile(settings, configuredProfileId);
        if (profile is not null)
        {
            return StartupTarget.Profile(GetStableProfileId(profile), "AlwaysProfile");
        }

        _logger?.LogWarning(
            "[STARTUP TARGET] Behavior={Behavior} ConfiguredProfileId={ConfiguredProfileId} Result=FallbackToWork Reason=ProfileUnavailable",
            StartupBehavior.AlwaysProfile,
            configuredProfileId ?? "null");
        return StartupTarget.Work("ProfileUnavailable");
    }

    private static StartupTarget ResolveLastActive(AppSettings settings, RuntimeState runtimeState)
    {
        if (string.Equals(runtimeState.LastActiveMode, "Profile", StringComparison.OrdinalIgnoreCase)
            || string.Equals(runtimeState.LastActiveMode, "Console", StringComparison.OrdinalIgnoreCase))
        {
            var profile = FindProfile(settings, runtimeState.LastActiveProfileId)
                ?? (string.IsNullOrWhiteSpace(runtimeState.LastActiveProfileId)
                    ? FindProfile(settings, settings.Display.ActiveGamingProfileName)
                    : null);
            if (profile is not null)
            {
                return StartupTarget.Profile(GetStableProfileId(profile), "RestoreLastActiveMode");
            }
        }

        return StartupTarget.Work("RestoreLastActiveMode");
    }

    public string? ResolveProfileName(AppSettings settings, string? profileId)
    {
        return FindProfile(settings, profileId)?.Name;
    }

    private static DisplayLayoutProfile? FindProfile(AppSettings settings, string? profileReference)
    {
        if (string.IsNullOrWhiteSpace(profileReference))
        {
            return null;
        }

        profileReference = profileReference.Trim();

        return settings.Display.GamingProfiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, profileReference, StringComparison.OrdinalIgnoreCase))
            ?? settings.Display.GamingProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Name, profileReference, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetStableProfileId(DisplayLayoutProfile profile) =>
        string.IsNullOrWhiteSpace(profile.Id) ? profile.Name : profile.Id;
}
