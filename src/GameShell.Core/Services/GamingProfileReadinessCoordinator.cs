using System.Globalization;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Services;

/// <summary>
/// Coordinates profile readiness checks and durable healthy capability updates.
/// It does not perform display I/O itself.
/// </summary>
public sealed class GamingProfileReadinessCoordinator
{
    private readonly ISettingsService _settingsService;
    private readonly GamingProfileRuntimeSettingsResolver _profileResolver;
    private readonly IGamingProfileTopologyReadinessEvaluator? _evaluator;
    private readonly ILogger _logger;

    public GamingProfileReadinessCoordinator(
        ISettingsService settingsService,
        GamingProfileRuntimeSettingsResolver profileResolver,
        IGamingProfileTopologyReadinessEvaluator? evaluator,
        ILogger logger)
    {
        _settingsService = settingsService;
        _profileResolver = profileResolver;
        _evaluator = evaluator;
        _logger = logger;
    }

    public async Task<ModeResult?> CheckBeforeTransitionAsync(
        AppSettings settings,
        string? requestedProfileName,
        CancellationToken cancellationToken)
    {
        var profile = ResolveProfileWithTopology(settings, requestedProfileName);
        if (_evaluator is null || profile is null)
        {
            return null;
        }

        await EnsureCapabilityCatalogMigratedAsync(settings, cancellationToken);

        var readiness = await _evaluator.EvaluateAsync(
            profile.Name,
            profile.Name,
            profile.DisplayTopology!,
            cancellationToken,
            deferInactiveModeValidation: true,
            displaySettings: settings.Display);
        _logger.LogInformation(
            "[PROFILE TOPOLOGY PRECHECK] Profile={Profile} Result={Result}",
            profile.Name,
            readiness.ProfileResult);

        if (readiness.IsReady)
        {
            return null;
        }

        var blockedTarget = FindBlockedTarget(readiness);
        return new ModeResult
        {
            Status = ModeResultStatus.InvalidConfiguration,
            CurrentState = ModeState.Error,
            Message = BuildPrecheckMessage(blockedTarget)
        };
    }

    public async Task<ModeResult?> CheckAfterTransitionAsync(
        AppSettings settings,
        string? requestedProfileName,
        CancellationToken cancellationToken)
    {
        var profile = ResolveProfileWithTopology(settings, requestedProfileName);
        if (_evaluator is null || profile is null)
        {
            return null;
        }

        await EnsureCapabilityCatalogMigratedAsync(settings, cancellationToken);

        var readiness = await _evaluator.EvaluateAsync(
            profile.Name,
            profile.Name,
            profile.DisplayTopology!,
            cancellationToken,
            displaySettings: settings.Display);
        if (readiness.IsReady)
        {
            await UpdateBestKnownCapabilitiesAsync(settings, profile, readiness, cancellationToken);
            return null;
        }

        var blockedTarget = FindBlockedTarget(readiness);
        return new ModeResult
        {
            Status = ModeResultStatus.InvalidConfiguration,
            CurrentState = ModeState.Error,
            Message = BuildReadinessMessage(blockedTarget)
        };
    }

    private DisplayLayoutProfile? ResolveProfileWithTopology(AppSettings settings, string? requestedProfileName)
    {
        var profile = _profileResolver.ResolveProfile(settings.Display, requestedProfileName);
        return profile?.DisplayTopology is null ? null : profile;
    }

    private async Task UpdateBestKnownCapabilitiesAsync(
        AppSettings settings,
        DisplayLayoutProfile profile,
        GamingProfileTopologyReadiness readiness,
        CancellationToken cancellationToken)
    {
        var changed = false;
        var updates = new List<string>();
        foreach (var targetReadiness in readiness.Targets.Where(IsHealthyTargetWithCapability))
        {
            var current = targetReadiness.CurrentCapability!;
            var identity = targetReadiness.ResolvedTarget?.Identity ?? targetReadiness.SavedTarget.Identity;
            changed |= KnownPhysicalDisplayCatalogService.RecordHealthyObservation(
                settings.Display,
                identity,
                current,
                out var update);
            updates.Add($"{FormatPhysicalId(identity)}:{update.Action}:{FormatCapability(update.PreviousBest)}->{FormatCapability(update.NewBest)}");
            _logger.LogInformation(
                "[DISPLAY CAPABILITY CATALOG] PhysicalId={PhysicalId} CanonicalName={CanonicalName} PreviousBest={PreviousBest} Observed={Observed} NewBest={NewBest} Action={Action}",
                FormatPhysicalId(identity),
                targetReadiness.SavedTarget.Identity.FriendlyName,
                FormatCapability(update.PreviousBest),
                FormatCapability(current),
                FormatCapability(update.NewBest),
                update.Action);
        }

        if (!changed)
        {
            return;
        }

        await _settingsService.SaveAsync(settings, cancellationToken);
        _logger.LogInformation(
            "[DISPLAY CAPABILITY CATALOG] Profile={Profile} Updates={Updates}",
            profile.Name,
            string.Join(", ", updates));
    }

    private static bool IsHealthyTargetWithCapability(ProfileDisplayTargetReadiness target)
    {
        return target.Result == ProfileDisplayReadiness.Ready
            && target.SavedTarget.Enabled
            && target.ResolvedTarget is { IsEnabled: true }
            && target.CurrentCapability is not null;
    }

    private async Task EnsureCapabilityCatalogMigratedAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        if (KnownPhysicalDisplayCatalogService.MigrateLegacyProfileCapabilities(settings.Display))
        {
            await _settingsService.SaveAsync(settings, cancellationToken);
        }
    }

    private static ProfileDisplayTargetReadiness? FindBlockedTarget(GamingProfileTopologyReadiness readiness)
    {
        return readiness.Targets.FirstOrDefault(target => target.Result is not (
            ProfileDisplayReadiness.Ready
            or ProfileDisplayReadiness.ResolvableInactive
            or ProfileDisplayReadiness.Disabled));
    }

    private static string BuildPrecheckMessage(ProfileDisplayTargetReadiness? blockedTarget)
    {
        return blockedTarget?.Reason switch
        {
            ProfileDisplayReadinessReason.ModeInventoryInvalid => "Nie moÅ¼na uruchomiÄ‡ profilu, poniewaÅ¼ lista trybÃ³w ekranu jest niespÃ³jna.",
            ProfileDisplayReadinessReason.InvalidExpectedMode => "Nie moÅ¼na uruchomiÄ‡ profilu, poniewaÅ¼ zapisana czÄ™stotliwoÅ›Ä‡ odÅ›wieÅ¼ania ekranu jest nieprawidÅ‚owa.",
            ProfileDisplayReadinessReason.ExpectedModeUnavailable => $"Ekran zostaÅ‚ wykryty, ale zapisany tryb profilu nie jest obecnie dostÄ™pny. ({blockedTarget.SavedTarget.Width} Ã— {blockedTarget.SavedTarget.Height} @ {FormatRefreshRate(blockedTarget.SavedTarget)} Hz)",
            _ => "Nie moÅ¼na uruchomiÄ‡ profilu, poniewaÅ¼ wymagany ekran nie jest obecnie gotowy."
        };
    }

    private static string BuildReadinessMessage(ProfileDisplayTargetReadiness? blockedTarget)
    {
        return blockedTarget?.Reason is ProfileDisplayReadinessReason.ExpectedModeUnavailable
            ? $"Ekran zostaÅ‚ aktywowany, ale zapisany tryb profilu nie jest obecnie dostÄ™pny. ({blockedTarget.SavedTarget.Width} Ã— {blockedTarget.SavedTarget.Height} @ {FormatRefreshRate(blockedTarget.SavedTarget)} Hz)"
            : "Ekran zostaÅ‚ aktywowany, ale nie udaÅ‚o siÄ™ potwierdziÄ‡ wymaganego trybu.";
    }



    private static string FormatCapability(DisplayCapabilitySnapshot? capability)
    {
        return capability is null || capability.Width <= 0 || capability.Height <= 0 || capability.MaxRefreshRateHz <= 0
            ? "none"
            : $"{capability.Width}x{capability.Height}@{capability.MaxRefreshRateHz:0.##}";
    }

    private static string FormatPhysicalId(DisplayIdentity identity)
    {
        return identity.AdapterLuid != 0 && identity.TargetId != 0
            ? $"{identity.AdapterLuid}:{identity.TargetId}"
            : string.IsNullOrWhiteSpace(identity.MonitorDevicePath) ? "fallback" : identity.MonitorDevicePath;
    }


    private static string FormatRefreshRate(ProfileDisplayTarget target)
    {
        return target.RefreshRateDenominator == 0
            ? "?"
            : ((double)target.RefreshRateNumerator / target.RefreshRateDenominator).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
