using GameShell.Core.Displays;
using GameShell.Core.Profiles;
using GameShell.Core.Runtime;
using GameShell.Core.Settings;
using GameShell.Desktop.Bridge;
using GameShell.Desktop.Displays;
using GameShell.Desktop.RemoteControl;
using GameShell.Desktop.Shortcuts;
using GameShell.Desktop.Tray;
using GameShell.Desktop.Hosting;
using System.Windows;
using System.Windows.Threading;
using GameShell.Desktop.Logging;
using GameShell.Windows.Detection;
using GameShell.Windows.Displays;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\GameShell.Desktop.SingleInstance";
    private const string ShowWindowEventName = @"Local\GameShell.Desktop.ShowWindow";
    private const string ConsoleModeCommandEventName = @"Local\GameShell.Desktop.Command.ConsoleMode";
    private const string GamingProfileCommandEventName = @"Local\GameShell.Desktop.Command.GamingProfile";
    private const string WorkCommandEventName = @"Local\GameShell.Desktop.Command.Work";
    private const string DisplayCapabilityDiagnosticCommandEventName = @"Local\GameShell.Desktop.Command.DisplayCapabilityDiagnostic";

    private ServiceProvider? _serviceProvider;
    private Mutex? _singleInstanceMutex;
    private SingleInstanceCommandHost? _commandHost;
    private MainWindow? _mainWindow;
    private SdlControllerShortcutService? _controllerShortcutService;
    private WebViewBridge? _webViewBridge;
    private ISettingsService? _settingsService;
    private IModeCommandService? _modeCommandService;
    private IDisplayTransitionService? _displayTransitionService;
    private IDisplayCapabilityDiagnostic? _displayCapabilityDiagnostic;
    private RemoteControlHost? _remoteControlHost;
    private ProfileSettingsResolver? _profileSettingsResolver;
    private DisplayDiscoveryCoordinator? _displayDiscoveryCoordinator;
    private GlobalHotkeyHost? _hotkeyHost;
    private TrayHost? _trayHost;
    private Task? _showMainWindowTask;
    private readonly TaskCompletionSource<bool> _startupReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
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
        ServiceRegistration.ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        _displayDiscoveryCoordinator = _serviceProvider.GetRequiredService<DisplayDiscoveryCoordinator>();
        _displayDiscoveryCoordinator.StateChanged += OnDisplayDiscoveryStateChanged;
        _commandHost = new SingleInstanceCommandHost(Dispatcher);
        _logger.LogInformation("GameShell starting. Args: {Args}", string.Join(' ', e.Args));

        _settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        _modeCommandService = _serviceProvider.GetRequiredService<IModeCommandService>();
        _remoteControlHost = _serviceProvider.GetRequiredService<RemoteControlHost>();
        _displayTransitionService = _serviceProvider.GetRequiredService<IDisplayTransitionService>();
        _displayCapabilityDiagnostic = _serviceProvider.GetRequiredService<IDisplayCapabilityDiagnostic>();
        _profileSettingsResolver = _serviceProvider.GetRequiredService<ProfileSettingsResolver>();
        var startupSettings = await _settingsService.LoadAsync();
        var safeMode = HasArgument(e.Args, "--safe-mode");

        var startupStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var startupReconciliation = _serviceProvider.GetRequiredService<StartupReconciliationService>();
            ModeResult? reconciliationResult;
            var explicitCommand = GetCommandEventName(e.Args) is not null;
            if (safeMode)
            {
                reconciliationResult = await startupReconciliation.ReconcileAsync(
                    startupSettings,
                    explicitGamingStartup: false,
                    CancellationToken.None,
                    forceWorkTarget: true);
            }
            else if (!explicitCommand)
            {
                var runtimeState = await _serviceProvider.GetRequiredService<IRuntimeStateService>().LoadAsync();
                var startupTarget = _serviceProvider.GetRequiredService<StartupTargetResolver>()
                    .Resolve(startupSettings, runtimeState, safeMode: false);
                _logger.LogInformation(
                    "[STARTUP TARGET] Behavior={StartupBehavior} ResolvedTarget={ResolvedTarget} Reason={Reason}",
                    startupSettings.Startup.StartupBehavior, startupTarget, startupTarget.Reason);
                reconciliationResult = await startupReconciliation.ActivateTargetAsync(
                    startupSettings, startupTarget, CancellationToken.None);
            }
            else
            {
                reconciliationResult = await startupReconciliation.ReconcileAsync(
                    startupSettings,
                    explicitGamingStartup: explicitCommand,
                    CancellationToken.None);
            }

            if (reconciliationResult is { Success: false })
            {
                _logger.LogWarning(
                    "Startup display reconciliation completed without a usable Work topology. Message={Message}",
                    reconciliationResult.Message);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Startup display reconciliation failed.");
        }

        await _remoteControlHost.StartAsync(startupSettings);
        RegisterCommandEvents();
        _controllerShortcutService = _serviceProvider.GetRequiredService<SdlControllerShortcutService>();
        _webViewBridge = _serviceProvider.GetRequiredService<WebViewBridge>();
        _webViewBridge.AttachControllerShortcutService(_controllerShortcutService);
        _webViewBridge.ApplyWindowsIntegrationForCurrentSettings(startupSettings);
        _webViewBridge.HotkeyCaptureStateChanged += OnHotkeyCaptureStateChanged;
        _ = _controllerShortcutService.StartAsync();

        try
        {
            InitializeTray(startupSettings);
            _logger.LogInformation("Tray initialized");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[STARTUP] Component=Tray Result=Unavailable StartupContinues=True");
        }
        InitializeHotkeys();
        _logger.LogInformation("Startup mode: {StartupMode}", startupSettings.Startup.StartMinimizedToTray ? "BackgroundTray" : "Interactive");
        _logger.LogInformation("Hotkeys initialized");
        _logger.LogInformation("Controller service initialized");
        _startupReady.TrySetResult(true);
        _logger.LogInformation(
            "[STARTUP READY] DurationMs={DurationMs}",
            startupStopwatch.ElapsedMilliseconds);

        if (!startupSettings.Startup.StartMinimizedToTray || safeMode)
        {
            await ShowMainWindowAsync();
        }

        if (!safeMode)
        {
            RunStartupCommand(e.Args);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("Application exiting");
        base.OnExit(e);
    }

    private void RegisterCommandEvents()
    {
        RegisterCommandEvent(ShowWindowEventName, () => ShowMainWindow());
        RegisterCommandEvent(ConsoleModeCommandEventName, async () => await RunCommandAsync(service => service.EnterGameShellAsync()));
        RegisterCommandEvent(GamingProfileCommandEventName, RunGamingCommandAsync);
        RegisterCommandEvent(WorkCommandEventName, async () => await RunCommandAsync(service => service.EnterWorkModeAsync()));
        RegisterCommandEvent(DisplayCapabilityDiagnosticCommandEventName, async () =>
        {
            if (_displayCapabilityDiagnostic is not null)
            {
                await _displayCapabilityDiagnostic.RunAsync("CONSOLE");
            }
        });
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
        _ = ShowMainWindowAsync();
    }

    private async Task ShowMainWindowAsync()
    {
        if (_isExiting)
        {
            return;
        }

        if (_showMainWindowTask is not null)
        {
            await _showMainWindowTask;
            return;
        }

        _showMainWindowTask = ShowMainWindowCoreAsync();
        try
        {
            await _showMainWindowTask;
        }
        finally
        {
            _showMainWindowTask = null;
        }
    }

    private async Task ShowMainWindowCoreAsync()
    {
        try
        {
            await _startupReady.Task;
            _logger?.LogInformation("MainWindow requested");
            var window = EnsureMainWindow();
            try
            {
                await window.PrepareForPresentationAsync();
            }
            catch (Exception ex)
            {
                // Placement repair is best-effort. A settings/monitor API failure
                // must not leave the tray application without a recoverable window.
                _logger?.LogWarning(ex, "MainWindow placement preparation failed; showing with current bounds.");
            }

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

    private void OnDisplayDiscoveryStateChanged(object? sender, DisplayDiscoveryStateChangedEventArgs e)
    {
        if (e.OldState != DisplayDiscoveryState.Settling
            || e.NewState != DisplayDiscoveryState.Ready
            || _mainWindow is not { IsVisible: true })
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => _ = ReconcileMainWindowPlacementAsync()));
    }

    private async Task ReconcileMainWindowPlacementAsync()
    {
        if (_isExiting || _mainWindow is not { IsVisible: true } window)
        {
            return;
        }

        try
        {
            await window.ReconcileWindowPlacementAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Window placement reconciliation failed after display settling.");
        }
    }

    private void InitializeTray(AppSettings startupSettings)
    {
        _trayHost = new TrayHost(
            _settingsService!,
            _modeCommandService!,
            _displayTransitionService!,
            _logger!,
            ShowMainWindow,
            profileName => RunTrayCommandAsync(() => _modeCommandService!.EnterGameShellWithDisplayProfileAsync(profileName)),
            () => RunTrayCommandAsync(() => _modeCommandService!.EnterWorkModeAsync()),
            OnExitMenuClick);
        _trayHost.Start(startupSettings);
    }

    private void OnExitMenuClick()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _logger?.LogInformation("Exit requested from tray. Thread={ThreadId}", Environment.CurrentManagedThreadId);
        _trayHost?.CloseMenu();
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
            _logger?.LogInformation("Remote control host stop begin. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            try
            {
                if (_remoteControlHost is not null)
                {
                    await _remoteControlHost.StopAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Remote control host stop failed. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            }

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
                _hotkeyHost?.Dispose();
                _hotkeyHost = null;
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

            if (_displayDiscoveryCoordinator is not null)
            {
                _displayDiscoveryCoordinator.StateChanged -= OnDisplayDiscoveryStateChanged;
            }

            _logger?.LogInformation("Tray dispose begin. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            try
            {
                _trayHost?.Dispose();
                _trayHost = null;
            }
            finally
            {
                _logger?.LogInformation("Tray dispose complete. Thread={ThreadId}", Environment.CurrentManagedThreadId);
            }

            _commandHost?.Dispose();
            _commandHost = null;
            _remoteControlHost = null;

            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
            if (_serviceProvider is not null)
            {
                var serviceProvider = _serviceProvider;
                _serviceProvider = null;
                _logger?.LogInformation("[SHUTDOWN] Component=ServiceProvider Action=DisposeAsync Result=Started");
                await serviceProvider.DisposeAsync();
                _logger?.LogInformation("[SHUTDOWN] Component=ServiceProvider Action=DisposeAsync Result=Success");
            }
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

    private async Task RunTrayCommandAsync(Func<Task<GameShell.Core.Runtime.ModeResult>> command)
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
            _ = _trayHost?.RefreshAsync();
        }
    }

    private void InitializeHotkeys()
    {
        _hotkeyHost = new GlobalHotkeyHost(
            _settingsService!,
            _modeCommandService!,
            _profileSettingsResolver!,
            _logger!,
            RunTrayCommandAsync,
            () => _webViewBridge?.NotifyStatusChanged());
        _webViewBridge!.SettingsSaved += OnSettingsSaved;
        _hotkeyHost.Start();
    }

    private async void OnSettingsSaved(object? sender, EventArgs e)
    {
        if (_hotkeyHost is not null)
        {
            await _hotkeyHost.ReloadAsync();
        }
        await (_trayHost?.RefreshAsync() ?? Task.CompletedTask);
    }

    private async void OnHotkeyCaptureStateChanged(object? sender, bool isActive)
    {
        _hotkeyHost?.SetCaptureActive(isActive);
        await Task.CompletedTask;
    }

    private void RegisterCommandEvent(string eventName, Func<Task> action)
    {
        _commandHost?.Register(eventName, action);
    }

    private void RegisterCommandEvent(string eventName, Action action)
    {
        RegisterCommandEvent(eventName, () =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    private async Task RunCommandAsync(Func<IModeCommandService, Task<GameShell.Core.Runtime.ModeResult>> action)
    {
        if (_isExiting || _serviceProvider is null)
        {
            return;
        }

        var modeCommandService = _serviceProvider.GetRequiredService<IModeCommandService>();
        await action(modeCommandService);
    }

    private async Task RunGamingCommandAsync()
    {
        if (_isExiting || _serviceProvider is null)
        {
            return;
        }

        var settings = await _serviceProvider.GetRequiredService<ISettingsService>().LoadAsync();
        var modeCommandService = _serviceProvider.GetRequiredService<IModeCommandService>();

        if (settings.Display.GamingLayout is not null || settings.Display.GamingProfiles.Count > 0)
        {
            await modeCommandService.EnterGameShellWithDisplayProfileAsync(string.Empty);
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
            ConsoleModeCommandEventName => RunCommandAsync(service => service.EnterGameShellAsync()),
            GamingProfileCommandEventName => RunGamingCommandAsync(),
            WorkCommandEventName => RunCommandAsync(service => service.EnterWorkModeAsync()),
            DisplayCapabilityDiagnosticCommandEventName => _displayCapabilityDiagnostic is null
                ? Task.CompletedTask
                : _displayCapabilityDiagnostic.RunAsync("CONSOLE"),
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
        if (args.Any(arg => arg.Equals("--console", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("--gaming", StringComparison.OrdinalIgnoreCase)))
        {
            return ConsoleModeCommandEventName;
        }

        if (args.Any(arg => arg.Equals("--profile", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("--pcgaming", StringComparison.OrdinalIgnoreCase)))
        {
            return GamingProfileCommandEventName;
        }

        if (args.Any(arg => arg.Equals("--work", StringComparison.OrdinalIgnoreCase)))
        {
            return WorkCommandEventName;
        }

        if (args.Any(arg => arg.Equals("--display-capability-diagnostic", StringComparison.OrdinalIgnoreCase)))
        {
            return DisplayCapabilityDiagnosticCommandEventName;
        }

        return null;
    }

    private static bool HasArgument(IEnumerable<string> args, string expected) =>
        args.Any(arg => string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase));


}
