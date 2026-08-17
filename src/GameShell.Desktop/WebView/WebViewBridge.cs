using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using GameShell.Desktop.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace GameShell.Desktop.WebView;

public sealed class WebViewBridge
{
    private CoreWebView2? _coreWebView;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IModeCommandService _modeCommandService;
    private readonly ISettingsService _settingsService;
    private readonly IDependencyDetectionService _dependencyDetectionService;
    private readonly IDisplayTransitionService _displayTransitionService;
    private readonly IWindowsIntegrationService _windowsIntegrationService;
    private readonly WebViewDisplayCommandHandler _displayCommandHandler;
    private readonly WebViewCommandRouter _commandRouter;
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
        IDisplayQueryService displayQueryService,
        IDisplayTransitionService displayTransitionService,
        IWindowsIntegrationService windowsIntegrationService,
        ILogger<WebViewBridge> logger,
        ILoggerFactory loggerFactory)
    {
        _modeCommandService = modeCommandService;
        _settingsService = settingsService;
        _dependencyDetectionService = dependencyDetectionService;
        _displayTransitionService = displayTransitionService;
        _windowsIntegrationService = windowsIntegrationService;
        _displayCommandHandler = new WebViewDisplayCommandHandler(
            settingsService,
            displayQueryService,
            displayTransitionService,
            windowsIntegrationService,
            loggerFactory.CreateLogger<WebViewDisplayCommandHandler>(),
            SaveAndApplySettingsAsync);
        _logger = logger;
        _commandRouter = new WebViewCommandRouter(CreateCommandHandlers());
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

            if (!_commandRouter.IsSupported(message.Type))
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

            var data = await _commandRouter.DispatchAsync(message.Type, message.Payload);

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

    private IReadOnlyDictionary<string, Func<JsonElement?, Task<object>>> CreateCommandHandlers()
    {
        return new Dictionary<string, Func<JsonElement?, Task<object>>>(StringComparer.Ordinal)
        {
            ["getStatus"] = async _ => await _modeCommandService.GetStatusAsync(),
            ["getSettings"] = async _ => await _settingsService.LoadAsync(),
            ["saveSettings"] = async payload => await SaveSettingsAsync(payload),
            ["getDependencies"] = async _ => await _dependencyDetectionService.DetectAsync(await _settingsService.LoadAsync()),
            ["getDisplays"] = async _ => await _displayCommandHandler.GetDisplaysAsync(),
            ["getDisplayModes"] = async payload => await _displayCommandHandler.GetDisplayModesAsync(payload),
            ["captureDisplayLayout"] = async _ => await _displayCommandHandler.CaptureDisplayLayoutAsync(),
            ["saveConsoleDisplay"] = async payload => await _displayCommandHandler.SaveConsoleDisplayAsync(payload),
            ["saveConsoleDisplays"] = async payload => await _displayCommandHandler.SaveConsoleDisplaysAsync(payload),
            ["saveGamingDisplays"] = async payload => await _displayCommandHandler.SaveGamingDisplaysAsync(payload),
            ["captureGamingLayout"] = async payload => await _displayCommandHandler.CaptureGamingLayoutAsync(payload),
            ["capturePrimaryGamingLayout"] = async payload => await _displayCommandHandler.CapturePrimaryGamingLayoutAsync(payload),
            ["setActiveGamingProfile"] = async payload => await _displayCommandHandler.SetActiveGamingProfileAsync(payload),
            ["clearGamingLayout"] = async payload => await _displayCommandHandler.ClearGamingLayoutAsync(payload),
            ["testConsoleDisplay"] = async _ => await _displayCommandHandler.TestConsoleDisplayAsync(),
            ["testGamingLayout"] = async _ => await _displayCommandHandler.TestGamingLayoutAsync(),
            ["confirmDisplayTest"] = async _ => await _displayTransitionService.ConfirmPendingTestAsync(),
            ["restoreDisplayTest"] = async _ => await _displayTransitionService.RestorePendingTestAsync(),
            ["restorePreviousDisplayLayout"] = async _ => await _displayTransitionService.RestorePreviousLayoutAsync(),
            ["openDependencyDownloadPage"] = async payload => await OpenDependencyDownloadPageAsync(payload),
            ["selectDependencyPath"] = async payload => await SelectDependencyPathAsync(payload),
            ["setHotkeyCaptureActive"] = payload => Task.FromResult<object>(SetHotkeyCaptureActive(payload)),
            ["startControllerShortcutCapture"] = payload => Task.FromResult<object>(StartControllerShortcutCapture(payload)),
            ["cancelControllerShortcutCapture"] = payload => Task.FromResult<object>(CancelControllerShortcutCapture(payload)),
            ["acknowledgeControllerShortcutCapture"] = payload => Task.FromResult<object>(AcknowledgeControllerShortcutCapture(payload)),
            ["getControllerStatus"] = _ => Task.FromResult<object>(GetControllerStatus()),
            ["enterGameShell"] = async _ => await _modeCommandService.EnterGameShellAsync(),
            ["enterGameShellWithProfile"] = async payload => await _modeCommandService.EnterGameShellWithDisplayProfileAsync(WebViewPayloadReader.ReadOptionalProfileName(payload) ?? string.Empty),
            ["enterWorkMode"] = async _ => await _modeCommandService.EnterWorkModeAsync()
        };
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

        var persistedSettings = await _settingsService.LoadAsync();
        if (settings.Display.KnownPhysicalDisplays.Count == 0
            && persistedSettings.Display.KnownPhysicalDisplays.Count > 0)
        {
            settings.Display.KnownPhysicalDisplays = persistedSettings.Display.KnownPhysicalDisplays;
        }

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
        foreach (var profile in settings.Display.GamingProfiles.Where(profile => profile.DisplayTopology is not null))
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
                    "[DISPLAY PROFILE REFERENCE] Profile={Profile}, KnownDisplayId={KnownDisplayId}, CanonicalName={CanonicalName}, Enabled={Enabled}, Primary={Primary}, Position={PositionX},{PositionY}, Resolution={Width}x{Height}, RefreshPolicy={RefreshPolicy}, FixedRefresh={FixedRefreshNumerator}/{FixedRefreshDenominator}",
                    profile.Name,
                    display.KnownPhysicalDisplayId,
                    display.Identity.FriendlyName,
                    display.Enabled,
                    display.IsPrimary,
                    display.PositionX,
                    display.PositionY,
                    display.Width,
                    display.Height,
                    display.EffectiveRefreshPolicy,
                    display.EffectiveFixedRefreshRateNumerator,
                    display.EffectiveFixedRefreshRateDenominator);
            }
        }
    }

    public void ApplyWindowsIntegrationForCurrentSettings(AppSettings settings)
    {
        _displayCommandHandler.ApplyWindowsIntegrationForCurrentSettings(settings);
    }

    private object SetHotkeyCaptureActive(JsonElement? payload)
    {
        var isActive = WebViewPayloadReader.ReadHotkeyCaptureState(payload);
        HotkeyCaptureStateChanged?.Invoke(this, isActive);
        return new { active = isActive };
    }

    private object StartControllerShortcutCapture(JsonElement? payload)
    {
        var profileId = WebViewPayloadReader.ReadRequiredCaptureProfileId(payload);
        return _controllerShortcutService?.StartCapture(profileId) ?? throw new InvalidOperationException("Controller capture service is unavailable.");
    }

    private object CancelControllerShortcutCapture(JsonElement? payload)
    {
        var reason = WebViewPayloadReader.ReadOptionalString(payload, "reason") ?? "UserCancelled";
        return _controllerShortcutService?.CancelCapture(reason) ?? throw new InvalidOperationException("Controller capture service is unavailable.");
    }

    private object AcknowledgeControllerShortcutCapture(JsonElement? payload)
    {
        var sessionId = WebViewPayloadReader.ReadRequiredGuid(payload, "sessionId");
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

    private Task<IReadOnlyList<DisplayDeviceInfo>> GetDisplaysAsync() => _displayCommandHandler.GetDisplaysAsync();

    private Task<DisplayModeQueryResult> GetDisplayModesAsync(JsonElement? payload) => _displayCommandHandler.GetDisplayModesAsync(payload);

    private Task<DisplayLayoutSnapshot> CaptureDisplayLayoutAsync() => _displayCommandHandler.CaptureDisplayLayoutAsync();

    private Task<AppSettings> SaveConsoleDisplayAsync(JsonElement? payload) => _displayCommandHandler.SaveConsoleDisplayAsync(payload);

    private Task<AppSettings> SaveConsoleDisplaysAsync(JsonElement? payload) => _displayCommandHandler.SaveConsoleDisplaysAsync(payload);

    private Task<AppSettings> SaveGamingDisplaysAsync(JsonElement? payload) => _displayCommandHandler.SaveGamingDisplaysAsync(payload);

    private Task<AppSettings> CaptureGamingLayoutAsync(JsonElement? payload) => _displayCommandHandler.CaptureGamingLayoutAsync(payload);

    private Task<AppSettings> CapturePrimaryGamingLayoutAsync(JsonElement? payload) => _displayCommandHandler.CapturePrimaryGamingLayoutAsync(payload);

    private Task<AppSettings> SetActiveGamingProfileAsync(JsonElement? payload) => _displayCommandHandler.SetActiveGamingProfileAsync(payload);

    private Task<AppSettings> ClearGamingLayoutAsync(JsonElement? payload) => _displayCommandHandler.ClearGamingLayoutAsync(payload);

    private Task<DisplayTransitionResult> TestConsoleDisplayAsync() => _displayCommandHandler.TestConsoleDisplayAsync();

    private Task<DisplayTransitionResult> TestGamingLayoutAsync() => _displayCommandHandler.TestGamingLayoutAsync();

    private async Task SaveAndApplySettingsAsync(AppSettings settings)
    {
        GamingProfileTopologyValidator.EnsureValid(settings);
        await _settingsService.SaveAsync(settings);
        _windowsIntegrationService.Apply(settings);
        SettingsSaved?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Settings saved from WebView. Path: {SettingsPath}", _settingsService.SettingsPath);
    }

    private async Task<DependencyActionResult> OpenDependencyDownloadPageAsync(JsonElement? payload)
    {
        var dependencyName = WebViewPayloadReader.ReadDependencyName(payload);
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
        var dependencyName = WebViewPayloadReader.ReadDependencyName(payload);
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

    private sealed record DependencyActionResult(bool Success, string Message, string? Path);
}
