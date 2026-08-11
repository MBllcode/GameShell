using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Services;

public sealed class GamingProfileTopologyReadinessEvaluator
{
    private const int MinimumStableIdentityScore = 35;
    private const double RefreshToleranceHz = 0.25;
    private readonly IDisplayQueryService _displayQueryService;
    private readonly ILogger<GamingProfileTopologyReadinessEvaluator> _logger;

    public GamingProfileTopologyReadinessEvaluator(
        IDisplayQueryService displayQueryService,
        ILogger<GamingProfileTopologyReadinessEvaluator> logger)
    {
        _displayQueryService = displayQueryService;
        _logger = logger;
    }

    public async Task<GamingProfileTopologyReadiness> EvaluateAsync(
        string profileId,
        string profileName,
        GamingProfileDisplayTopology topology,
        CancellationToken cancellationToken = default,
        bool deferInactiveModeValidation = false)
    {
        var inventory = await _displayQueryService.GetAvailableDisplaysAsync(cancellationToken);
        var targets = new List<ProfileDisplayTargetReadiness>(topology.Displays.Count);

        foreach (var savedTarget in topology.Displays)
        {
            var readiness = await EvaluateTargetAsync(savedTarget, inventory, cancellationToken, deferInactiveModeValidation);
            targets.Add(readiness);
            _logger.LogInformation(
                "[{Phase}] ProfileId={ProfileId} ProfileName={ProfileName} SavedName={SavedName} Enabled={Enabled} ResolvedName={ResolvedName} Active={Active} Available={Available} RequestedMode={RequestedMode} AvailableModesForRequestedResolution={AvailableModesForRequestedResolution} Result={Result} Reason={Reason}",
                deferInactiveModeValidation ? "PROFILE TOPOLOGY PRECHECK" : "PROFILE TOPOLOGY POSTCHECK",
                profileId,
                profileName,
                savedTarget.Identity.FriendlyName,
                savedTarget.Enabled,
                readiness.ResolvedTarget?.Name ?? readiness.ResolvedTarget?.Identity.FriendlyName ?? "-",
                readiness.ResolvedTarget?.IsEnabled ?? false,
                readiness.ResolvedTarget?.IsAvailable ?? false,
                FormatMode(savedTarget),
                readiness.AvailableModesForRequestedResolution,
                readiness.Result,
                readiness.Reason);
        }

        var result = new GamingProfileTopologyReadiness
        {
            ProfileId = profileId,
            ProfileName = profileName,
            Targets = targets
        };
        _logger.LogInformation(
            "[{Phase}] ProfileId={ProfileId} ProfileName={ProfileName} ProfileResult={ProfileResult}",
            deferInactiveModeValidation ? "PROFILE TOPOLOGY PRECHECK" : "PROFILE TOPOLOGY POSTCHECK",
            profileId,
            profileName,
            result.ProfileResult);
        return result;
    }

    private async Task<ProfileDisplayTargetReadiness> EvaluateTargetAsync(
        ProfileDisplayTarget savedTarget,
        IReadOnlyList<DisplayDeviceInfo> inventory,
        CancellationToken cancellationToken,
        bool deferInactiveModeValidation)
    {
        if (!savedTarget.Enabled)
        {
            return new ProfileDisplayTargetReadiness
            {
                SavedTarget = savedTarget,
                Result = ProfileDisplayReadiness.Disabled,
                Reason = ProfileDisplayReadinessReason.None
            };
        }

        if (savedTarget.Width <= 0 || savedTarget.Height <= 0)
        {
            return new ProfileDisplayTargetReadiness
            {
                SavedTarget = savedTarget,
                Result = ProfileDisplayReadiness.InvalidConfiguration,
                Reason = ProfileDisplayReadinessReason.InvalidExpectedMode
            };
        }

        if (savedTarget.EffectiveRefreshPolicy == RefreshPolicy.Fixed && !IsValidFixedRefresh(savedTarget))
        {
            return new ProfileDisplayTargetReadiness
            {
                SavedTarget = savedTarget,
                Result = ProfileDisplayReadiness.InvalidConfiguration,
                Reason = ProfileDisplayReadinessReason.InvalidExpectedMode
            };
        }

        var resolution = Resolve(savedTarget.Identity, inventory);
        if (resolution.IsAmbiguous)
        {
            return new ProfileDisplayTargetReadiness
            {
                SavedTarget = savedTarget,
                Result = ProfileDisplayReadiness.Ambiguous,
                Reason = ProfileDisplayReadinessReason.AmbiguousIdentity
            };
        }

        if (resolution.Target is null)
        {
            return new ProfileDisplayTargetReadiness
            {
                SavedTarget = savedTarget,
                Result = ProfileDisplayReadiness.Missing,
                Reason = ProfileDisplayReadinessReason.PhysicalTargetMissing
            };
        }

        if (!resolution.Target.IsAvailable)
        {
            return new ProfileDisplayTargetReadiness
            {
                SavedTarget = savedTarget,
                ResolvedTarget = resolution.Target,
                Result = ProfileDisplayReadiness.Missing,
                Reason = ProfileDisplayReadinessReason.PhysicalTargetMissing
            };
        }

        if (deferInactiveModeValidation && !resolution.Target.IsEnabled && resolution.Target.IsAvailable)
        {
            return new ProfileDisplayTargetReadiness
            {
                SavedTarget = savedTarget,
                ResolvedTarget = resolution.Target,
                Result = ProfileDisplayReadiness.ResolvableInactive,
                Reason = ProfileDisplayReadinessReason.TargetInactive
            };
        }

        if (deferInactiveModeValidation && savedTarget.EffectiveRefreshPolicy == RefreshPolicy.AutoHighest)
        {
            return new ProfileDisplayTargetReadiness
            {
                SavedTarget = savedTarget,
                ResolvedTarget = resolution.Target,
                Result = ProfileDisplayReadiness.Ready,
                Reason = ProfileDisplayReadinessReason.None
            };
        }

        var modeQuery = await _displayQueryService.GetAvailableDisplayModesAsync(resolution.Target.Identity, cancellationToken);
        if (!modeQuery.InventoryValid)
        {
            return new ProfileDisplayTargetReadiness
            {
                SavedTarget = savedTarget,
                ResolvedTarget = resolution.Target,
                AvailableModes = modeQuery.Modes,
                AvailableModesForRequestedResolution = modeQuery.Modes.Count(mode => mode.Width == savedTarget.Width && mode.Height == savedTarget.Height),
                Result = ProfileDisplayReadiness.InvalidConfiguration,
                Reason = ProfileDisplayReadinessReason.ModeInventoryInvalid
            };
        }
        if (!modeQuery.Success)
        {
            return new ProfileDisplayTargetReadiness
            {
                SavedTarget = savedTarget,
                ResolvedTarget = resolution.Target,
                AvailableModes = [],
                Result = ProfileDisplayReadiness.Degraded,
                Reason = ProfileDisplayReadinessReason.ModeInformationUnavailable
            };
        }

        var matchingResolutionModes = modeQuery.Modes
            .Where(mode => mode.Width == savedTarget.Width && mode.Height == savedTarget.Height)
            .ToList();
        var hasRequestedMode = savedTarget.EffectiveRefreshPolicy == RefreshPolicy.AutoHighest
            ? matchingResolutionModes.Count > 0
            : IsValidFixedRefresh(savedTarget)
                && matchingResolutionModes.Any(mode => RefreshMatches(
                    mode.RefreshRateNumerator,
                    mode.RefreshRateDenominator,
                    savedTarget.EffectiveFixedRefreshRateNumerator,
                    savedTarget.EffectiveFixedRefreshRateDenominator));

        return new ProfileDisplayTargetReadiness
        {
            SavedTarget = savedTarget,
            ResolvedTarget = resolution.Target,
            AvailableModes = matchingResolutionModes,
            AvailableModesForRequestedResolution = matchingResolutionModes.Count,
            Result = hasRequestedMode ? ProfileDisplayReadiness.Ready : ProfileDisplayReadiness.Degraded,
            Reason = hasRequestedMode
                ? ProfileDisplayReadinessReason.None
                : ProfileDisplayReadinessReason.ExpectedModeUnavailable
        };
    }

    private static IdentityResolution Resolve(
        DisplayIdentity saved,
        IReadOnlyList<DisplayDeviceInfo> inventory)
    {
        var candidates = inventory
            .Select(device => new { Device = device, Score = DisplayIdentityMatcher.GetStableIdentityScore(saved, device.Identity) })
            .Where(candidate => candidate.Score >= MinimumStableIdentityScore)
            .OrderByDescending(candidate => candidate.Score)
            .ToList();
        if (candidates.Count == 0)
        {
            return new IdentityResolution();
        }

        var highestScore = candidates[0].Score;
        var highest = candidates.Where(candidate => candidate.Score == highestScore).ToList();
        return highest.Count == 1
            ? new IdentityResolution(highest[0].Device, false)
            : new IdentityResolution(null, true);
    }

    private static bool RefreshMatches(uint leftNumerator, uint leftDenominator, uint rightNumerator, uint rightDenominator)
    {
        if (leftDenominator == 0 || rightDenominator == 0)
        {
            return false;
        }

        var left = (double)leftNumerator / leftDenominator;
        var right = (double)rightNumerator / rightDenominator;
        return Math.Abs(left - right) <= RefreshToleranceHz;
    }

    private static bool IsValidFixedRefresh(ProfileDisplayTarget target)
    {
        return target.EffectiveFixedRefreshRateNumerator > 0
            && target.EffectiveFixedRefreshRateDenominator > 0
            && double.IsFinite((double)target.EffectiveFixedRefreshRateNumerator / target.EffectiveFixedRefreshRateDenominator);
    }

    private static string FormatMode(ProfileDisplayTarget target)
    {
        var numerator = target.EffectiveRefreshPolicy == RefreshPolicy.Fixed ? target.EffectiveFixedRefreshRateNumerator : 0;
        var denominator = target.EffectiveRefreshPolicy == RefreshPolicy.Fixed ? target.EffectiveFixedRefreshRateDenominator : 0;
        var refresh = denominator == 0
            ? "?"
            : ((double)numerator / denominator).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return $"{target.Width}x{target.Height}@{refresh} Hz";
    }

    private readonly record struct IdentityResolution(DisplayDeviceInfo? Target = null, bool IsAmbiguous = false);
}
