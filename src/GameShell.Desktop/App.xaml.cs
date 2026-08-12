using System.Windows;
using System.IO;
using System.Windows.Input;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using GameShell.Desktop.Logging;
using GameShell.Desktop.Services;
using GameShell.Windows.Detection;
using GameShell.Windows.Displays;
using GameShell.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop;

public partial class App : System.Windows.Application
{
    private const int HotkeyConsoleModeId = 0x4701;
    private const int HotkeyPcGamingModeId = 0x4702;
    private const int HotkeyWorkModeId = 0x4703;
    private const int HotkeyProfileEnterBaseId = 0x4800;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const int WmHotkey = 0x0312;
    private const string SingleInstanceMutexName = @"Local\GameShell.Desktop.SingleInstance";
    private const string ShowWindowEventName = @"Local\GameShell.Desktop.ShowWindow";
    private const string GamingCommandEventName = @"Local\GameShell.Desktop.Command.Gaming";
    private const string PcGamingCommandEventName = @"Local\GameShell.Desktop.Command.PcGaming";
    private const string WorkCommandEventName = @"Local\GameShell.Desktop.Command.Work";

    private ServiceProvider? _serviceProvider;
    private Mutex? _singleInstanceMutex;
    private readonly List<EventWaitHandle> _commandEvents = [];
    private readonly List<RegisteredWaitHandle> _commandRegistrations = [];
    private MainWindow? _mainWindow;
    private SdlControllerShortcutService? _controllerShortcutService;
    private WebView.WebViewBridge? _webViewBridge;
    private ISettingsService? _settingsService;
    private IModeCommandService? _modeCommandService;
    private IDisplayTransitionService? _displayTransitionService;
    private GlobalHotkeyMessageWindow? _hotkeySource;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _trayMenu;
    private AppSettings _traySettings = new();
    private AppStatus _trayStatus = new();
    private RuntimeState _trayRuntimeState = new();
    private readonly Dictionary<int, string> _profileEnterHotkeys = new();
    private bool _isHotkeyCaptureActive;
    private bool _isExiting;
    private ILogger<App>? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            SignalExistingInstance(e.Args);
            _isExiting = true;
            _ = ShutdownApplicationAsync();
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("GameShell starting. Args: {Args}", string.Join(' ', e.Args));

        _settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        _modeCommandService = _serviceProvider.GetRequiredService<IModeCommandService>();
        _displayTransitionService = _serviceProvider.GetRequiredService<IDisplayTransitionService>();
        var startupSettings = await _settingsService.LoadAsync();
        RegisterCommandEvents();
        _controllerShortcutService = _serviceProvider.GetRequiredService<SdlControllerShortcutService>();
        _webViewBridge = _serviceProvider.GetRequiredService<WebView.WebViewBridge>();
        _webViewBridge.AttachControllerShortcutService(_controllerShortcutService);
        _webViewBridge.ApplyWindowsIntegrationForCurrentSettings(startupSettings);
        _webViewBridge.HotkeyCaptureStateChanged += OnHotkeyCaptureStateChanged;
        _ = _controllerShortcutService.StartAsync();

        InitializeTray(startupSettings);
        InitializeHotkeys();
        _logger.LogInformation("Startup mode: {StartupMode}", startupSettings.Startup.StartMinimizedToTray ? "BackgroundTray" : "Interactive");
        _logger.LogInformation("Tray initialized");
        _logger.LogInformation("Hotkeys initialized");
        _logger.LogInformation("Controller service initialized");

        if (!startupSettings.Startup.StartMinimizedToTray)
        {
            ShowMainWindow();
        }

        RunStartupCommand(e.Args);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("Application exiting");
        base.OnExit(e);
    }

    private void RegisterCommandEvents()
    {
        RegisterCommandEvent(ShowWindowEventName, () => ShowMainWindow());
        RegisterCommandEvent(GamingCommandEventName, async () => await RunCommandAsync(service => service.EnterGameShellAsync()));
        RegisterCommandEvent(PcGamingCommandEventName, RunPcGamingCommandAsync);
        RegisterCommandEvent(WorkCommandEventName, async () => await RunCommandAsync(service => service.EnterWorkModeAsync()));
    }

    private MainWindow EnsureMainWindow()
    {
        if (_mainWindow is not null)
        {
            return _mainWindow;
        }

        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("Services are not initialized.");
        }

        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        _mainWindow.Closed += OnMainWindowClosed;
        _logger?.LogInformation("MainWindow created");
        return _mainWindow;
    }

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _mainWindow))
        {
            _mainWindow = null;
        }
    }

    private void ShowMainWindow()
    {
        if (_isExiting)
        {
            return;
        }

        try
        {
            _logger?.LogInformation("MainWindow requested");
            var window = EnsureMainWindow();
            window.Show();
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            _logger?.LogInformation("MainWindow shown");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show MainWindow.");
        }
    }

    private void InitializeTray(AppSettings startupSettings)
    {
        _traySettings = startupSettings;
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Opening += (_, _) => RebuildTrayMenu();
        RebuildTrayMenu();
        _ = RefreshTrayStateAsync();

        _notifyIcon = new NotifyIcon
        {
            Text = "GameShell",
            Icon = LoadTrayIcon(),
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
    }

    private async Task RefreshTrayStateAsync()
    {
        if (_settingsService is null || _modeCommandService is null || _displayTransitionService is null)
        {
            return;
        }

        try
        {
            var settingsTask = _settingsService.LoadAsync();
            var statusTask = _modeCommandService.GetStatusAsync();
            var runtimeStateTask = _displayTransitionService.LoadRuntimeStateAsync();
            await Task.WhenAll(settingsTask, statusTask, runtimeStateTask);
            _traySettings = settingsTask.Result;
            _trayStatus = statusTask.Result;
            _trayRuntimeState = runtimeStateTask.Result;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to refresh tray menu state.");
        }
    }

    private void RebuildTrayMenu()
    {
        if (_trayMenu is null)
        {
            return;
        }

        var language = _traySettings.Ui.Language ?? "en";
        _trayMenu.Items.Clear();

        var openItem = new ToolStripMenuItem(TrayMenuText.OpenGameShell(language))
        {
            Enabled = true,
            Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold)
        };
        openItem.Click += (_, _) =>
        {
            _logger?.LogInformation("Tray click: Open GameShell");
            ShowMainWindow();
        };
        _trayMenu.Items.Add(openItem);
        _trayMenu.Items.Add(new ToolStripSeparator());

        var currentMode = _trayStatus.CurrentMode ?? _trayRuntimeState.CurrentMode?.ToString();
        var isWorkMode = string.Equals(currentMode, GameShellMode.Work.ToString(), StringComparison.OrdinalIgnoreCase);
        var isProfileMode = string.Equals(currentMode, GameShellMode.PcGaming.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentMode, GameShellMode.Console.ToString(), StringComparison.OrdinalIgnoreCase);
        var activeProfileName = _traySettings.Display.ActivePcGamingProfileName;
        var currentLabel = TrayMenuText.CurrentStatus(language, isWorkMode, activeProfileName);
        _trayMenu.Items.Add(new ToolStripMenuItem(currentLabel) { Enabled = false });

        var profilesItem = new ToolStripMenuItem(TrayMenuText.GamingProfiles(language)) { Enabled = true };
        if (_traySettings.Display.PcGamingProfiles.Count == 0)
        {
            profilesItem.DropDownItems.Add(new ToolStripMenuItem(TrayMenuText.NoGamingProfiles(language)) { Enabled = false });
        }
        else
        {
            foreach (var profile in _traySettings.Display.PcGamingProfiles)
            {
                var profileItem = new ToolStripMenuItem(profile.Name)
                {
                    Enabled = true,
                    Checked = isProfileMode && string.Equals(activeProfileName, profile.Name, StringComparison.OrdinalIgnoreCase),
                    CheckOnClick = false
                };
                var profileName = profile.Name;
                _logger?.LogInformation("Tray profile item: {ProfileName} enabled={Enabled}", profileName, profileItem.Enabled);
                profileItem.Click += async (_, _) =>
                {
                    _logger?.LogInformation("Tray click: Profile={ProfileName}", profileName);
                    await RunTrayCommandAsync(
                        () => _modeCommandService!.EnterGameShellWithDisplayProfileAsync(profileName));
                };
                profilesItem.DropDownItems.Add(profileItem);
            }
        }

        _logger?.LogInformation(
            "Tray menu rebuilt. Open enabled={OpenEnabled}; Profiles enabled={ProfilesEnabled} count={ProfileCount}; WorkMode enabled={WorkModeEnabled}; Exit enabled={ExitEnabled}",
            openItem.Enabled,
            profilesItem.Enabled,
            _traySettings.Display.PcGamingProfiles.Count,
            true,
            true);

        _trayMenu.Items.Add(profilesItem);
        _trayMenu.Items.Add(new ToolStripSeparator());

        var workModeItem = new ToolStripMenuItem(TrayMenuText.ReturnToWorkMode(language)) { Enabled = true };
        workModeItem.Click += async (_, _) =>
        {
            _logger?.LogInformation("Tray click: Work Mode");
            await RunTrayCommandAsync(() => _modeCommandService!.EnterWorkModeAsync());
        };
        _trayMenu.Items.Add(workModeItem);
        _trayMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem(TrayMenuText.CloseGameShell(language)) { Enabled = true };
        exitItem.Click += (_, _) => OnExitMenuClick();
        _trayMenu.Items.Add(exitItem);
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "GameShell.ico");
        return File.Exists(iconPath)
            ? new System.Drawing.Icon(iconPath, SystemInformation.SmallIconSize.Width, SystemInformation.SmallIconSize.Height)
            : System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? System.Drawing.SystemIcons.Application;
    }

    private void OnExitMenuClick()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _logger?.LogInformation("Exit requested from tray. Thread={ThreadId}", Environment.CurrentManagedThreadId);
        _trayMenu?.Close();
        _logger?.LogInformation("Tray menu close requested. Thread={ThreadId}", Environment.CurrentManagedThreadId);

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                _logger?.LogInformation("Shutdown scheduled. Thread={ThreadId}", Environment.CurrentManagedThreadId);
                _ = ShutdownApplicationAsync();
            }));
    }

    private async Task ShutdownApplicationAsync()
    {
        _logger?.LogInformation("Shutdown begin. Thread={ThreadId}", Environment.CurrentManagedThreadId);

        try
        {
            _logger?.LogInformation("MainWindow close begin. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            try
            {
                if (_mainWindow is not null)
                {
                    _mainWindow.AllowApplicationExit();
                    await _mainWindow.SaveBoundsForApplicationExitAsync();
                    _mainWindow.Close();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MainWindow close failed. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            }
            finally
            {
                _logger?.LogInformation("MainWindow close complete. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            }

            _logger?.LogInformation("Hotkeys unregister begin. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            try
            {
                UnregisterAllHotkeys();
            }
            finally
            {
                _logger?.LogInformation("Hotkeys unregister complete. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            }

            _logger?.LogInformation("Controller service stop begin. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            try
            {
                if (_controllerShortcutService is not null)
                {
                    await _controllerShortcutService.StopAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Controller service stop failed. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            }
            finally
            {
                _logger?.LogInformation("Controller service stop complete. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            }

            if (_webViewBridge is not null)
            {
                _webViewBridge.HotkeyCaptureStateChanged -= OnHotkeyCaptureStateChanged;
            }

            _logger?.LogInformation("Message host dispose begin. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            try
            {
                _hotkeySource?.Dispose();
                _hotkeySource = null;
            }
            finally
            {
                _logger?.LogInformation("Message host dispose complete. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            }

            _logger?.LogInformation("Tray dispose begin. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            try
            {
                if (_notifyIcon is not null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.ContextMenuStrip = null;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }

                _trayMenu?.Dispose();
                _trayMenu = null;
            }
            finally
            {
                _logger?.LogInformation("Tray dispose complete. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            }

            foreach (var registration in _commandRegistrations)
            {
                registration.Unregister(null);
            }

            foreach (var commandEvent in _commandEvents)
            {
                commandEvent.Dispose();
            }

            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Shutdown cleanup failed. Thread={ThreadId}", Environment.CurrentManagedThreadId);
        }
        finally
        {
            _logger?.LogInformation("Application.Shutdown begin. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            Shutdown();
        }
    }

    private async Task RunTrayCommandAsync(Func<Task<GameShell.Core.Models.ModeResult>> command)
    {
        if (_isExiting)
        {
            return;
        }

        try
        {
            await command();
        }
        catch (Exception ex)
        {
            _serviceProvider?.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>().LogError(ex, "Tray command failed.");
        }
        finally
        {
            _webViewBridge?.NotifyStatusChanged();
            _ = RefreshTrayStateAsync();
        }
    }

    private async Task RestorePreviousDisplayLayoutFromTrayAsync()
    {
        if (_isExiting || _displayTransitionService is null)
        {
            return;
        }

        try
        {
            var result = await _displayTransitionService.RestorePreviousLayoutAsync();
            if (!result.Success)
            {
                _notifyIcon?.ShowBalloonTip(4000, "GameShell", result.UserMessage ?? "Nie udało się przywrócić układu ekranów.", ToolTipIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _serviceProvider?.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>().LogError(ex, "Restore previous display layout tray command failed.");
        }
        finally
        {
            _webViewBridge?.NotifyStatusChanged();
            _ = RefreshTrayStateAsync();
        }
    }

    private void InitializeHotkeys()
    {
        _hotkeySource = new GlobalHotkeyMessageWindow(OnHotkeyPressed);
        _webViewBridge!.SettingsSaved += OnSettingsSaved;
        _ = RegisterConfiguredHotkeysAsync();
    }

    private void OnHotkeyPressed(int hotkeyId)
    {
        if (_isExiting)
        {
            return;
        }

        _ = HandleHotkeyAsync(hotkeyId);
    }

    private async Task HandleHotkeyAsync(int hotkeyId)
    {
        if (_profileEnterHotkeys.TryGetValue(hotkeyId, out var profileName))
        {
            if (!_isHotkeyCaptureActive)
            {
                await RunPcGamingHotkeyAsync(profileName);
            }

            return;
        }

        if (_isHotkeyCaptureActive)
        {
            return;
        }

        switch (hotkeyId)
        {
            case HotkeyConsoleModeId:
                await RunTrayCommandAsync(() => _modeCommandService!.EnterGameShellAsync());
                break;
            case HotkeyPcGamingModeId:
                await RunPcGamingHotkeyAsync(null);
                break;
            case HotkeyWorkModeId:
                await RunTrayCommandAsync(() => _modeCommandService!.EnterWorkModeAsync());
                break;
        }
    }

    private async void OnSettingsSaved(object? sender, EventArgs e)
    {
        await RegisterConfiguredHotkeysAsync();
        await RefreshTrayStateAsync();
    }

    private async Task RegisterConfiguredHotkeysAsync()
    {
        if (_hotkeySource is null || _settingsService is null)
        {
            return;
        }

        UnregisterAllHotkeys();

        try
        {
            var settings = await _settingsService.LoadAsync();
            RegisterHotkey(HotkeyConsoleModeId, settings.Hotkeys?.ConsoleMode ?? "");

            var hasProfileEnterHotkeys = false;
            for (var index = 0; index < settings.Display.PcGamingProfileConfigurations.Count; index++)
            {
                var configuration = settings.Display.PcGamingProfileConfigurations[index];
                if (string.IsNullOrWhiteSpace(configuration.ProfileName))
                {
                    continue;
                }

                var enterId = HotkeyProfileEnterBaseId + index;
                if (RegisterHotkey(enterId, configuration.Hotkeys?.EnterProfile ?? ""))
                {
                    _profileEnterHotkeys[enterId] = configuration.ProfileName;
                    hasProfileEnterHotkeys = true;
                }
            }

            if (!hasProfileEnterHotkeys)
            {
                RegisterHotkey(HotkeyPcGamingModeId, settings.Hotkeys?.PcGamingMode ?? "");
            }

            RegisterHotkey(HotkeyWorkModeId, settings.Hotkeys?.WorkMode ?? "");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to register configured hotkeys.");
        }
    }

    private bool RegisterHotkey(int id, string hotkey)
    {
        if (_hotkeySource is null || !TryParseHotkey(hotkey, out var modifiers, out var virtualKey))
        {
            return false;
        }

        if (!RegisterHotKey(_hotkeySource.Handle, id, modifiers, virtualKey))
        {
            _logger?.LogWarning("Failed to register hotkey {Hotkey}.", hotkey);
            return false;
        }

        return true;
    }

    private void UnregisterAllHotkeys()
    {
        if (_hotkeySource is not null)
        {
            UnregisterHotkey(HotkeyConsoleModeId);
            UnregisterHotkey(HotkeyPcGamingModeId);
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
        if (_hotkeySource is not null && _hotkeySource.Handle != IntPtr.Zero)
        {
            UnregisterHotKey(_hotkeySource.Handle, id);
        }
    }

    private async Task RunPcGamingHotkeyAsync(string? forcedProfileName)
    {
        try
        {
            var settings = await _settingsService!.LoadAsync();
            var profileName = ResolveRequestedProfileName(settings, forcedProfileName);
            if (settings.Display.PcGamingLayout is not null || settings.Display.PcGamingProfiles.Count > 0)
            {
                await _modeCommandService!.EnterGameShellWithDisplayProfileAsync(profileName ?? "PcGaming");
            }
            else
            {
                await _modeCommandService!.EnterGameShellAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "PC Gaming hotkey command failed.");
        }
        finally
        {
            _webViewBridge?.NotifyStatusChanged();
        }
    }

    private static string? ResolveRequestedProfileName(AppSettings settings, string? forcedProfileName)
    {
        if (!string.IsNullOrWhiteSpace(forcedProfileName)
            && settings.Display.PcGamingProfiles.Any(profile => string.Equals(profile.Name, forcedProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            return forcedProfileName;
        }

        if (!string.IsNullOrWhiteSpace(settings.Display.ActivePcGamingProfileName)
            && settings.Display.PcGamingProfiles.Any(profile => string.Equals(profile.Name, settings.Display.ActivePcGamingProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            return settings.Display.ActivePcGamingProfileName;
        }

        return settings.Display.PcGamingProfiles.FirstOrDefault()?.Name;
    }

    private async void OnHotkeyCaptureStateChanged(object? sender, bool isActive)
    {
        if (_isHotkeyCaptureActive == isActive)
        {
            return;
        }

        _isHotkeyCaptureActive = isActive;
        if (isActive)
        {
            UnregisterAllHotkeys();
            return;
        }

        await RegisterConfiguredHotkeysAsync();
    }

    private static bool TryParseHotkey(string? value, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        foreach (var part in parts[..^1])
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) modifiers |= ModControl;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModAlt;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModShift;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase)) modifiers |= ModWin;
        }

        if (modifiers == 0)
        {
            return false;
        }

        var keyName = parts[^1].ToUpperInvariant();
        var key = keyName.Length == 1 && (keyName[0] is >= 'A' and <= 'Z' || keyName[0] is >= '0' and <= '9')
            ? KeyInterop.KeyFromVirtualKey(keyName[0])
            : Enum.TryParse<Key>(keyName, true, out var parsedKey) ? parsedKey : Key.None;
        if (key == Key.None)
        {
            return false;
        }

        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private void RegisterCommandEvent(string eventName, Func<Task> action)
    {
        var commandEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        var registration = ThreadPool.RegisterWaitForSingleObject(
            commandEvent,
            (_, _) => Dispatcher.BeginInvoke(action),
            null,
            Timeout.Infinite,
            false);

        _commandEvents.Add(commandEvent);
        _commandRegistrations.Add(registration);
    }

    private void RegisterCommandEvent(string eventName, Action action)
    {
        RegisterCommandEvent(eventName, () =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    private async Task RunCommandAsync(Func<IModeCommandService, Task<GameShell.Core.Models.ModeResult>> action)
    {
        if (_isExiting || _serviceProvider is null)
        {
            return;
        }

        var modeCommandService = _serviceProvider.GetRequiredService<IModeCommandService>();
        await action(modeCommandService);
    }

    private async Task RunPcGamingCommandAsync()
    {
        if (_isExiting || _serviceProvider is null)
        {
            return;
        }

        var settings = await _serviceProvider.GetRequiredService<ISettingsService>().LoadAsync();
        var modeCommandService = _serviceProvider.GetRequiredService<IModeCommandService>();

        if (settings.Display.PcGamingLayout is not null || settings.Display.PcGamingProfiles.Count > 0)
        {
            await modeCommandService.EnterGameShellWithDisplayProfileAsync("PcGaming");
            return;
        }

        await modeCommandService.EnterGameShellAsync();
    }

    private void RunStartupCommand(string[] args)
    {
        var eventName = GetCommandEventName(args);
        if (eventName is null)
        {
            return;
        }

        _ = eventName switch
        {
            GamingCommandEventName => RunCommandAsync(service => service.EnterGameShellAsync()),
            PcGamingCommandEventName => RunPcGamingCommandAsync(),
            WorkCommandEventName => RunCommandAsync(service => service.EnterWorkModeAsync()),
            _ => Task.CompletedTask
        };
    }

    private static void SignalExistingInstance(string[] args)
    {
        try
        {
            using var commandEvent = EventWaitHandle.OpenExisting(GetCommandEventName(args) ?? ShowWindowEventName);
            commandEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    private static string? GetCommandEventName(string[] args)
    {
        if (args.Any(arg => arg.Equals("--gaming", StringComparison.OrdinalIgnoreCase)))
        {
            return GamingCommandEventName;
        }

        if (args.Any(arg => arg.Equals("--pcgaming", StringComparison.OrdinalIgnoreCase)))
        {
            return PcGamingCommandEventName;
        }

        if (args.Any(arg => arg.Equals("--work", StringComparison.OrdinalIgnoreCase)))
        {
            return WorkCommandEventName;
        }

        return null;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new FileLoggerProvider());
        });

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAudioDeviceService, WindowsAudioDeviceService>();
        services.AddSingleton<IModeWorkflowService, NativeModeWorkflowService>();
        services.AddSingleton<IRuntimeStateService, RuntimeStateService>();
        services.AddSingleton<ConsoleDisplayLayoutBuilder>();
        services.AddSingleton<GamingProfileTopologyReadinessEvaluator>();
        services.AddSingleton<WindowsDisplayConfigurationService>();
        services.AddSingleton<IDisplayQueryService>(provider => provider.GetRequiredService<WindowsDisplayConfigurationService>());
        services.AddSingleton<IDisplayConfigurationService>(provider => provider.GetRequiredService<WindowsDisplayConfigurationService>());
        services.AddSingleton<IDisplayLayoutApplicationService>(provider => provider.GetRequiredService<WindowsDisplayConfigurationService>());
        services.AddSingleton<IDisplayRecoveryService, WindowsDisplayRecoveryService>();
        services.AddSingleton<IDisplayTransitionService, DisplayTransitionService>();
        services.AddSingleton<IModeOrchestrator, ModeOrchestrator>();
        services.AddSingleton<IGameSessionMonitor, WindowsGameSessionMonitor>();
        services.AddSingleton<ILauncherRecoveryStrategy, WindowsLauncherRecoveryStrategy>();
        services.AddSingleton<IConsoleSessionManager, ConsoleSessionManager>();
        services.AddSingleton<IModeCommandService, ModeCommandService>();
        services.AddSingleton<IDependencyDetectionService, DependencyDetectionService>();
        services.AddSingleton<SdlControllerShortcutService>();
        services.AddSingleton<WebView.WebViewBridge>();
        services.AddTransient<MainWindow>();
    }
}
