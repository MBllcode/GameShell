using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace GameShell.Desktop.Services;

public sealed class SdlControllerShortcutService : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IModeCommandService _modeCommandService;
    private readonly WebView.WebViewBridge _webViewBridge;
    private readonly ILogger<SdlControllerShortcutService> _logger;
    private readonly Lock _syncRoot = new();
    private readonly HashSet<ControllerButton> _lastPressedButtons = [];
    private readonly Dictionary<string, DateTimeOffset> _pressedSince = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _triggeredProfiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ControllerCaptureStateMachine _captureMachine = new();

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private AppSettings _settings = new();
    private IntPtr _gamepad;
    private uint _gamepadId;
    private bool _isExecutingCommand;
    private bool _isCaptureActive;
    private string? _lastControllerId;
    private string? _lastDeviceName;
    private DateTimeOffset _lastReadingAt = DateTimeOffset.MinValue;
    private string _diagnosticStatus = "Brak zgodnego kontrolera SDL3";
    private DateTimeOffset _lastDiagnosticLogAt = DateTimeOffset.MinValue;
    private long _controllerStatusChangeSequence;
    private ControllerInputSnapshot _latestSnapshot = new("", new HashSet<ControllerButton>(), 0, DateTimeOffset.UtcNow);
    private long _snapshotSequence;
    private Guid? _captureSessionId;
    private string? _captureProfileId;
    private DateTimeOffset? _captureStartedAt;
    private CompletedControllerCapture? _completedCapture;

    public SdlControllerShortcutService(ISettingsService settingsService, IModeCommandService modeCommandService, WebView.WebViewBridge webViewBridge, ILogger<SdlControllerShortcutService> logger)
    {
        _settingsService = settingsService;
        _modeCommandService = modeCommandService;
        _webViewBridge = webViewBridge;
        _logger = logger;
    }

    public object StartCapture(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId)) throw new InvalidOperationException("ProfileId is required.");

        lock (_syncRoot)
        {
            if (IsCaptureActive(_captureMachine.Phase) || _completedCapture is not null)
            {
                return GetCaptureSessionStatus();
            }

            var startedAt = DateTimeOffset.UtcNow;
            _captureSessionId = Guid.NewGuid();
            _captureProfileId = profileId;
            _captureStartedAt = startedAt;
            _completedCapture = null;
            _captureMachine.Start(_latestSnapshot, startedAt);
            _isCaptureActive = IsCaptureActive(_captureMachine.Phase);
            _diagnosticStatus = GetCaptureStatusText(_captureMachine.Phase);
            _logger.LogInformation("Capture {SessionId} started for profile={ProfileId} phase={Phase}.", _captureSessionId, profileId, _captureMachine.Phase);
        }
        NotifyControllerStatusChanged();
        return GetCaptureSessionStatus();
    }

    public object CancelCapture(string reason)
    {
        lock (_syncRoot)
        {
            if (_captureSessionId is null || !IsCaptureActive(_captureMachine.Phase)) return GetCaptureSessionStatus();
            _logger.LogInformation("Capture {SessionId} cancelled reason={Reason}.", _captureSessionId, reason);
            _captureMachine.Cancel();
            _captureMachine.Reset();
            _captureSessionId = null;
            _captureProfileId = null;
            _captureStartedAt = null;
            _completedCapture = null;
            _isCaptureActive = false;
            _diagnosticStatus = GetCaptureStatusText(ControllerCapturePhase.Idle);
        }
        NotifyControllerStatusChanged();
        return GetCaptureSessionStatus();
    }

    public object AcknowledgeCapture(Guid sessionId)
    {
        lock (_syncRoot)
        {
            if (_completedCapture?.SessionId == sessionId)
            {
                _logger.LogInformation("Capture {SessionId} acknowledged.", sessionId);
                _completedCapture = null;
                _captureMachine.Reset();
                _captureSessionId = null;
                _captureProfileId = null;
                _captureStartedAt = null;
                _isCaptureActive = false;
                _diagnosticStatus = GetCaptureStatusText(ControllerCapturePhase.Idle);
            }
        }
        NotifyControllerStatusChanged();
        return GetCaptureSessionStatus();
    }

    public object GetStatus()
    {
        lock (_syncRoot)
        {
            return new
            {
                isConnected = _gamepad != IntPtr.Zero,
                deviceName = _lastDeviceName,
                isListening = IsCaptureActive(_captureMachine.Phase),
                capturePhase = _captureMachine.Phase.ToString(),
                captureSessionId = _captureSessionId,
                profileId = _captureProfileId,
                startedAt = _captureStartedAt,
                capturedButtons = _captureMachine.CapturedButtons.Select(button => button.ToString()).ToArray(),
                completedCapture = _completedCapture is null ? null : new
                {
                    sessionId = _completedCapture.SessionId,
                    profileId = _completedCapture.ProfileId,
                    buttons = _completedCapture.Buttons.Select(button => button.ToString()).ToArray(),
                    completedAt = _completedCapture.CompletedAt
                },
                inputSource = "SDL3",
                gamepadButtonCount = _lastPressedButtons.Count,
                joystickButtonCount = 0,
                rawPressedButtons = Array.Empty<int>(),
                backend = "SDL3",
                controllerCount = _gamepad == IntPtr.Zero ? 0 : 1,
                sequenceNumber = _controllerStatusChangeSequence,
                controllerId = _lastControllerId,
                lastReadButtons = _lastPressedButtons.Select(button => button.ToString()).ToArray(),
                lastReadAt = _lastReadingAt,
                diagnosticStatus = _diagnosticStatus
            };
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null) return;
        _settings = await _settingsService.LoadAsync(cancellationToken);
        _webViewBridge.SettingsSaved += OnSettingsSaved;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
    }

    public async Task StopAsync()
    {
        _webViewBridge.SettingsSaved -= OnSettingsSaved;
        CancelCapture("ApplicationClosed");
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_loopTask is not null)
            {
                try { await _loopTask; } catch (OperationCanceledException) { }
            }
            _loopTask = null;
            _cts.Dispose();
            _cts = null;
        }
        CloseSdl();
    }

    private async void OnSettingsSaved(object? sender, EventArgs e)
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            lock (_syncRoot)
            {
                _settings = settings;
                _pressedSince.Clear();
                _triggeredProfiles.Clear();
            }
            _logger.LogInformation(
                "Controller shortcut settings refreshed. ProfilesWithShortcuts={Profiles}.",
                settings.Display.PcGamingProfileConfigurations.Count(configuration => configuration.ControllerShortcut?.Buttons.Count > 0));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to refresh controller settings after save."); }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!SdlNative.SDL_InitSubSystem(SdlNative.InitFlags.Gamepad))
            {
                SetDisconnected($"SDL3 initialization failed: {SdlNative.Utf8(SdlNative.SDL_GetError())}");
                return;
            }

            LogRuntimeInfo();

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
            var enumerationTick = 0;
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (++enumerationTick >= 50)
                {
                    enumerationTick = 0;
                    RefreshGamepad();
                }
                PollGamepad(cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SDL3 controller polling failed.");
            SetDisconnected($"SDL3 polling failed: {ex.Message}");
        }
        finally { CloseSdl(); }
    }

    private void RefreshGamepad()
    {
        SdlNative.SDL_PumpEvents();
        var ids = SdlNative.SDL_GetGamepads(out var count);
        _logger.LogInformation("SDL_GetGamepads returned {Count} device(s).", count);
        var selectedId = 0u;
        for (var index = 0; index < count; index++)
        {
            var id = unchecked((uint)Marshal.ReadInt32(ids, index * sizeof(uint)));
            if (SdlNative.SDL_IsGamepad(id) != 0) { selectedId = id; break; }
        }

        if (selectedId == _gamepadId
            && _gamepad != IntPtr.Zero
            && SdlNative.SDL_GamepadConnected(_gamepad) != 0)
        {
            return;
        }
        if (_gamepad != IntPtr.Zero) SdlNative.SDL_CloseGamepad(_gamepad);
        _gamepad = IntPtr.Zero;
        _gamepadId = selectedId;
        if (selectedId == 0)
        {
            SetDisconnected("Brak zgodnego kontrolera SDL3");
            return;
        }

        _gamepad = SdlNative.SDL_OpenGamepad(selectedId);
        if (_gamepad == IntPtr.Zero)
        {
            SetDisconnected($"Nie można otworzyć kontrolera SDL3: {SdlNative.Utf8(SdlNative.SDL_GetError())}");
            return;
        }

        lock (_syncRoot)
        {
            _lastControllerId = selectedId.ToString();
            _lastDeviceName = SdlNative.Utf8(SdlNative.SDL_GetGamepadName(_gamepad)) ?? "SDL3 Gamepad";
            _diagnosticStatus = "Kontroler SDL3 wykryty";
        }
        _logger.LogInformation(
            "SDL gamepad opened. Id={Id}, Handle=0x{Handle:X}, Name={Name}, Mapping={Mapping}, Connected={Connected}.",
            selectedId,
            _gamepad.ToInt64(),
            _lastDeviceName,
            SdlNative.Utf8(SdlNative.SDL_GetGamepadMapping(_gamepad)) ?? "(null)",
            SdlNative.SDL_GamepadConnected(_gamepad) != 0);
        LogButtonSupport();
        NotifyControllerStatusChanged();
    }

    private void PollGamepad(CancellationToken cancellationToken)
    {
        if (_gamepad == IntPtr.Zero) { RefreshGamepad(); return; }
        if (SdlNative.SDL_GamepadConnected(_gamepad) == 0)
        {
            RefreshGamepad();
            return;
        }

        SdlNative.SDL_PumpEvents();
        SdlNative.SDL_UpdateGamepads();
        var pressed = new HashSet<ControllerButton>();
        AddIfPressed(pressed, ControllerButton.South, SdlNative.SdlGamepadButton.South);
        AddIfPressed(pressed, ControllerButton.East, SdlNative.SdlGamepadButton.East);
        AddIfPressed(pressed, ControllerButton.West, SdlNative.SdlGamepadButton.West);
        AddIfPressed(pressed, ControllerButton.North, SdlNative.SdlGamepadButton.North);
        AddIfPressed(pressed, ControllerButton.Back, SdlNative.SdlGamepadButton.Back);
        AddIfPressed(pressed, ControllerButton.Start, SdlNative.SdlGamepadButton.Start);
        AddIfPressed(pressed, ControllerButton.Guide, SdlNative.SdlGamepadButton.Guide);
        AddIfPressed(pressed, ControllerButton.LeftShoulder, SdlNative.SdlGamepadButton.LeftShoulder);
        AddIfPressed(pressed, ControllerButton.RightShoulder, SdlNative.SdlGamepadButton.RightShoulder);
        AddIfPressed(pressed, ControllerButton.LeftStick, SdlNative.SdlGamepadButton.LeftStick);
        AddIfPressed(pressed, ControllerButton.RightStick, SdlNative.SdlGamepadButton.RightStick);
        AddIfPressed(pressed, ControllerButton.DpadUp, SdlNative.SdlGamepadButton.DpadUp);
        AddIfPressed(pressed, ControllerButton.DpadDown, SdlNative.SdlGamepadButton.DpadDown);
        AddIfPressed(pressed, ControllerButton.DpadLeft, SdlNative.SdlGamepadButton.DpadLeft);
        AddIfPressed(pressed, ControllerButton.DpadRight, SdlNative.SdlGamepadButton.DpadRight);
        AddIfPressed(pressed, ControllerButton.Misc1, SdlNative.SdlGamepadButton.Misc1);
        AddIfPressed(pressed, ControllerButton.Misc2, SdlNative.SdlGamepadButton.Misc2);
        AddIfPressed(pressed, ControllerButton.Misc3, SdlNative.SdlGamepadButton.Misc3);

        string controllerId;
        string deviceName;
        ControllerCapturePhase capturePhase;
        ControllerCapturePhase previousCapturePhase;
        long sequenceNumber;
        bool stateChanged;
        string capturedButtonsForLog;
        var leftStickY = SdlNative.SDL_GetGamepadAxis(_gamepad, SdlNative.SdlGamepadAxis.LeftY);
        lock (_syncRoot)
        {
            controllerId = _lastControllerId!;
            deviceName = _lastDeviceName!;
            previousCapturePhase = _captureMachine.Phase;
            var snapshot = new ControllerInputSnapshot(
                controllerId,
                pressed.ToHashSet(),
                ++_snapshotSequence,
                DateTimeOffset.UtcNow);
            sequenceNumber = snapshot.SequenceNumber;
            _latestSnapshot = snapshot;
            _captureMachine.Process(snapshot);
            _isCaptureActive = IsCaptureActive(_captureMachine.Phase);
            capturePhase = _captureMachine.Phase;
            capturedButtonsForLog = string.Join(',', _captureMachine.CapturedButtons);
            stateChanged = !_lastPressedButtons.SetEquals(pressed) || previousCapturePhase != capturePhase;
            _lastPressedButtons.Clear();
            _lastPressedButtons.UnionWith(pressed);
            _lastReadingAt = DateTimeOffset.UtcNow;
            _diagnosticStatus = GetCaptureStatusText(capturePhase);
            if (capturePhase == ControllerCapturePhase.Completed && _completedCapture is null && _captureSessionId is not null && _captureProfileId is not null)
            {
                _completedCapture = new CompletedControllerCapture
                {
                    SessionId = _captureSessionId.Value,
                    ProfileId = _captureProfileId,
                    Buttons = _captureMachine.CapturedButtons.OrderBy(button => button).ToArray(),
                    CompletedAt = DateTimeOffset.UtcNow
                };
                _logger.LogInformation("Capture {SessionId} completed buttons=[{Buttons}].", _captureSessionId, capturedButtonsForLog);
            }
        }
        if (previousCapturePhase != capturePhase)
        {
            _logger.LogInformation(
                "Capture phase={Phase} Sequence={Sequence} Buttons=[{Buttons}] ControllerId={ControllerId}.",
                capturePhase,
                sequenceNumber,
                capturedButtonsForLog,
                controllerId);
        }
        if (capturePhase == ControllerCapturePhase.Idle)
        {
            _ = HandleShortcutsAsync(controllerId, pressed, cancellationToken);
        }
        LogPollingDiagnostics(pressed, sequenceNumber, capturePhase);
        if (stateChanged)
        {
            NotifyControllerStatusChanged();
        }
    }

    private void LogRuntimeInfo()
    {
        var version = SdlNative.SDL_GetVersion();
        var major = version / 1_000_000;
        var minor = version / 1_000 % 1_000;
        var patch = version % 1_000;
        _logger.LogInformation(
            "SDL runtime initialized. Library={Library}, Version={Version}, Architecture={Architecture}, GamepadSubsystem={GamepadSubsystem}.",
            SdlNative.LoadedLibraryPath() ?? "(unknown)",
            $"{major}.{minor}.{patch}",
            Environment.Is64BitProcess ? "x64" : "x86",
            SdlNative.SDL_WasInit(SdlNative.InitFlags.Gamepad));
    }

    private void LogButtonSupport()
    {
        var buttons = new[]
        {
            SdlNative.SdlGamepadButton.South,
            SdlNative.SdlGamepadButton.East,
            SdlNative.SdlGamepadButton.West,
            SdlNative.SdlGamepadButton.North,
            SdlNative.SdlGamepadButton.Back,
            SdlNative.SdlGamepadButton.Start,
            SdlNative.SdlGamepadButton.Touchpad
        };
        var support = string.Join(", ", buttons.Select(button => $"{button}: has={SdlNative.SDL_GamepadHasButton(_gamepad, button) != 0}"));
        _logger.LogInformation("SDL gamepad button support. {Support}", support);
    }

    private void LogPollingDiagnostics(HashSet<ControllerButton> pressed, long sequenceNumber, ControllerCapturePhase capturePhase)
    {
        if (!_isCaptureActive || DateTimeOffset.UtcNow - _lastDiagnosticLogAt < TimeSpan.FromSeconds(1)) return;
        _lastDiagnosticLogAt = DateTimeOffset.UtcNow;
        _logger.LogInformation(
            "Poll seq={Sequence}, Phase={Phase}, Handle valid: {HandleValid}, Connected: {Connected}, South: {South}, East: {East}, West: {West}, North: {North}, Back: {Back}, Start: {Start}, Left axis X: {LeftX}, Left axis Y: {LeftY}, Pressed: [{Pressed}].",
            sequenceNumber,
            capturePhase,
            _gamepad != nint.Zero,
            SdlNative.SDL_GamepadConnected(_gamepad) != 0,
            SdlNative.SDL_GetGamepadButton(_gamepad, SdlNative.SdlGamepadButton.South) != 0,
            SdlNative.SDL_GetGamepadButton(_gamepad, SdlNative.SdlGamepadButton.East) != 0,
            SdlNative.SDL_GetGamepadButton(_gamepad, SdlNative.SdlGamepadButton.West) != 0,
            SdlNative.SDL_GetGamepadButton(_gamepad, SdlNative.SdlGamepadButton.North) != 0,
            SdlNative.SDL_GetGamepadButton(_gamepad, SdlNative.SdlGamepadButton.Back) != 0,
            SdlNative.SDL_GetGamepadButton(_gamepad, SdlNative.SdlGamepadButton.Start) != 0,
            SdlNative.SDL_GetGamepadAxis(_gamepad, SdlNative.SdlGamepadAxis.LeftX),
            SdlNative.SDL_GetGamepadAxis(_gamepad, SdlNative.SdlGamepadAxis.LeftY),
            string.Join('+', pressed));
    }

    private void AddIfPressed(HashSet<ControllerButton> pressed, ControllerButton button, SdlNative.SdlGamepadButton sdlButton)
    {
        if (SdlNative.SDL_GetGamepadButton(_gamepad, sdlButton) != 0) pressed.Add(button);
    }

    private static bool IsCaptureActive(ControllerCapturePhase phase)
        => phase is ControllerCapturePhase.WaitingForRelease
            or ControllerCapturePhase.WaitingForFirstPress
            or ControllerCapturePhase.Recording
            or ControllerCapturePhase.WaitingForCombinationRelease;

    private static string GetCaptureStatusText(ControllerCapturePhase phase)
        => phase switch
        {
            ControllerCapturePhase.WaitingForRelease => "Puść wszystkie przyciski kontrolera.",
            ControllerCapturePhase.WaitingForFirstPress => "Naciśnij wybraną kombinację.",
            ControllerCapturePhase.Recording => "Przytrzymaj kombinację przez chwilę.",
            ControllerCapturePhase.WaitingForCombinationRelease => "Puść kombinację, aby zakończyć.",
            ControllerCapturePhase.Completed => "Przypisano kombinację.",
            ControllerCapturePhase.TimedOut => "Nasłuchiwanie wygasło.",
            ControllerCapturePhase.Cancelled => "Nasłuchiwanie anulowane.",
            _ => "Kontroler SDL3 wykryty"
        };

    private async Task HandleShortcutsAsync(string controllerId, HashSet<ControllerButton> pressed, CancellationToken cancellationToken)
    {
        List<(string ProfileName, ControllerShortcutSettings Shortcut)> shortcuts;
        ControllerShortcutSettings? workModeShortcut;
        lock (_syncRoot)
        {
            shortcuts = _settings.Display.PcGamingProfileConfigurations
                .Where(configuration => !string.IsNullOrWhiteSpace(configuration.ProfileName) && configuration.ControllerShortcut is not null)
                .Select(configuration => (configuration.ProfileName, configuration.ControllerShortcut!))
                .Where(entry => entry.Item2.Buttons.Count > 0).ToList();
            workModeShortcut = _settings.Controller.WorkMode is { Buttons.Count: > 0 }
                ? _settings.Controller.WorkMode
                : null;
        }
        foreach (var (profileName, shortcut) in shortcuts)
        {
            var key = $"{controllerId}:{profileName}";
            if (!pressed.SetEquals(shortcut.Buttons))
            {
                lock (_syncRoot) { _pressedSince.Remove(key); _triggeredProfiles.Remove(key); }
                continue;
            }
            var shouldTrigger = false;
            var now = DateTimeOffset.UtcNow;
            lock (_syncRoot)
            {
                if (!_pressedSince.TryGetValue(key, out var startedAt)) _pressedSince[key] = now;
                else if (!_triggeredProfiles.Contains(key) && now - startedAt >= TimeSpan.FromMilliseconds(Math.Clamp(shortcut.HoldDurationMs, 300, 3000)))
                {
                    _triggeredProfiles.Add(key);
                    shouldTrigger = true;
                }
            }
            if (shouldTrigger)
            {
                _logger.LogInformation(
                    "Controller shortcut matched. Profile={ProfileName}, ControllerId={ControllerId}, Buttons=[{Buttons}], HoldDurationMs={HoldDurationMs}.",
                    profileName,
                    controllerId,
                    string.Join(',', shortcut.Buttons),
                    shortcut.HoldDurationMs);
                await TriggerProfileAsync(profileName, cancellationToken);
            }
        }

        if (workModeShortcut is null)
        {
            return;
        }

        const string workModeKey = "WorkMode";
        if (!pressed.SetEquals(workModeShortcut.Buttons))
        {
            lock (_syncRoot) { _pressedSince.Remove(workModeKey); _triggeredProfiles.Remove(workModeKey); }
            return;
        }

        var shouldTriggerWorkMode = false;
        var currentTime = DateTimeOffset.UtcNow;
        lock (_syncRoot)
        {
            if (!_pressedSince.TryGetValue(workModeKey, out var startedAt)) _pressedSince[workModeKey] = currentTime;
            else if (!_triggeredProfiles.Contains(workModeKey) && currentTime - startedAt >= TimeSpan.FromMilliseconds(Math.Clamp(workModeShortcut.HoldDurationMs, 300, 3000)))
            {
                _triggeredProfiles.Add(workModeKey);
                shouldTriggerWorkMode = true;
            }
        }

        if (shouldTriggerWorkMode)
        {
            _logger.LogInformation(
                "Controller shortcut matched. Mode=WorkMode, ControllerId={ControllerId}, Buttons=[{Buttons}], HoldDurationMs={HoldDurationMs}.",
                controllerId,
                string.Join(',', workModeShortcut.Buttons),
                workModeShortcut.HoldDurationMs);
            await TriggerWorkModeAsync(cancellationToken);
        }
    }

    private async Task TriggerProfileAsync(string profileName, CancellationToken cancellationToken)
    {
        lock (_syncRoot) { if (_isExecutingCommand) return; _isExecutingCommand = true; }
        try
        {
            var result = await _modeCommandService.EnterGameShellWithDisplayProfileAsync(profileName, cancellationToken);
            _logger.LogInformation(
                "Controller shortcut profile transition finished. Profile={ProfileName}, Status={Status}, Message={Message}.",
                profileName,
                result.Status,
                result.Message);
            _webViewBridge.NotifyStatusChanged();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Controller shortcut failed for profile {ProfileName}.", profileName); }
        finally { lock (_syncRoot) { _isExecutingCommand = false; } }
    }

    private async Task TriggerWorkModeAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot) { if (_isExecutingCommand) return; _isExecutingCommand = true; }
        try
        {
            var result = await _modeCommandService.EnterWorkModeAsync(cancellationToken);
            _logger.LogInformation(
                "Controller shortcut Work Mode transition finished. Status={Status}, Message={Message}.",
                result.Status,
                result.Message);
            _webViewBridge.NotifyStatusChanged();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Controller shortcut failed for Work Mode."); }
        finally { lock (_syncRoot) { _isExecutingCommand = false; } }
    }

    private void SetDisconnected(string reason)
    {
        lock (_syncRoot)
        {
            var activeSessionId = _captureSessionId;
            var wasCaptureActive = IsCaptureActive(_captureMachine.Phase);
            _lastControllerId = null;
            _lastDeviceName = null;
            _lastPressedButtons.Clear();
            _lastReadingAt = DateTimeOffset.MinValue;
            _diagnosticStatus = reason;
            _pressedSince.Clear();
            _triggeredProfiles.Clear();
            _captureMachine.Cancel();
            _captureMachine.Reset();
            _isCaptureActive = false;
            if (activeSessionId is not null && wasCaptureActive)
            {
                _logger.LogInformation("Capture {SessionId} cancelled reason=ControllerDisconnected.", activeSessionId);
            }
            _captureSessionId = null;
            _captureProfileId = null;
            _captureStartedAt = null;
            _completedCapture = null;
            _latestSnapshot = new ControllerInputSnapshot(
                string.Empty,
                new HashSet<ControllerButton>(),
                ++_snapshotSequence,
                DateTimeOffset.UtcNow);
        }
        NotifyControllerStatusChanged();
    }

    private void NotifyControllerStatusChanged()
    {
        long sequenceNumber;
        bool isConnected;
        string? deviceName;
        lock (_syncRoot)
        {
            sequenceNumber = ++_controllerStatusChangeSequence;
            isConnected = _gamepad != IntPtr.Zero;
            deviceName = _lastDeviceName;
        }

        _logger.LogInformation(
            "Controller status changed #{SequenceNumber}. Connected={Connected}, Name={Name}, Family={Family}.",
            sequenceNumber,
            isConnected,
            deviceName ?? "(none)",
            GetControllerFamilyForDiagnostics(deviceName));
        _webViewBridge.NotifyControllerStatusChanged(sequenceNumber, isConnected, deviceName);
    }

    private static string GetControllerFamilyForDiagnostics(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName)) return "Generic";
        if (deviceName.Contains("DualSense", StringComparison.OrdinalIgnoreCase)
            || deviceName.Contains("PlayStation", StringComparison.OrdinalIgnoreCase)
            || deviceName.Contains("PS5", StringComparison.OrdinalIgnoreCase)) return "PlayStation";
        if (deviceName.Contains("Xbox", StringComparison.OrdinalIgnoreCase)) return "Xbox";
        if (deviceName.Contains("Nintendo", StringComparison.OrdinalIgnoreCase)
            || deviceName.Contains("Switch", StringComparison.OrdinalIgnoreCase)) return "Nintendo";
        return "Generic";
    }

    private object GetCaptureSessionStatus()
    {
        return new
        {
            captureSessionId = _captureSessionId,
            profileId = _captureProfileId,
            capturePhase = _captureMachine.Phase.ToString(),
            startedAt = _captureStartedAt,
            capturedButtons = _captureMachine.CapturedButtons.Select(button => button.ToString()).ToArray(),
            completedCapture = _completedCapture is null ? null : new
            {
                sessionId = _completedCapture.SessionId,
                profileId = _completedCapture.ProfileId,
                buttons = _completedCapture.Buttons.Select(button => button.ToString()).ToArray(),
                completedAt = _completedCapture.CompletedAt
            }
        };
    }

    private void CloseSdl()
    {
        if (_gamepad != IntPtr.Zero) { SdlNative.SDL_CloseGamepad(_gamepad); _gamepad = IntPtr.Zero; }
        _gamepadId = 0;
        if ((SdlNative.SDL_WasInit(SdlNative.InitFlags.Gamepad) & SdlNative.InitFlags.Gamepad) != 0) SdlNative.SDL_Quit();
    }

    public void Dispose() => CloseSdl();
}