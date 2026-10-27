using GameShell.Core.Profiles;
using GameShell.Core.Runtime;
using GameShell.Core.Settings;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.Shortcuts;

internal sealed class GlobalHotkeyHost : IDisposable
{
    private const int HotkeyConsoleModeId = 0x4701;
    private const int HotkeyGamingProfileModeId = 0x4702;
    private const int HotkeyWorkModeId = 0x4703;
    private const int HotkeyProfileEnterBaseId = 0x4800;

    private readonly ISettingsService _settingsService;
    private readonly IModeCommandService _modeCommandService;
    private readonly ProfileSettingsResolver _profileSettingsResolver;
    private readonly ILogger _logger;
    private readonly Func<Func<Task<ModeResult>>, Task> _runModeCommandAsync;
    private readonly Action _notifyStatusChanged;
    private readonly Dictionary<int, string> _profileEnterHotkeys = new();

    private GlobalHotkeyMessageWindow? _source;
    private bool _isCaptureActive;
    private bool _disposed;

    public GlobalHotkeyHost(
        ISettingsService settingsService,
        IModeCommandService modeCommandService,
        ProfileSettingsResolver profileSettingsResolver,
        ILogger logger,
        Func<Func<Task<ModeResult>>, Task> runModeCommandAsync,
        Action notifyStatusChanged)
    {
        _settingsService = settingsService;
        _modeCommandService = modeCommandService;
        _profileSettingsResolver = profileSettingsResolver;
        _logger = logger;
        _runModeCommandAsync = runModeCommandAsync;
        _notifyStatusChanged = notifyStatusChanged;
    }

    public void Start()
    {
        ThrowIfDisposed();
        _source = new GlobalHotkeyMessageWindow(OnHotkeyPressed);
        _ = ReloadAsync();
    }

    public Task ReloadAsync()
    {
        if (_disposed || _source is null || _isCaptureActive)
        {
            return Task.CompletedTask;
        }

        return RegisterConfiguredHotkeysAsync();
    }

    public void SetCaptureActive(bool isActive)
    {
        if (_disposed || _isCaptureActive == isActive)
        {
            return;
        }

        _isCaptureActive = isActive;
        if (isActive)
        {
            UnregisterAllHotkeys();
            return;
        }

        _ = ReloadAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterAllHotkeys();
        _source?.Dispose();
        _source = null;
    }

    private void OnHotkeyPressed(int hotkeyId)
    {
        if (_disposed)
        {
            return;
        }

        _ = HandleHotkeyAsync(hotkeyId);
    }

    private async Task HandleHotkeyAsync(int hotkeyId)
    {
        if (_profileEnterHotkeys.TryGetValue(hotkeyId, out var profileName))
        {
            if (!_isCaptureActive)
            {
                await RunGamingHotkeyAsync(profileName);
            }

            return;
        }

        if (_isCaptureActive)
        {
            return;
        }

        switch (hotkeyId)
        {
            case HotkeyConsoleModeId:
                await _runModeCommandAsync(() => _modeCommandService.EnterGameShellAsync());
                break;
            case HotkeyGamingProfileModeId:
                await RunGamingHotkeyAsync(null);
                break;
            case HotkeyWorkModeId:
                await _runModeCommandAsync(() => _modeCommandService.EnterWorkModeAsync());
                break;
        }
    }

    private async Task RegisterConfiguredHotkeysAsync()
    {
        UnregisterAllHotkeys();

        try
        {
            var settings = await _settingsService.LoadAsync();
            RegisterHotkey(HotkeyConsoleModeId, settings.Hotkeys?.ConsoleMode ?? string.Empty);

            var hasProfileEnterHotkeys = false;
            for (var index = 0; index < settings.Display.GamingProfileConfigurations.Count; index++)
            {
                var configuration = settings.Display.GamingProfileConfigurations[index];
                if (string.IsNullOrWhiteSpace(configuration.ProfileName))
                {
                    continue;
                }

                var enterId = HotkeyProfileEnterBaseId + index;
                if (RegisterHotkey(enterId, configuration.Hotkeys?.EnterProfile ?? string.Empty))
                {
                    _profileEnterHotkeys[enterId] = configuration.ProfileName;
                    hasProfileEnterHotkeys = true;
                }
            }

            if (!hasProfileEnterHotkeys)
            {
                RegisterHotkey(HotkeyGamingProfileModeId, settings.Hotkeys?.GamingMode ?? string.Empty);
            }

            RegisterHotkey(HotkeyWorkModeId, settings.Hotkeys?.WorkMode ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register configured hotkeys.");
        }
    }

    private bool RegisterHotkey(int id, string hotkey)
    {
        if (_source is null || !GlobalHotkeyParser.TryParse(hotkey, out var modifiers, out var virtualKey))
        {
            return false;
        }

        if (!RegisterHotKey(_source.Handle, id, modifiers, virtualKey))
        {
            _logger.LogWarning("Failed to register hotkey {Hotkey}.", hotkey);
            return false;
        }

        return true;
    }

    private void UnregisterAllHotkeys()
    {
        if (_source is not null)
        {
            UnregisterHotkey(HotkeyConsoleModeId);
            UnregisterHotkey(HotkeyGamingProfileModeId);
            UnregisterHotkey(HotkeyWorkModeId);
            foreach (var hotkeyId in _profileEnterHotkeys.Keys.ToList())
            {
                UnregisterHotkey(hotkeyId);
            }
        }

        _profileEnterHotkeys.Clear();
    }

    private void UnregisterHotkey(int id)
    {
        if (_source is not null && _source.Handle != IntPtr.Zero)
        {
            UnregisterHotKey(_source.Handle, id);
        }
    }

    private async Task RunGamingHotkeyAsync(string? forcedProfileName)
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            var profileName = _profileSettingsResolver.ResolveProfileName(settings.Display, forcedProfileName);
            if (settings.Display.GamingLayout is not null || settings.Display.GamingProfiles.Count > 0)
            {
                await _modeCommandService.EnterGameShellWithDisplayProfileAsync(profileName ?? string.Empty);
            }
            else
            {
                await _modeCommandService.EnterGameShellAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gaming profile hotkey command failed.");
        }
        finally
        {
            _notifyStatusChanged();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
