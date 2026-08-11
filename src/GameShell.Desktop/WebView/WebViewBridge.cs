using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using GameShell.Desktop.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace GameShell.Desktop.WebView;

public sealed class WebViewBridge
{
    private CoreWebView2? _coreWebView;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly HashSet<string> AllowedCommands = new(StringComparer.Ordinal)
    {
        "getStatus",
        "getSettings",
        "saveSettings",
        "getDependencies",
        "getAudioDevices",
        "getDisplays",
        "getDisplayModes",
        "captureDisplayLayout",
        "saveConsoleDisplay",
        "saveConsoleDisplays",
        "savePcGamingDisplays",
        "saveConsoleAudioDevice",
        "capturePcGamingLayout",
        "capturePrimaryPcGamingLayout",
        "setActivePcGamingProfile",
        "clearPcGamingLayout",
        "testConsoleDisplay",
        "testPcGamingLayout",
        "confirmDisplayTest",
        "restoreDisplayTest",
        "restorePreviousDisplayLayout",
        "openDependencyDownloadPage",
        "selectDependencyPath",
        "setHotkeyCaptureActive",
        "startControllerShortcutCapture",
        "cancelControllerShortcutCapture",
        "acknowledgeControllerShortcutCapture",
        "getControllerStatus",
        "enterGameShell",
        "enterGameShellWithProfile",
        "enterWorkMode"
    };

    private readonly IModeCommandService _modeCommandService;
    private readonly ISettingsService _settingsService;
    private readonly IDependencyDetectionService _dependencyDetectionService;
    private readonly IAudioDeviceService _audioDeviceService;
    private readonly IDisplayQueryService _displayQueryService;
    private readonly IDisplayTransitionService _displayTransitionService;
    private readonly ILogger<WebViewBridge> _logger;
    private readonly Lock _controllerNotificationSync = new();
    private Services.SdlControllerShortcutService? _controllerShortcutService;
    private bool _hasControllerNotification;
    private bool _lastControllerConnected;
    private string? _lastControllerDeviceName;

    public WebViewBridge(
        IModeCommandService modeCommandService,
        ISettingsService settingsService,
        IDependencyDetectionService dependencyDetectionService,
        IAudioDeviceService audioDeviceService,
        IDisplayQueryService displayQueryService,
        IDisplayTransitionService displayTransitionService,
        ILogger<WebViewBridge> logger)
    {
        _modeCommandService = modeCommandService;
        _settingsService = settingsService;
        _dependencyDetectionService = dependencyDetectionService;
        _audioDeviceService = audioDeviceService;
        _displayQueryService = displayQueryService;
        _displayTransitionService = displayTransitionService;
        _logger = logger;
    }

    public void AttachControllerShortcutService(Services.SdlControllerShortcutService controllerShortcutService)
    {
        _controllerShortcutService = controllerShortcutService;
    }

    public event EventHandler? SettingsSaved;
    public event EventHandler<bool>? HotkeyCaptureStateChanged;

    public void Attach(CoreWebView2 coreWebView)
    {
        _coreWebView = coreWebView;
        coreWebView.WebMessageReceived += OnWebMessageReceived;
    }

    public void NotifyStatusChanged(string? transitionId = null)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _logger.LogWarning("[STATUSCHANGED EMIT] Dispatcher unavailable. TransitionId={TransitionId}.", transitionId ?? "unknown");
            return;
        }

        void PostStatusChanged()
        {
            try
            {
                _logger.LogInformation(
                    "[STATUSCHANGED EMIT] Timestamp={Timestamp} TransitionId={TransitionId} ThreadId={ThreadId}.",
                    DateTimeOffset.Now.ToString("O"),
                    transitionId ?? "unknown",
                    Environment.CurrentManagedThreadId);
                _coreWebView?.PostWebMessageAsJson("""{"type":"statusChanged"}""");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify WebView about status change. TransitionId={TransitionId}.", transitionId ?? "unknown");
            }
        }

        try
        {
            if (dispatcher.CheckAccess())
            {
                PostStatusChanged();
            }
            else
            {
                dispatcher.BeginInvoke((Action)PostStatusChanged);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch status change notification. TransitionId={TransitionId}.", transitionId ?? "unknown");
        }
    }

    public void NotifyControllerStatusChanged(long sequenceNumber, bool isConnected, string? deviceName)
    {
        lock (_controllerNotificationSync)
        {
            if (_hasControllerNotification
                && _lastControllerConnected == isConnected
                && string.Equals(_lastControllerDeviceName, deviceName, StringComparison.Ordinal))
            {
                return;
            }

            _hasControllerNotification = true;
            _lastControllerConnected = isConnected;
            _lastControllerDeviceName = deviceName;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _logger.LogError("Cannot send controllerStatusChanged #{SequenceNumber}: WPF dispatcher is unavailable.", sequenceNumber);
            return;
        }

        dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                _logger.LogInformation(
                    "Bridge sending controllerStatusChanged #{SequenceNumber}. Connected={Connected}, Name={Name}, DispatcherThread={ThreadId}.",
                    sequenceNumber,
                    isConnected,
                    deviceName ?? "(none)",
                    Environment.CurrentManagedThreadId);
                _coreWebView?.PostWebMessageAsJson(JsonSerializer.Serialize(new
                {
                    type = "controllerStatusChanged",
                    sequenceNumber
                }, JsonOptions));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify WebView about controller status change #{SequenceNumber}.", sequenceNumber);
            }
        }));
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (sender is not CoreWebView2 coreWebView)
        {
            return;
        }

        WebViewBridgeMessage? message = null;

        try
        {
            message = JsonSerializer.Deserialize<WebViewBridgeMessage>(e.WebMessageAsJson, JsonOptions);

            if (message is null || string.IsNullOrWhiteSpace(message.RequestId))
            {
                await PostResponseAsync(coreWebView, string.Empty, false, null, "Invalid bridge message.");
                return;
            }

            if (!AllowedCommands.Contains(message.Type))
            {
                _logger.LogWarning("Rejected WebView command: {Command}", message.Type);
                await PostResponseAsync(coreWebView, message.RequestId, false, null, "Command is not allowed.");
                return;
            }

            if (message.Type == "enterWorkMode")
            {
                _logger.LogInformation(
                    "[WORKMODE REQUEST] Timestamp={Timestamp} TransitionId={TransitionId}.",
                    DateTimeOffset.Now.ToString("O"),
                    message.RequestId);
            }

            object data = message.Type switch
            {
                "getStatus" => await _modeCommandService.GetStatusAsync(),
                "getSettings" => await _settingsService.LoadAsync(),
                "saveSettings" => await SaveSettingsAsync(message.Payload),
                "getDependencies" => await _dependencyDetectionService.DetectAsync(await _settingsService.LoadAsync()),
                "getAudioDevices" => await GetAudioDevicesAsync(),
                "getDisplays" => await GetDisplaysAsync(),
                "getDisplayModes" => await GetDisplayModesAsync(message.Payload),
                "captureDisplayLayout" => await CaptureDisplayLayoutAsync(),
                "saveConsoleDisplay" => await SaveConsoleDisplayAsync(message.Payload),
                "saveConsoleDisplays" => await SaveConsoleDisplaysAsync(message.Payload),
                "savePcGamingDisplays" => await SavePcGamingDisplaysAsync(message.Payload),
                "saveConsoleAudioDevice" => await SaveConsoleAudioDeviceAsync(message.Payload),
                "capturePcGamingLayout" => await CapturePcGamingLayoutAsync(message.Payload),
                "capturePrimaryPcGamingLayout" => await CapturePrimaryPcGamingLayoutAsync(message.Payload),
                "setActivePcGamingProfile" => await SetActivePcGamingProfileAsync(message.Payload),
                "clearPcGamingLayout" => await ClearPcGamingLayoutAsync(message.Payload),
                "testConsoleDisplay" => await TestConsoleDisplayAsync(),
                "testPcGamingLayout" => await TestPcGamingLayoutAsync(),
                "confirmDisplayTest" => await _displayTransitionService.ConfirmPendingTestAsync(),
                "restoreDisplayTest" => await _displayTransitionService.RestorePendingTestAsync(),
                "restorePreviousDisplayLayout" => await _displayTransitionService.RestorePreviousLayoutAsync(),
                "openDependencyDownloadPage" => await OpenDependencyDownloadPageAsync(message.Payload),
                "selectDependencyPath" => await SelectDependencyPathAsync(message.Payload),
                "setHotkeyCaptureActive" => SetHotkeyCaptureActive(message.Payload),
                "startControllerShortcutCapture" => StartControllerShortcutCapture(message.Payload),
                "cancelControllerShortcutCapture" => CancelControllerShortcutCapture(message.Payload),
                "acknowledgeControllerShortcutCapture" => AcknowledgeControllerShortcutCapture(message.Payload),
                "getControllerStatus" => GetControllerStatus(),
                "enterGameShell" => await _modeCommandService.EnterGameShellAsync(),
                "enterGameShellWithProfile" => await _modeCommandService.EnterGameShellWithDisplayProfileAsync(ReadOptionalProfileName(message.Payload) ?? "PcGaming"),
                "enterWorkMode" => await _modeCommandService.EnterWorkModeAsync(),
                _ => throw new InvalidOperationException("Unsupported command.")
            };

            if (message.Type == "getStatus" && data is AppStatus status)
            {
                _logger.LogInformation(
                    "[GETSTATUS RESPONSE] Timestamp={Timestamp} CurrentMode={CurrentMode} SessionState={SessionState} ActiveProfileName={ActiveProfileName} ActiveLauncher={ActiveLauncher}.",
                    DateTimeOffset.Now.ToString("O"),
                    status.CurrentMode ?? "null",
                    status.SessionState ?? "null",
                    status.ActiveProfileName ?? "null",
                    status.ActiveLauncher ?? "null");
            }

                    if (message.Type == "enterWorkMode" && data is ModeResult { Success: true })
                    {
                    var committedStatus = await _modeCommandService.GetStatusAsync();
                    _logger.LogInformation(
                        "[WORKMODE BACKEND COMMIT] Timestamp={Timestamp} TransitionId={TransitionId} CurrentMode={CurrentMode} SessionState={SessionState} ActiveProfileName={ActiveProfileName} ActiveLauncher={ActiveLauncher}.",
                        DateTimeOffset.Now.ToString("O"),
                        message.RequestId,
                        committedStatus.CurrentMode ?? "null",
                        committedStatus.SessionState ?? "null",
                        committedStatus.ActiveProfileName ?? "null",
                        committedStatus.ActiveLauncher ?? "null");
                    }

            await PostResponseAsync(coreWebView, message.RequestId, true, data, null);
            if (message.Type is "enterGameShell" or "enterGameShellWithProfile" or "enterWorkMode" or "saveSettings")
            {
                NotifyStatusChanged(message.RequestId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebView command failed: {Command}", message?.Type);
            await PostResponseAsync(coreWebView, message?.RequestId ?? string.Empty, false, null, ex.Message);
        }
    }

    private static Task PostResponseAsync(CoreWebView2 coreWebView, string requestId, bool success, object? data, string? error)
    {
        var response = new WebViewBridgeResponse
        {
            RequestId = requestId,
            Success = success,
            Data = data,
            Error = error
        };

        coreWebView.PostWebMessageAsJson(JsonSerializer.Serialize(response, JsonOptions));
        return Task.CompletedTask;
    }

    private async Task<AppSettings> SaveSettingsAsync(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("Settings payload is required.");
        }

        var settings = payload.Value.Deserialize<AppSettings>(JsonOptions)
            ?? throw new InvalidOperationException("Settings payload is invalid.");

        LogGamingProfileTopologies(settings);
        await SaveAndApplySettingsAsync(settings);
        if (settings.Ui.IsFirstRunCompleted)
        {
            await _modeCommandService.GetStatusAsync();
        }
        return settings;
    }

    private void LogGamingProfileTopologies(AppSettings settings)
    {
        foreach (var profile in settings.Display.PcGamingProfiles.Where(profile => profile.DisplayTopology is not null))
        {
            var topology = profile.DisplayTopology!;
            _logger.LogInformation(
                "[GAMING PROFILE TOPOLOGY SAVED] ProfileId={ProfileId}, ProfileName={ProfileName}, DisplayCount={DisplayCount}",
                profile.Name,
                profile.Name,
                topology.Displays.Count);

            foreach (var display in topology.Displays)
            {
                _logger.LogInformation(
                    "[GAMING PROFILE TOPOLOGY SAVED] ProfileId={ProfileId}, SavedName={SavedName}, Enabled={Enabled}, Primary={Primary}, Position={PositionX},{PositionY}, Mode={Width}x{Height}@{RefreshRateNumerator}/{RefreshRateDenominator}",
                    profile.Name,
                    display.Identity.FriendlyName,
                    display.Enabled,
                    display.IsPrimary,
                    display.PositionX,
                    display.PositionY,
                    display.Width,
                    display.Height,
                    display.RefreshRateNumerator,
                    display.RefreshRateDenominator);
            }
        }
    }

    public void ApplyWindowsIntegrationForCurrentSettings(AppSettings settings)
    {
        ApplyWindowsIntegration(settings);
    }

    private object SetHotkeyCaptureActive(JsonElement? payload)
    {
        var isActive = ReadHotkeyCaptureState(payload);
        HotkeyCaptureStateChanged?.Invoke(this, isActive);
        return new { active = isActive };
    }

    private object StartControllerShortcutCapture(JsonElement? payload)
    {
        var profileId = ReadRequiredCaptureProfileId(payload);
        return _controllerShortcutService?.StartCapture(profileId) ?? throw new InvalidOperationException("Controller capture service is unavailable.");
    }

    private object CancelControllerShortcutCapture(JsonElement? payload)
    {
        var reason = ReadOptionalString(payload, "reason") ?? "UserCancelled";
        return _controllerShortcutService?.CancelCapture(reason) ?? throw new InvalidOperationException("Controller capture service is unavailable.");
    }

    private object AcknowledgeControllerShortcutCapture(JsonElement? payload)
    {
        var sessionId = ReadRequiredGuid(payload, "sessionId");
        return _controllerShortcutService?.AcknowledgeCapture(sessionId) ?? throw new InvalidOperationException("Controller capture service is unavailable.");
    }

    private object GetControllerStatus()
    {
        return _controllerShortcutService?.GetStatus() ?? new
        {
            isConnected = false,
            deviceName = (string?)null,
            isListening = false,
            capturePhase = "Idle",
            captureSessionId = (Guid?)null,
            profileId = (string?)null,
            startedAt = (DateTimeOffset?)null,
            capturedButtons = Array.Empty<string>(),
            completedCapture = (object?)null,
            inputSource = "SDL3",
            gamepadButtonCount = 0,
            joystickButtonCount = 0,
            rawPressedButtons = Array.Empty<int>(),
            backend = "SDL3",
            controllerCount = 0,
            controllerId = (string?)null,
            lastReadButtons = Array.Empty<string>(),
            lastReadAt = (DateTimeOffset?)null,
            diagnosticStatus = "Brak zgodnego kontrolera"
        };
    }

    private async Task<IReadOnlyList<DisplayDeviceInfo>> GetDisplaysAsync()
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

    private async Task<DisplayModeQueryResult> GetDisplayModesAsync(JsonElement? payload)
    {
        var identity = ReadDisplayIdentity(payload);
        return await _displayQueryService.GetAvailableDisplayModesAsync(identity);
    }

    private async Task<IReadOnlyList<AudioDeviceSnapshot>> GetAudioDevicesAsync()
    {
        try
        {
            return await _audioDeviceService.GetRenderDevicesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read audio render devices.");
            throw new InvalidOperationException("Nie udało się odczytać urządzeń audio.");
        }
    }

    private async Task<DisplayLayoutSnapshot> CaptureDisplayLayoutAsync()
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

    private async Task<AppSettings> SaveConsoleDisplayAsync(JsonElement? payload)
    {
        var identity = ReadDisplayIdentity(payload);
        var settings = await _settingsService.LoadAsync();
        settings.Display.ConsoleDisplay = identity;
        settings.Display.ConsoleDisplays = [identity];
        settings.Launcher.TargetDisplayId = identity.MonitorDevicePath;
        await SaveAndApplySettingsAsync(settings);
        return settings;
    }

    private async Task<AppSettings> SaveConsoleDisplaysAsync(JsonElement? payload)
    {
        var identities = ReadDisplayIdentities(payload);
        var settings = await _settingsService.LoadAsync();
        settings.Display.ConsoleDisplays = identities;
        settings.Display.ConsoleDisplay = identities[0];
        settings.Launcher.TargetDisplayId = identities[0].MonitorDevicePath;
        await SaveAndApplySettingsAsync(settings);
        return settings;
    }

    private async Task<AppSettings> SavePcGamingDisplaysAsync(JsonElement? payload)
    {
        var identities = ReadDisplayIdentities(payload);
        var profileName = ReadOptionalProfileName(payload);
        var available = await _displayQueryService.GetAvailableDisplaysAsync();
        var maxProfileCount = CalculateMaxProfileCount(available.Count);
        var selected = new List<DisplayDeviceInfo>(identities.Count);
        foreach (var identity in identities)
        {
            var match = available.FirstOrDefault(display => MatchesSelectedDisplay(identity, display));
            if (match is null)
            {
                throw new InvalidOperationException("Nie wszystkie wybrane ekrany PC Gaming są obecnie dostępne.");
            }

            if (selected.Any(existing => IsSameDisplay(existing, match)))
            {
                throw new InvalidOperationException("Wybrano ten sam ekran PC Gaming więcej niż raz.");
            }

            selected.Add(match);
        }

        if (selected.Count == 0)
        {
            throw new InvalidOperationException("Wybierz co najmniej jeden ekran PC Gaming.");
        }

        var settings = await _settingsService.LoadAsync();
        var primary = selected.FirstOrDefault(display => display.IsPrimary) ?? selected[0];
        var originX = primary.PositionX;
        var originY = primary.PositionY;
        var placement = selected
            .Select(display => new DisplayPlacement
            {
                Display = display,
                PositionX = display.PositionX - originX,
                PositionY = display.PositionY - originY
            })
            .ToList();

        if (placement.Any(item => !item.Display.IsEnabled))
        {
            _logger.LogInformation("PC Gaming selection contains inactive displays. Assigning deterministic fallback positions for inactive screens.");

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

        var layout = new DisplayLayoutSnapshot
        {
            CapturedAt = DateTimeOffset.UtcNow,
            Displays = placement.Select(item => new DisplayPathSnapshot
            {
                Identity = item.Display.Identity,
                IsActive = true,
                IsPrimary = IsSameDisplay(primary, item.Display),
                PositionX = item.PositionX,
                PositionY = item.PositionY,
                Width = item.Display.Width,
                Height = item.Display.Height,
                Orientation = item.Display.Orientation,
                RefreshRateNumerator = item.Display.RefreshRateNumerator,
                RefreshRateDenominator = item.Display.RefreshRateDenominator,
                SourceId = item.Display.SourceId,
                TargetId = item.Display.TargetId,
                AdapterLuid = item.Display.AdapterLuid,
                SourceDeviceName = item.Display.SourceDeviceName,
                ConnectionTechnology = item.Display.ConnectionTechnology
            }).ToList()
        };
        UpsertPcGamingProfile(settings.Display, profileName, layout, maxProfileCount);
        await SaveAndApplySettingsAsync(settings);
        return settings;
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
        if (left.AdapterLuid != 0
            && right.AdapterLuid != 0
            && left.AdapterLuid == right.AdapterLuid
            && left.TargetId == right.TargetId)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(left.Identity.MonitorDevicePath)
            && !string.IsNullOrWhiteSpace(right.Identity.MonitorDevicePath)
            && string.Equals(left.Identity.MonitorDevicePath, right.Identity.MonitorDevicePath, StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveDisplayWidth(int width)
    {
        return width > 0 ? width : 1920;
    }

    private sealed class DisplayPlacement
    {
        public required DisplayDeviceInfo Display { get; init; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
    }

    private async Task<AppSettings> SaveConsoleAudioDeviceAsync(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidOperationException("Audio device payload is required.");
        }

        var deviceId = payload.Value.TryGetProperty("deviceId", out var deviceIdElement)
            ? deviceIdElement.GetString()
            : null;
        var deviceName = payload.Value.TryGetProperty("deviceName", out var deviceNameElement)
            ? deviceNameElement.GetString()
            : null;

        var settings = await _settingsService.LoadAsync();
        settings.Audio.ConsoleAudioDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;
        settings.Audio.ConsoleAudioDeviceName = string.IsNullOrWhiteSpace(deviceId) ? null : deviceName;
        await SaveAndApplySettingsAsync(settings);
        return settings;
    }

    private async Task<AppSettings> CapturePcGamingLayoutAsync(JsonElement? payload)
    {
        var profileName = ReadOptionalProfileName(payload);
        var available = await _displayQueryService.GetAvailableDisplaysAsync();
        var maxProfileCount = CalculateMaxProfileCount(available.Count);
        var settings = await _settingsService.LoadAsync();
        var layout = await CaptureDisplayLayoutAsync();
        UpsertPcGamingProfile(settings.Display, profileName, layout, maxProfileCount);
        await SaveAndApplySettingsAsync(settings);
        return settings;
    }

    private async Task<AppSettings> CapturePrimaryPcGamingLayoutAsync(JsonElement? payload)
    {
        var profileName = ReadOptionalProfileName(payload);
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
        UpsertPcGamingProfile(settings.Display, profileName, layout, maxProfileCount);
        await SaveAndApplySettingsAsync(settings);
        return settings;
    }

    private async Task<AppSettings> SetActivePcGamingProfileAsync(JsonElement? payload)
    {
        var profileName = ReadRequiredProfileName(payload);
        var settings = await _settingsService.LoadAsync();
        var profile = settings.Display.PcGamingProfiles.FirstOrDefault(candidate => string.Equals(candidate.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            throw new InvalidOperationException("Wybrany profil PC Gaming nie istnieje.");
        }

        settings.Display.ActivePcGamingProfileName = profile.Name;
        settings.Display.PcGamingLayout = profile.Layout;
        await SaveAndApplySettingsAsync(settings);
        return settings;
    }

    private async Task<AppSettings> ClearPcGamingLayoutAsync(JsonElement? payload)
    {
        var settings = await _settingsService.LoadAsync();
        var profileName = ReadOptionalProfileName(payload);

        if (string.IsNullOrWhiteSpace(profileName))
        {
            settings.Display.PcGamingLayout = null;
            settings.Display.ActivePcGamingProfileName = null;
            settings.Display.PcGamingProfiles.Clear();
            await SaveAndApplySettingsAsync(settings);
            return settings;
        }

        var index = settings.Display.PcGamingProfiles.FindIndex(profile => string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new InvalidOperationException("Wybrany profil PC Gaming nie istnieje.");
        }

        settings.Display.PcGamingProfiles.RemoveAt(index);
        var active = settings.Display.PcGamingProfiles.FirstOrDefault(profile => string.Equals(profile.Name, settings.Display.ActivePcGamingProfileName, StringComparison.OrdinalIgnoreCase));
        if (active is null)
        {
            active = settings.Display.PcGamingProfiles.FirstOrDefault();
        }

        settings.Display.ActivePcGamingProfileName = active?.Name;
        settings.Display.PcGamingLayout = active?.Layout;
        await SaveAndApplySettingsAsync(settings);
        return settings;
    }

    private async Task<DisplayTransitionResult> TestConsoleDisplayAsync()
    {
        var settings = await _settingsService.LoadAsync();
        return await _displayTransitionService.TestConsoleDisplayAsync(GetConsoleDisplays(settings));
    }

    private async Task<DisplayTransitionResult> TestPcGamingLayoutAsync()
    {
        var settings = await _settingsService.LoadAsync();
        return await _displayTransitionService.TestPcGamingLayoutAsync(settings.Display.PcGamingLayout);
    }

    private async Task SaveAndApplySettingsAsync(AppSettings settings)
    {
        await _settingsService.SaveAsync(settings);
        ApplyWindowsIntegration(settings);
        SettingsSaved?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Settings saved from WebView. Path: {SettingsPath}", _settingsService.SettingsPath);
    }

    private void ApplyWindowsIntegration(AppSettings settings)
    {
        var startupRegistration = ResolveStartupRegistration();

        ApplyStartup(settings.Startup.StartWithWindows, startupRegistration.RunCommand);
        _logger.LogInformation(
            "Windows integration applied. StartWithWindows={StartWithWindows}, RunCommand={RunCommand}, BaseDirectory={BaseDirectory}.",
            settings.Startup.StartWithWindows,
            startupRegistration.RunCommand,
            AppContext.BaseDirectory);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "GameShellShortcut.ico");

        ApplyShortcut(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "GameShell.lnk",
            settings.Startup.CreateDesktopShortcut,
            startupRegistration.ShortcutTargetPath,
            startupRegistration.ShortcutArguments,
            iconPath);

        ApplyShortcut(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Path.Combine("Programs", "GameShell.lnk"),
            settings.Startup.CreateStartMenuShortcut,
            startupRegistration.ShortcutTargetPath,
            startupRegistration.ShortcutArguments,
            iconPath);
    }

    private static void ApplyStartup(bool enabled, string runCommand)
    {
        const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(runKeyPath, writable: true);

        if (enabled)
        {
            runKey.SetValue("GameShell", runCommand);
            return;
        }

        runKey.DeleteValue("GameShell", throwOnMissingValue: false);
    }

    private static void ApplyShortcut(string baseDirectory, string relativeShortcutPath, bool enabled, string targetPath, string arguments, string iconPath)
    {
        var shortcutPath = Path.Combine(baseDirectory, relativeShortcutPath);

        if (!enabled)
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        CreateShortcut(shortcutPath, targetPath, arguments, iconPath);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string iconPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows shortcut service is not available.");

        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Windows shortcut service could not be created.");
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.Arguments = arguments;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
        shortcut.Description = "GameShell";
        shortcut.IconLocation = File.Exists(iconPath) ? $"{iconPath},0" : $"{targetPath},0";
        shortcut.Save();
    }

    private static StartupRegistration ResolveStartupRegistration()
    {
        var processPath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("GameShell executable path is not available.");

        var appHostPath = Path.Combine(AppContext.BaseDirectory, "GameShell.exe");
        if (File.Exists(appHostPath))
        {
            return new StartupRegistration(appHostPath, string.Empty, $"\"{appHostPath}\"");
        }

        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        var processFileName = Path.GetFileName(processPath);
        var isDotnetHost = string.Equals(processFileName, "dotnet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(processFileName, "dotnet.exe", StringComparison.OrdinalIgnoreCase);

        if (isDotnetHost
            && !string.IsNullOrWhiteSpace(entryAssemblyPath)
            && entryAssemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var arguments = $"\"{entryAssemblyPath}\"";
            return new StartupRegistration(processPath, arguments, $"\"{processPath}\" {arguments}");
        }

        return new StartupRegistration(processPath, string.Empty, $"\"{processPath}\"");
    }

    private sealed record StartupRegistration(string ShortcutTargetPath, string ShortcutArguments, string RunCommand);

    private async Task<DependencyActionResult> OpenDependencyDownloadPageAsync(JsonElement? payload)
    {
        var dependencyName = ReadDependencyName(payload);
        var dependencies = await _dependencyDetectionService.DetectAsync(await _settingsService.LoadAsync());
        var dependency = dependencies.FirstOrDefault(item => string.Equals(item.Name, dependencyName, StringComparison.OrdinalIgnoreCase));

        if (dependency?.DownloadUrl is null)
        {
            throw new InvalidOperationException("Download page is not available for this dependency.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = dependency.DownloadUrl,
            UseShellExecute = true
        });

        return new DependencyActionResult(true, $"Otwarto stronę pobierania: {dependency.Name}", null);
    }

    private async Task<AppSettings> SelectDependencyPathAsync(JsonElement? payload)
    {
        var dependencyName = ReadDependencyName(payload);
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Wskaż plik dla {dependencyName}",
            Filter = "Programy Windows (*.exe)|*.exe|Wszystkie pliki (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            throw new OperationCanceledException("File selection cancelled.");
        }

        var settings = await _settingsService.LoadAsync();
        if (string.Equals(dependencyName, "Playnite", StringComparison.OrdinalIgnoreCase))
        {
            settings.Launcher.Type = "Playnite";
            settings.Launcher.ExecutablePath = dialog.FileName;
        }
        else if (string.Equals(dependencyName, "Steam", StringComparison.OrdinalIgnoreCase))
        {
            settings.Launcher.Type = "Steam";
            settings.Launcher.ExecutablePath = dialog.FileName;
            settings.Launcher.Arguments = "";
        }
        else
        {
            throw new InvalidOperationException("This dependency path cannot be changed.");
        }

        await SaveAndApplySettingsAsync(settings);
        return settings;
    }

    private static string ReadDependencyName(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object || !payload.Value.TryGetProperty("name", out var nameElement))
        {
            throw new InvalidOperationException("Dependency name is required.");
        }

        var dependencyName = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(dependencyName))
        {
            throw new InvalidOperationException("Dependency name is required.");
        }

        return dependencyName;
    }

    private static bool ReadHotkeyCaptureState(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object || !payload.Value.TryGetProperty("active", out var activeElement))
        {
            throw new InvalidOperationException("Hotkey capture state is required.");
        }

        return activeElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidOperationException("Hotkey capture state is invalid.")
        };
    }

    private static Guid ReadRequiredGuid(JsonElement? payload, string propertyName)
    {
        var value = ReadOptionalString(payload, propertyName);
        if (!Guid.TryParse(value, out var result)) throw new InvalidOperationException($"{propertyName} is required.");
        return result;
    }

    private static string? ReadOptionalString(JsonElement? payload, string propertyName)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object || !payload.Value.TryGetProperty(propertyName, out var property)) return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static DisplayIdentity ReadDisplayIdentity(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object || !payload.Value.TryGetProperty("identity", out var identityElement))
        {
            throw new InvalidOperationException("Display identity is required.");
        }

        return identityElement.Deserialize<DisplayIdentity>(JsonOptions)
            ?? throw new InvalidOperationException("Display identity is invalid.");
    }

    private static List<DisplayIdentity> ReadDisplayIdentities(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object || !payload.Value.TryGetProperty("identities", out var identitiesElement))
        {
            throw new InvalidOperationException("Display identities are required.");
        }

        var identities = identitiesElement.Deserialize<List<DisplayIdentity>>(JsonOptions);
        if (identities is null || identities.Count == 0)
        {
            throw new InvalidOperationException("Wybierz co najmniej jeden ekran.");
        }

        return identities;
    }

    private static string? ReadOptionalProfileName(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        if (!payload.Value.TryGetProperty("profileName", out var profileNameElement))
        {
            return null;
        }

        var profileName = profileNameElement.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(profileName)
            ? null
            : profileName;
    }

    private static string ReadRequiredProfileName(JsonElement? payload)
    {
        return ReadOptionalProfileName(payload)
            ?? throw new InvalidOperationException("Nazwa profilu jest wymagana.");
    }

    private static string ReadRequiredCaptureProfileId(JsonElement? payload)
    {
        var profileId = ReadOptionalString(payload, "profileId") ?? ReadOptionalProfileName(payload);
        return string.IsNullOrWhiteSpace(profileId)
            ? throw new InvalidOperationException("ProfileId is required.")
            : profileId.Trim();
    }

    private static void UpsertPcGamingProfile(DisplaySettings displaySettings, string? profileName, DisplayLayoutSnapshot layout, int maxProfileCount)
    {
        displaySettings.PcGamingLayout = layout;

        if (string.IsNullOrWhiteSpace(profileName))
        {
            if (string.IsNullOrWhiteSpace(displaySettings.ActivePcGamingProfileName))
            {
                return;
            }

            var activeProfile = displaySettings.PcGamingProfiles.FirstOrDefault(profile => string.Equals(profile.Name, displaySettings.ActivePcGamingProfileName, StringComparison.OrdinalIgnoreCase));
            if (activeProfile is not null)
            {
                activeProfile.Layout = layout;
            }

            return;
        }

        var existingProfile = displaySettings.PcGamingProfiles.FirstOrDefault(profile => string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (displaySettings.PcGamingProfiles.Any(profile => !string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase) && IsSameProfileCombination(profile.Layout, layout)))
        {
            throw new InvalidOperationException("Profil z taką samą kombinacją ekranów już istnieje.");
        }

        if (existingProfile is not null)
        {
            existingProfile.Layout = layout;
            displaySettings.ActivePcGamingProfileName = existingProfile.Name;
            return;
        }

        if (maxProfileCount > 0 && displaySettings.PcGamingProfiles.Count >= maxProfileCount)
        {
            throw new InvalidOperationException($"Osiągnięto limit profili ({maxProfileCount}) wynikający z liczby kombinacji wykrytych monitorów.");
        }

        displaySettings.PcGamingProfiles.Add(new DisplayLayoutProfile
        {
            Name = profileName,
            Layout = layout
        });
        displaySettings.ActivePcGamingProfileName = profileName;
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

        if (leftKeys.Count != rightKeys.Count)
        {
            return false;
        }

        for (var index = 0; index < leftKeys.Count; index++)
        {
            if (!string.Equals(leftKeys[index], rightKeys[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
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

        if (displayCount >= 31)
        {
            return int.MaxValue;
        }

        return (1 << displayCount) - 1;
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

    private sealed record DependencyActionResult(bool Success, string Message, string? Path);
}
