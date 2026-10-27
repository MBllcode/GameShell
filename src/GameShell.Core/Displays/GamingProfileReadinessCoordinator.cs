using GameShell.Core.Profiles;
using GameShell.Core.Runtime;
using GameShell.Core.Settings;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Displays;

/// <summary>
/// Coordinates profile readiness checks and durable healthy capability updates.
/// It does not perform display I/O itself.
/// </summary>
public sealed class GamingProfileReadinessCoordinator
{
    private static readonly TimeSpan DisplaySettlingReadinessTimeout = TimeSpan.FromSeconds(3);
    private readonly ISettingsService _settingsService;
    private readonly ProfileSettingsResolver _profileResolver;
    private readonly IGamingProfileTopologyReadinessEvaluator? _evaluator;
    private readonly IDisplayDiscoveryStabilizationService? _displayDiscoveryStabilizationService;
    private readonly ILogger _logger;

    public GamingProfileReadinessCoordinator(
        ISettingsService settingsService,
        ProfileSettingsResolver profileResolver,
        IGamingProfileTopologyReadinessEvaluator? evaluator,
        ILogger logger,
        IDisplayDiscoveryStabilizationService? displayDiscoveryStabilizationService = null)
    {
        _settingsService = settingsService;
        _profileResolver = profileResolver;
        _evaluator = evaluator;
        _logger = logger;
        _displayDiscoveryStabilizationService = displayDiscoveryStabilizationService;
    }

    public async Task<ModeResult?> CheckBeforeTransitionAsync(
        AppSettings settings,
        string? requestedProfileName,
        CancellationToken cancellationToken)
    {
        var preparation = await PrepareBeforeTransitionAsync(settings, requestedProfileName, cancellationToken);
        return preparation.BlockedResult;
    }

    public async Task<GamingProfileTransitionPreparation> PrepareBeforeTransitionAsync(
        AppSettings settings,
        string? requestedProfileName,
        CancellationToken cancellationToken)
    {
        var profile = ResolveProfileWithTopology(settings, requestedProfileName);
        if (_evaluator is null || profile is null)
        {
            return new GamingProfileTransitionPreparation
            {
                ProfileName = profile?.Name ?? requestedProfileName ?? string.Empty,
                TargetLayout = profile is null
                    ? _profileResolver.ResolveLayout(settings.Display, requestedProfileName)
                    : GamingProfileDisplayTopologyAdapter.ToLegacy(profile.DisplayTopology!)
            };
        }

        if (_displayDiscoveryStabilizationService?.State == DisplayDiscoveryState.Settling)
        {
            _logger.LogInformation("[DISPLAY READINESS WAIT] Profile={Profile} Reason=DisplayDiscoverySettling", profile.Name);
            try
            {
                await _displayDiscoveryStabilizationService.WaitForStableAsync(
                    DisplaySettlingReadinessTimeout,
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning(
                    "[DISPLAY READINESS WAIT] Profile={Profile} Result=Timeout; continuing with normal readiness evaluation.",
                    profile.Name);
            }
        }

        await EnsureCapabilityCatalogMigratedAsync(settings, cancellationToken);

        var readiness = await _evaluator.EvaluateAsync(
            profile.Name,
            profile.Name,
            profile.DisplayTopology!,
            cancellationToken,
            deferInactiveModeValidation: true,
            displaySettings: settings.Display);
        if (readiness.IdentityCatalogChanged)
        {
            await _settingsService.SaveAsync(settings, cancellationToken);
        }
        _logger.LogInformation(
            "[PROFILE TOPOLOGY PRECHECK] Profile={Profile} Result={Result}",
            profile.Name,
            readiness.ProfileResult);

        if (!readiness.IsReady)
        {
            var blockedTarget = FindBlockedTarget(readiness);
            return new GamingProfileTransitionPreparation
            {
                ProfileName = profile.Name,
                BlockedResult = new ModeResult
                {
                    Status = ModeResultStatus.InvalidConfiguration,
                    CurrentState = ModeState.Error,
                    Message = BuildPrecheckMessage(blockedTarget)
                }
            };
        }

        var targetLayout = BuildResolvedTargetLayout(readiness);
        var expectedDisplayCount = profile.DisplayTopology!.Displays.Count(target => target.Enabled);
        if (targetLayout.Displays.Count != expectedDisplayCount)
        {
            _logger.LogError(
                "[DISPLAY TRANSITION INVARIANT] TargetTopologyMismatch Profile={Profile} ExpectedDisplayCount={ExpectedDisplayCount} ActualDisplayCount={ActualDisplayCount} TargetDisplays={TargetDisplays}",
                profile.Name,
                expectedDisplayCount,
                targetLayout.Displays.Count,
                FormatDisplays(targetLayout));
            return new GamingProfileTransitionPreparation
            {
                ProfileName = profile.Name,
                BlockedResult = new ModeResult
                {
                    Status = ModeResultStatus.Failed,
                    CurrentState = ModeState.Error,
                    Message = "TargetTopologyMismatch"
                }
            };
        }

        return new GamingProfileTransitionPreparation
        {
            ProfileName = profile.Name,
            TargetLayout = targetLayout,
            ExpectedDisplayCount = expectedDisplayCount,
            Readiness = readiness
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
        if (readiness.IdentityCatalogChanged)
        {
            await _settingsService.SaveAsync(settings, cancellationToken);
        }
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

    private static DisplayLayoutSnapshot BuildResolvedTargetLayout(
        GamingProfileTopologyReadiness readiness)
    {
        var displays = readiness.Targets
            .Where(target => target.SavedTarget.Enabled)
            .Select(target => new DisplayPathSnapshot
            {
                KnownPhysicalDisplayId = target.SavedTarget.KnownPhysicalDisplayId,
                Identity = target.ResolvedTarget?.Identity ?? target.SavedTarget.Identity,
                IsActive = true,
                IsPrimary = target.SavedTarget.IsPrimary,
                PositionX = target.SavedTarget.PositionX,
                PositionY = target.SavedTarget.PositionY,
                Width = target.SavedTarget.Width,
                Height = target.SavedTarget.Height,
                Orientation = target.SavedTarget.Orientation,
                RefreshRateNumerator = target.SavedTarget.EffectiveRefreshPolicy == RefreshPolicy.Fixed
                    ? target.SavedTarget.EffectiveFixedRefreshRateNumerator
                    : 0,
                RefreshRateDenominator = target.SavedTarget.EffectiveRefreshPolicy == RefreshPolicy.Fixed
                    ? target.SavedTarget.EffectiveFixedRefreshRateDenominator
                    : 0,
                SourceId = target.ResolvedTarget?.SourceId ?? 0,
                TargetId = target.ResolvedTarget?.Identity.TargetId ?? target.SavedTarget.Identity.TargetId,
                AdapterLuid = target.ResolvedTarget?.Identity.AdapterLuid ?? target.SavedTarget.Identity.AdapterLuid,
                SourceDeviceName = target.ResolvedTarget?.SourceDeviceName ?? string.Empty,
                ConnectionTechnology = target.ResolvedTarget?.ConnectionTechnology ?? string.Empty
            })
            .ToList();

        return DisplayLayoutTopologyNormalizer.NormalizePrimaryOrigin(new DisplayLayoutSnapshot
        {
            Displays = displays
        });
    }

    private static string FormatDisplays(DisplayLayoutSnapshot layout)
    {
        return string.Join(", ", layout.Displays.Select(display =>
            $"{display.Identity.FriendlyName}({display.Identity.AdapterLuid}:{display.Identity.TargetId})"));
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
            _ => "Nie można uruchomić profilu, ponieważ wymagany ekran nie jest obecnie gotowy."
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

public sealed class GamingProfileTransitionPreparation
{
    public string ProfileName { get; init; } = string.Empty;
    public DisplayLayoutSnapshot? TargetLayout { get; init; }
    public int ExpectedDisplayCount { get; init; }
    public GamingProfileTopologyReadiness? Readiness { get; init; }
    public ModeResult? BlockedResult { get; init; }
}
