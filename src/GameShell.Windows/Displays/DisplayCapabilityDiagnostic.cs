using GameShell.Core.Displays;
using GameShell.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Displays;

public interface IDisplayCapabilityDiagnostic
{
    Task RunAsync(
        string profileName = "CONSOLE",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Passive display capability inspection. It only queries Windows and the
/// persisted catalog; it never changes display, device, TV, or GPU state.
/// </summary>
public sealed class DisplayCapabilityDiagnostic : IDisplayCapabilityDiagnostic
{
    private readonly ISettingsService _settingsService;
    private readonly IDisplayQueryService _displayQueryService;
    private readonly ILogger<DisplayCapabilityDiagnostic> _logger;

    public DisplayCapabilityDiagnostic(
        ISettingsService settingsService,
        IDisplayQueryService displayQueryService,
        ILogger<DisplayCapabilityDiagnostic> logger)
    {
        _settingsService = settingsService;
        _displayQueryService = displayQueryService;
        _logger = logger;
    }

    public async Task RunAsync(
        string profileName = "CONSOLE",
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        var profile = settings.Display.GamingProfiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (profile?.DisplayTopology is null)
        {
            _logger.LogWarning(
                "[DISPLAY CAPABILITY DIAGNOSTIC] Profile={Profile} Result=ProfileNotFound",
                profileName);
            return;
        }

        var inventory = await _displayQueryService.GetAvailableDisplaysAsync(cancellationToken);
        foreach (var target in profile.DisplayTopology.Displays.Where(display => display.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var known = KnownPhysicalDisplayCatalogService.FindKnownById(
                            settings.Display,
                            target.KnownPhysicalDisplayId)
                        ?? KnownPhysicalDisplayCatalogService.FindKnownByIdentity(settings.Display, target.Identity);
            if (known is null)
            {
                _logger.LogWarning(
                    "[DISPLAY CAPABILITY DIAGNOSTIC] Profile={Profile} SavedName={SavedName} Result=KnownDisplayNotResolved",
                    profile.Name,
                    target.Identity.FriendlyName);
                continue;
            }

            var current = inventory
                .Select(device => new
                {
                    Device = device,
                    Match = string.Equals(device.KnownPhysicalDisplayId, known.Id, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : DisplayIdentityMatcher.ResolveKnownDisplay(known, device.Identity),
                    Score = DisplayIdentityMatcher.GetStableIdentityScore(known, device.Identity)
                })
                .Where(candidate => string.Equals(candidate.Device.KnownPhysicalDisplayId, known.Id, StringComparison.OrdinalIgnoreCase)
                    || candidate.Match?.IsMatch == true)
                .OrderByDescending(candidate => string.Equals(candidate.Device.KnownPhysicalDisplayId, known.Id, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(candidate => candidate.Match?.MatchConfidence ?? DisplayIdentityMatchConfidence.ExactMonitorDevicePath)
                .ThenByDescending(candidate => candidate.Score)
                .Select(candidate => candidate.Device)
                .FirstOrDefault();

            if (current is null)
            {
                _logger.LogInformation(
                    "[DISPLAY CAPABILITY DIAGNOSTIC] Profile={Profile} KnownDisplayId={KnownDisplayId} CanonicalName={CanonicalName} Result=NotCurrentlyObserved",
                    profile.Name,
                    known.Id,
                    ResolveName(known));
                continue;
            }

            var modes = await _displayQueryService.GetAvailableDisplayModesAsync(current.Identity, cancellationToken);
            var requestedModes = modes.Modes
                .Where(mode => mode.Width == target.Width && mode.Height == target.Height)
                .OrderBy(mode => mode.RefreshRateHz)
                .ToList();
            var currentMax = requestedModes.Count == 0 ? 0 : requestedModes.Max(mode => mode.RefreshRateHz);
            var bestKnown = known.BestKnownCapability ?? known.LastVerifiedActiveCapability;
            var belowBestKnown = profile.DisplayTopology is not null
                && target.EffectiveRefreshPolicy == RefreshPolicy.AutoHighest
                && current.IsEnabled
                && bestKnown is not null
                && bestKnown.Width == target.Width
                && bestKnown.Height == target.Height
                && currentMax > 0
                && currentMax + 0.25 < bestKnown.MaxRefreshRateHz;

            _logger.LogInformation(
                "[DISPLAY CAPABILITY DIAGNOSTIC] Profile={Profile} KnownDisplayId={KnownDisplayId} CanonicalName={CanonicalName} ObservedName={ObservedName} Source={Source} AdapterLuid={AdapterLuid} TargetId={TargetId} Active={Active} CurrentMode={CurrentMode} RequestedResolutionRefreshRates=[{Rates}] ModeCount={ModeCount} BestKnownCapability={BestKnown} State={State} BelowBestKnown={BelowBestKnown}",
                profile.Name,
                known.Id,
                ResolveName(known),
                current.Name,
                current.SourceDeviceName,
                current.Identity.AdapterLuid,
                current.Identity.TargetId,
                current.IsEnabled,
                FormatMode(current),
                string.Join(",", requestedModes.Select(mode => mode.RefreshRateHz.ToString("0.##"))),
                modes.Modes.Count,
                FormatCapability(bestKnown),
                current.IsEnabled ? "Active" : current.IsAvailable ? "InactiveObservation" : "Unavailable",
                belowBestKnown);

            if (belowBestKnown)
            {
                _logger.LogInformation(
                    "[DISPLAY CAPABILITY DEGRADATION] KnownDisplayId={KnownDisplayId} CanonicalName={CanonicalName} ObservedName={ObservedName} RequestedResolution={RequestedResolution} BestKnownRefresh={BestKnownRefresh} CurrentMaxRefresh={CurrentMaxRefresh} CurrentModeCount={CurrentModeCount} BestKnownModeCount={BestKnownModeCount} State=DegradedCurrentCapability Action=UseCurrentAvailableMode",
                    known.Id,
                    ResolveName(known),
                    current.Name,
                    $"{target.Width}x{target.Height}",
                    FormatRefresh(bestKnown?.MaxRefreshRateHz ?? 0),
                    FormatRefresh(currentMax),
                    modes.Modes.Count,
                    "unknown");
            }
        }
    }

    private static string ResolveName(KnownPhysicalDisplay known)
    {
        return string.IsNullOrWhiteSpace(known.OptionalUserLabel)
            ? known.CanonicalFriendlyName
            : known.OptionalUserLabel;
    }

    private static string FormatMode(DisplayDeviceInfo display)
    {
        return display.Width > 0 && display.Height > 0 && display.RefreshRateHz > 0
            ? $"{display.Width}x{display.Height}@{display.RefreshRateHz:0.##}"
            : "unknown";
    }

    private static string FormatCapability(DisplayCapabilitySnapshot? capability)
    {
        return capability is null
            ? "none"
            : $"{capability.Width}x{capability.Height}@{capability.MaxRefreshRateHz:0.##}";
    }

    private static string FormatRefresh(double refreshRate)
    {
        return refreshRate > 0 ? $"{refreshRate:0.##}" : "unknown";
    }
}
