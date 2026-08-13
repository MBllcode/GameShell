using System.Globalization;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Services;

/// <summary>
/// Coordinates profile readiness checks, bounded recovery and durable healthy
/// capability updates. It does not perform display I/O itself.
/// </summary>
public sealed class GamingProfileReadinessCoordinator
{
    private readonly ISettingsService _settingsService;
    private readonly GamingProfileRuntimeSettingsResolver _profileResolver;
    private readonly IGamingProfileTopologyReadinessEvaluator? _evaluator;
    private readonly IDisplayRecoveryService? _displayRecoveryService;
    private readonly ILogger _logger;

    public GamingProfileReadinessCoordinator(
        ISettingsService settingsService,
        GamingProfileRuntimeSettingsResolver profileResolver,
        IGamingProfileTopologyReadinessEvaluator? evaluator,
        IDisplayRecoveryService? displayRecoveryService,
        ILogger logger)
    {
        _settingsService = settingsService;
        _profileResolver = profileResolver;
        _evaluator = evaluator;
        _displayRecoveryService = displayRecoveryService;
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

        var readiness = await _evaluator.EvaluateAsync(
            profile.Name,
            profile.Name,
            profile.DisplayTopology!,
            cancellationToken,
            deferInactiveModeValidation: true);
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

        var readiness = await _evaluator.EvaluateAsync(
            profile.Name,
            profile.Name,
            profile.DisplayTopology!,
            cancellationToken);
        if (!readiness.IsReady && _displayRecoveryService is not null)
        {
            readiness = await AttemptDisplayRecoveryAsync(profile, readiness, cancellationToken);
        }

        if (readiness.IsReady)
        {
            await UpdateLastKnownGoodCapabilitiesAsync(settings, profile, readiness, cancellationToken);
            return null;
        }

        var blockedTarget = FindBlockedTarget(readiness);
        return new ModeResult
        {
            Status = ModeResultStatus.InvalidConfiguration,
            CurrentState = ModeState.Error,
            Message = blockedTarget?.Reason is ProfileDisplayReadinessReason.ExpectedModeUnavailable
                ? $"Ekran zostaÅ‚ aktywowany, ale zapisany tryb profilu nie jest obecnie dostÄ™pny. ({blockedTarget.SavedTarget.Width} Ã— {blockedTarget.SavedTarget.Height} @ {FormatRefreshRate(blockedTarget.SavedTarget)} Hz)"
                : "Ekran zostaÅ‚ aktywowany, ale nie udaÅ‚o siÄ™ potwierdziÄ‡ wymaganego trybu."
        };
    }

    private DisplayLayoutProfile? ResolveProfileWithTopology(AppSettings settings, string? requestedProfileName)
    {
        var profile = _profileResolver.ResolveProfile(settings.Display, requestedProfileName);
        return profile?.DisplayTopology is null ? null : profile;
    }

    private async Task<GamingProfileTopologyReadiness> AttemptDisplayRecoveryAsync(
        DisplayLayoutProfile profile,
        GamingProfileTopologyReadiness readiness,
        CancellationToken cancellationToken)
    {
        foreach (var targetReadiness in readiness.Targets.Where(IsRecoverableTarget))
        {
            var resolvedTarget = targetReadiness.ResolvedTarget!;
            var recovery = await _displayRecoveryService!.RecoverAsync(
                profile.Name,
                targetReadiness.SavedTarget,
                resolvedTarget,
                targetReadiness.AvailableModes,
                cancellationToken);

            var refreshedReadiness = await _evaluator!.EvaluateAsync(
                profile.Name,
                profile.Name,
                profile.DisplayTopology!,
                cancellationToken);
            var refreshedTarget = FindMatchingReadiness(refreshedReadiness, targetReadiness.SavedTarget);
            LogRecovery(profile, targetReadiness, resolvedTarget, recovery, refreshedTarget);

            readiness = refreshedReadiness;
            if (readiness.IsReady)
            {
                _logger.LogInformation(
                    "[DISPLAY RECOVERY] Profile={Profile} Recovery restored all requested display modes.",
                    profile.Name);
                return readiness;
            }
        }

        return readiness;
    }

    private async Task UpdateLastKnownGoodCapabilitiesAsync(
        AppSettings settings,
        DisplayLayoutProfile profile,
        GamingProfileTopologyReadiness readiness,
        CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var targetReadiness in readiness.Targets.Where(IsHealthyTargetWithCapability))
        {
            var current = targetReadiness.CurrentCapability!;
            var saved = targetReadiness.SavedTarget.CapabilityExpectation;
            if (ShouldKeepSavedCapability(saved, current))
            {
                continue;
            }

            targetReadiness.SavedTarget.CapabilityExpectation = new DisplayCapabilityExpectation
            {
                Width = current.Width,
                Height = current.Height,
                LastKnownGoodMaxRefreshNumerator = current.LastKnownGoodMaxRefreshNumerator,
                LastKnownGoodMaxRefreshDenominator = current.LastKnownGoodMaxRefreshDenominator
            };
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        await _settingsService.SaveAsync(settings, cancellationToken);
        _logger.LogInformation(
            "[DISPLAY CAPABILITY EXPECTATION UPDATED] Profile={Profile} Capabilities={Capabilities}",
            profile.Name,
            string.Join(", ", readiness.Targets
                .Where(target => target.SavedTarget.CapabilityExpectation is not null)
                .Select(target => $"{target.SavedTarget.Identity.FriendlyName}={FormatCapability(target.SavedTarget.CapabilityExpectation)}")));
    }

    private static bool IsRecoverableTarget(ProfileDisplayTargetReadiness target)
    {
        return target.SavedTarget.Enabled
            && target.Result == ProfileDisplayReadiness.Degraded
            && target.Reason is ProfileDisplayReadinessReason.ExpectedModeUnavailable or ProfileDisplayReadinessReason.CapabilityRegression
            && target.ResolvedTarget is { IsEnabled: true };
    }

    private static bool IsHealthyTargetWithCapability(ProfileDisplayTargetReadiness target)
    {
        return target.Result == ProfileDisplayReadiness.Ready
            && target.SavedTarget.Enabled
            && target.CurrentCapability is not null;
    }

    private static bool ShouldKeepSavedCapability(
        DisplayCapabilityExpectation? saved,
        DisplayCapabilityExpectation current)
    {
        if (saved is null || saved.Width != current.Width || saved.Height != current.Height)
        {
            return false;
        }

        return current.MaxRefreshRateHz <= saved.MaxRefreshRateHz + 0.25;
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

    private void LogRecovery(
        DisplayLayoutProfile profile,
        ProfileDisplayTargetReadiness before,
        DisplayDeviceInfo resolvedTarget,
        DisplayRecoveryAttempt recovery,
        ProfileDisplayTargetReadiness? after)
    {
        _logger.LogInformation(
            "[DISPLAY RECOVERY RUNTIME]\nProfile={Profile}\nSavedDisplay={SavedDisplay}\nResolvedDisplay={ResolvedDisplay}\nMonitorDevicePath={MonitorDevicePath}\nDevNodeInstanceId={DevNodeInstanceId}\nRequestedMode={RequestedMode}\nBeforeModesForRequestedResolution={BeforeModes}\nCM_Locate_DevNode_Result={LocateResult}\nCM_Reenumerate_DevNode_Result={ReenumerateResult}\nWaitMs={WaitMs}\nAfterResolvedDisplay={AfterResolvedDisplay}\nAfterModesForRequestedResolution={AfterModes}\nReadinessBefore={ReadinessBefore}\nReadinessAfter={ReadinessAfter}\nRecoveryResult={RecoveryResult}",
            profile.Name,
            before.SavedTarget.Identity.FriendlyName,
            resolvedTarget.Name,
            resolvedTarget.Identity.MonitorDevicePath,
            string.IsNullOrWhiteSpace(recovery.DevNodeInstanceId) ? "-" : recovery.DevNodeInstanceId,
            FormatMode(before.SavedTarget),
            FormatModes(before.AvailableModes),
            FormatNativeResult(recovery.LocateDevNodeResultCode),
            FormatNativeResult(recovery.ReenumerateDevNodeResultCode),
            recovery.WaitMs,
            after?.ResolvedTarget?.Name ?? "-",
            FormatModes(after?.AvailableModes ?? []),
            FormatReadiness(before),
            FormatReadiness(after),
            recovery.Result);
    }

    private static ProfileDisplayTargetReadiness? FindMatchingReadiness(
        GamingProfileTopologyReadiness readiness,
        ProfileDisplayTarget savedTarget)
    {
        return readiness.Targets.FirstOrDefault(target => ReferenceEquals(target.SavedTarget, savedTarget))
            ?? readiness.Targets.FirstOrDefault(target => string.Equals(
                target.SavedTarget.Identity.MonitorDevicePath,
                savedTarget.Identity.MonitorDevicePath,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatModes(IReadOnlyList<DisplayModeInfo> modes)
    {
        return $"[{string.Join(", ", modes.Select(mode => $"{mode.Width}x{mode.Height}@{mode.RefreshRateHz:0.##}"))}]";
    }

    private static string FormatCapability(DisplayCapabilityExpectation? capability)
    {
        return capability is null || capability.Width <= 0 || capability.Height <= 0 || capability.MaxRefreshRateHz <= 0
            ? "none"
            : $"{capability.Width}x{capability.Height}@{capability.MaxRefreshRateHz:0.##}";
    }

    private static string FormatMode(ProfileDisplayTarget target)
    {
        return $"{target.Width}x{target.Height}@{FormatRefreshRate(target)}";
    }

    private static string FormatReadiness(ProfileDisplayTargetReadiness? target)
    {
        return target is null ? "Unknown" : $"{target.Result}/{target.Reason}";
    }

    private static string FormatNativeResult(int? resultCode)
    {
        return resultCode?.ToString(CultureInfo.InvariantCulture) ?? "NotAttempted";
    }

    private static string FormatRefreshRate(ProfileDisplayTarget target)
    {
        return target.RefreshRateDenominator == 0
            ? "?"
            : ((double)target.RefreshRateNumerator / target.RefreshRateDenominator).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
