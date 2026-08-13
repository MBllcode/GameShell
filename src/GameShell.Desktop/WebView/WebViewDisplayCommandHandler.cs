using System.Text.Json;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using GameShell.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.WebView;

internal sealed class WebViewDisplayCommandHandler
{
    private readonly ISettingsService _settingsService;
    private readonly IDisplayQueryService _displayQueryService;
    private readonly IDisplayTransitionService _displayTransitionService;
    private readonly IWindowsIntegrationService _windowsIntegrationService;
    private readonly ILogger<WebViewDisplayCommandHandler> _logger;
    private readonly Func<AppSettings, Task> _saveAndApplySettingsAsync;

    public WebViewDisplayCommandHandler(
        ISettingsService settingsService,
        IDisplayQueryService displayQueryService,
        IDisplayTransitionService displayTransitionService,
        IWindowsIntegrationService windowsIntegrationService,
        ILogger<WebViewDisplayCommandHandler> logger,
        Func<AppSettings, Task> saveAndApplySettingsAsync)
    {
        _settingsService = settingsService;
        _displayQueryService = displayQueryService;
        _displayTransitionService = displayTransitionService;
        _windowsIntegrationService = windowsIntegrationService;
        _logger = logger;
        _saveAndApplySettingsAsync = saveAndApplySettingsAsync;
    }

    public void ApplyWindowsIntegrationForCurrentSettings(AppSettings settings)
    {
        _windowsIntegrationService.Apply(settings);
    }

    public async Task<IReadOnlyList<DisplayDeviceInfo>> GetDisplaysAsync()
    {
        try
        {
            return await _displayQueryService.GetAvailableDisplaysAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read active displays.");
            throw new InvalidOperationException("Nie udało się odczytać konfiguracji ekranów.");
        }
    }

    public async Task<DisplayModeQueryResult> GetDisplayModesAsync(JsonElement? payload)
    {
        var identity = WebViewPayloadReader.ReadDisplayIdentity(payload);
        return await _displayQueryService.GetAvailableDisplayModesAsync(identity);
    }

    public async Task<DisplayLayoutSnapshot> CaptureDisplayLayoutAsync()
    {
        try
        {
            return await _displayQueryService.CaptureCurrentLayoutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture display layout snapshot.");
            throw new InvalidOperationException("Nie udało się odczytać konfiguracji ekranów.");
        }
    }

    public async Task<AppSettings> SaveConsoleDisplayAsync(JsonElement? payload)
    {
        var identity = WebViewPayloadReader.ReadDisplayIdentity(payload);
        var settings = await _settingsService.LoadAsync();
        settings.Display.ConsoleDisplay = identity;
        settings.Display.ConsoleDisplays = [identity];
        settings.Launcher.TargetDisplayId = identity.MonitorDevicePath;
        await _saveAndApplySettingsAsync(settings);
        return settings;
    }

    public async Task<AppSettings> SaveConsoleDisplaysAsync(JsonElement? payload)
    {
        var identities = WebViewPayloadReader.ReadDisplayIdentities(payload);
        var settings = await _settingsService.LoadAsync();
        settings.Display.ConsoleDisplays = identities;
        settings.Display.ConsoleDisplay = identities[0];
        settings.Launcher.TargetDisplayId = identities[0].MonitorDevicePath;
        await _saveAndApplySettingsAsync(settings);
        return settings;
    }

    public async Task<AppSettings> SaveGamingDisplaysAsync(JsonElement? payload)
    {
        var identities = WebViewPayloadReader.ReadDisplayIdentities(payload);
        var profileName = WebViewPayloadReader.ReadOptionalProfileName(payload);
        var available = await _displayQueryService.GetAvailableDisplaysAsync();
        var maxProfileCount = CalculateMaxProfileCount(available.Count);
        var selected = ResolveSelectedDisplays(identities, available);
        var settings = await _settingsService.LoadAsync();
        var layout = BuildSelectedDisplayLayout(selected);
        UpsertGamingProfile(settings.Display, profileName, layout, maxProfileCount);
        await _saveAndApplySettingsAsync(settings);
        return settings;
    }

    public async Task<AppSettings> CaptureGamingLayoutAsync(JsonElement? payload)
    {
        var profileName = WebViewPayloadReader.ReadOptionalProfileName(payload);
        var available = await _displayQueryService.GetAvailableDisplaysAsync();
        var maxProfileCount = CalculateMaxProfileCount(available.Count);
        var settings = await _settingsService.LoadAsync();
        var layout = await CaptureDisplayLayoutAsync();
        UpsertGamingProfile(settings.Display, profileName, layout, maxProfileCount);
        await _saveAndApplySettingsAsync(settings);
        return settings;
    }

    public async Task<AppSettings> CapturePrimaryGamingLayoutAsync(JsonElement? payload)
    {
        var profileName = WebViewPayloadReader.ReadOptionalProfileName(payload);
        var available = await _displayQueryService.GetAvailableDisplaysAsync();
        var maxProfileCount = CalculateMaxProfileCount(available.Count);
        var currentLayout = await CaptureDisplayLayoutAsync();
        var primaryDisplay = currentLayout.Displays.SingleOrDefault(display => display.IsActive && display.IsPrimary)
            ?? currentLayout.Displays.SingleOrDefault(display => display.IsActive && display.PositionX == 0 && display.PositionY == 0)
            ?? throw new InvalidOperationException("Nie udało się ustalić monitora głównego.");
        var settings = await _settingsService.LoadAsync();
        var layout = new DisplayLayoutSnapshot
        {
            CapturedAt = DateTimeOffset.UtcNow,
            Displays =
            [
                new DisplayPathSnapshot
                {
                    Identity = primaryDisplay.Identity,
                    IsActive = true,
                    IsPrimary = true,
                    PositionX = 0,
                    PositionY = 0,
                    Width = primaryDisplay.Width,
                    Height = primaryDisplay.Height,
                    Orientation = primaryDisplay.Orientation,
                    RefreshRateNumerator = primaryDisplay.RefreshRateNumerator,
                    RefreshRateDenominator = primaryDisplay.RefreshRateDenominator,
                    SourceId = primaryDisplay.SourceId,
                    TargetId = primaryDisplay.TargetId,
                    AdapterLuid = primaryDisplay.AdapterLuid,
                    SourceDeviceName = primaryDisplay.SourceDeviceName,
                    ConnectionTechnology = primaryDisplay.ConnectionTechnology
                }
            ]
        };
        UpsertGamingProfile(settings.Display, profileName, layout, maxProfileCount);
        await _saveAndApplySettingsAsync(settings);
        return settings;
    }

    public async Task<AppSettings> SetActiveGamingProfileAsync(JsonElement? payload)
    {
        var profileName = WebViewPayloadReader.ReadRequiredProfileName(payload);
        var settings = await _settingsService.LoadAsync();
        var profile = settings.Display.GamingProfiles.FirstOrDefault(candidate => string.Equals(candidate.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            throw new InvalidOperationException("Wybrany profil gamingowy nie istnieje.");
        }

        settings.Display.ActiveGamingProfileName = profile.Name;
        settings.Display.GamingLayout = profile.Layout;
        await _saveAndApplySettingsAsync(settings);
        return settings;
    }

    public async Task<AppSettings> ClearGamingLayoutAsync(JsonElement? payload)
    {
        var settings = await _settingsService.LoadAsync();
        var profileName = WebViewPayloadReader.ReadOptionalProfileName(payload);
        if (string.IsNullOrWhiteSpace(profileName))
        {
            settings.Display.GamingLayout = null;
            settings.Display.ActiveGamingProfileName = null;
            settings.Display.GamingProfiles.Clear();
            await _saveAndApplySettingsAsync(settings);
            return settings;
        }

        var index = settings.Display.GamingProfiles.FindIndex(profile => string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new InvalidOperationException("Wybrany profil gamingowy nie istnieje.");
        }

        settings.Display.GamingProfiles.RemoveAt(index);
        var active = settings.Display.GamingProfiles.FirstOrDefault(profile => string.Equals(profile.Name, settings.Display.ActiveGamingProfileName, StringComparison.OrdinalIgnoreCase))
            ?? settings.Display.GamingProfiles.FirstOrDefault();
        settings.Display.ActiveGamingProfileName = active?.Name;
        settings.Display.GamingLayout = active?.Layout;
        await _saveAndApplySettingsAsync(settings);
        return settings;
    }

    public async Task<DisplayTransitionResult> TestConsoleDisplayAsync()
    {
        var settings = await _settingsService.LoadAsync();
        return await _displayTransitionService.TestConsoleDisplayAsync(GetConsoleDisplays(settings));
    }

    public async Task<DisplayTransitionResult> TestGamingLayoutAsync()
    {
        var settings = await _settingsService.LoadAsync();
        return await _displayTransitionService.TestGamingLayoutAsync(settings.Display.GamingLayout);
    }

    private List<DisplayDeviceInfo> ResolveSelectedDisplays(IReadOnlyList<DisplayIdentity> identities, IReadOnlyList<DisplayDeviceInfo> available)
    {
        var selected = new List<DisplayDeviceInfo>(identities.Count);
        foreach (var identity in identities)
        {
            var match = available.FirstOrDefault(display => MatchesSelectedDisplay(identity, display));
            if (match is null)
            {
                throw new InvalidOperationException("Nie wszystkie wybrane ekrany profilu gamingowego są obecnie dostępne.");
            }

            if (selected.Any(existing => IsSameDisplay(existing, match)))
            {
                throw new InvalidOperationException("Wybrano ten sam ekran profilu gamingowego więcej niż raz.");
            }

            selected.Add(match);
        }

        if (selected.Count == 0)
        {
            throw new InvalidOperationException("Wybierz co najmniej jeden ekran profilu gamingowego.");
        }

        return selected;
    }

    private DisplayLayoutSnapshot BuildSelectedDisplayLayout(IReadOnlyList<DisplayDeviceInfo> selected)
    {
        var primary = selected.FirstOrDefault(display => display.IsPrimary) ?? selected[0];
        var placement = selected.Select(display => new DisplayPlacement
        {
            Display = display,
            PositionX = display.PositionX - primary.PositionX,
            PositionY = display.PositionY - primary.PositionY
        }).ToList();

        AssignFallbackPositions(placement, primary);
        return new DisplayLayoutSnapshot
        {
            CapturedAt = DateTimeOffset.UtcNow,
            Displays = placement.Select(item => CreatePathSnapshot(
                item.Display,
                item.PositionX,
                item.PositionY,
                IsSameDisplay(primary, item.Display))).ToList()
        };
    }

    private void AssignFallbackPositions(IReadOnlyList<DisplayPlacement> placement, DisplayDeviceInfo primary)
    {
        if (!placement.Any(item => !item.Display.IsEnabled))
        {
            return;
        }

        _logger.LogInformation("Gaming profile selection contains inactive displays. Assigning deterministic fallback positions for inactive screens.");
        var rightEdge = placement
            .Where(item => item.Display.IsEnabled)
            .Select(item => item.PositionX + ResolveDisplayWidth(item.Display.Width))
            .DefaultIfEmpty(ResolveDisplayWidth(primary.Width))
            .Max();
        var cursorX = Math.Max(rightEdge, ResolveDisplayWidth(primary.Width));
        foreach (var item in placement.Where(item => !item.Display.IsEnabled))
        {
            item.PositionX = cursorX;
            item.PositionY = 0;
            cursorX += ResolveDisplayWidth(item.Display.Width);
        }
    }

    private static DisplayPathSnapshot CreatePathSnapshot(DisplayDeviceInfo display, int positionX, int positionY, bool isPrimary)
    {
        return new DisplayPathSnapshot
        {
            Identity = display.Identity,
            IsActive = true,
            IsPrimary = isPrimary,
            PositionX = positionX,
            PositionY = positionY,
            Width = display.Width,
            Height = display.Height,
            Orientation = display.Orientation,
            RefreshRateNumerator = display.RefreshRateNumerator,
            RefreshRateDenominator = display.RefreshRateDenominator,
            SourceId = display.SourceId,
            TargetId = display.TargetId,
            AdapterLuid = display.AdapterLuid,
            SourceDeviceName = display.SourceDeviceName,
            ConnectionTechnology = display.ConnectionTechnology
        };
    }

    private static bool MatchesSelectedDisplay(DisplayIdentity selected, DisplayDeviceInfo detected)
    {
        if (DisplayIdentityMatcher.IsReliableMatch(selected, detected.Identity))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(selected.MonitorDevicePath)
            && !string.IsNullOrWhiteSpace(detected.Identity.MonitorDevicePath)
            && string.Equals(selected.MonitorDevicePath, detected.Identity.MonitorDevicePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return selected.AdapterLuid != 0
            && detected.AdapterLuid != 0
            && selected.AdapterLuid == detected.AdapterLuid
            && selected.TargetId == detected.TargetId;
    }

    private static bool IsSameDisplay(DisplayDeviceInfo left, DisplayDeviceInfo right)
    {
        if (left.AdapterLuid != 0 && right.AdapterLuid != 0 && left.AdapterLuid == right.AdapterLuid && left.TargetId == right.TargetId)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(left.Identity.MonitorDevicePath)
            && !string.IsNullOrWhiteSpace(right.Identity.MonitorDevicePath)
            && string.Equals(left.Identity.MonitorDevicePath, right.Identity.MonitorDevicePath, StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveDisplayWidth(int width) => width > 0 ? width : 1920;

    private static void UpsertGamingProfile(DisplaySettings displaySettings, string? profileName, DisplayLayoutSnapshot layout, int maxProfileCount)
    {
        displaySettings.GamingLayout = layout;

        if (string.IsNullOrWhiteSpace(profileName))
        {
            if (string.IsNullOrWhiteSpace(displaySettings.ActiveGamingProfileName))
            {
                return;
            }

            var activeProfile = displaySettings.GamingProfiles.FirstOrDefault(profile => string.Equals(profile.Name, displaySettings.ActiveGamingProfileName, StringComparison.OrdinalIgnoreCase));
            if (activeProfile is not null)
            {
                activeProfile.Layout = layout;
            }

            return;
        }

        var existingProfile = displaySettings.GamingProfiles.FirstOrDefault(profile => string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (displaySettings.GamingProfiles.Any(profile => !string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase) && IsSameProfileCombination(profile.Layout, layout)))
        {
            throw new InvalidOperationException("Profil z taką samą kombinacją ekranów już istnieje.");
        }

        if (existingProfile is not null)
        {
            existingProfile.Layout = layout;
            displaySettings.ActiveGamingProfileName = existingProfile.Name;
            return;
        }

        if (maxProfileCount > 0 && displaySettings.GamingProfiles.Count >= maxProfileCount)
        {
            throw new InvalidOperationException($"Osiągnięto limit profili ({maxProfileCount}) wynikający z liczby kombinacji wykrytych monitorów.");
        }

        displaySettings.GamingProfiles.Add(new DisplayLayoutProfile
        {
            Name = profileName,
            Layout = layout
        });
        displaySettings.ActiveGamingProfileName = profileName;
    }

    private static bool IsSameProfileCombination(DisplayLayoutSnapshot left, DisplayLayoutSnapshot right)
    {
        var leftKeys = left.Displays
            .Where(display => display.IsActive)
            .Select(display => BuildIdentityKey(display.Identity))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rightKeys = right.Displays
            .Where(display => display.IsActive)
            .Select(display => BuildIdentityKey(display.Identity))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return leftKeys.SequenceEqual(rightKeys, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildIdentityKey(DisplayIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.MonitorDevicePath))
        {
            return $"path:{identity.MonitorDevicePath.Trim()}";
        }

        if (identity.AdapterLuid != 0 && identity.TargetId != 0)
        {
            return $"target:{identity.AdapterLuid}:{identity.TargetId}";
        }

        return $"edid:{identity.EdidManufacturerId}:{identity.EdidProductCodeId}:{identity.ConnectorInstance}";
    }

    private static int CalculateMaxProfileCount(int displayCount)
    {
        if (displayCount <= 0)
        {
            return 0;
        }

        return displayCount >= 31 ? int.MaxValue : (1 << displayCount) - 1;
    }

    private static IReadOnlyCollection<DisplayIdentity> GetConsoleDisplays(AppSettings settings)
    {
        if (settings.Display.ConsoleDisplays.Count > 0)
        {
            return settings.Display.ConsoleDisplays;
        }

        return settings.Display.ConsoleDisplay is null
            ? []
            : [settings.Display.ConsoleDisplay];
    }

    private sealed class DisplayPlacement
    {
        public required DisplayDeviceInfo Display { get; init; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
    }
}
