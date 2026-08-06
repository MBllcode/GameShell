using System.Windows;
using GameShell.Core.Interfaces;
using GameShell.Core.Services;
using GameShell.Desktop.Logging;
using GameShell.Windows.Detection;
using GameShell.Windows.Displays;
using GameShell.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop;

public partial class App : System.Windows.Application
{
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            SignalExistingInstance(e.Args);
            Shutdown();
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();
        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("GameShell starting. Args: {Args}", string.Join(' ', e.Args));

        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        _ = settingsService.LoadAsync();
        RegisterCommandEvents();

        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        _mainWindow.Show();
        RunStartupCommand(e.Args);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        foreach (var registration in _commandRegistrations)
        {
            registration.Unregister(null);
        }

        foreach (var commandEvent in _commandEvents)
        {
            commandEvent.Dispose();
        }

        _singleInstanceMutex?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private void RegisterCommandEvents()
    {
        RegisterCommandEvent(ShowWindowEventName, () => _mainWindow?.ShowFromTray());
        RegisterCommandEvent(GamingCommandEventName, async () => await RunCommandAsync(service => service.EnterGameShellAsync()));
        RegisterCommandEvent(PcGamingCommandEventName, RunPcGamingCommandAsync);
        RegisterCommandEvent(WorkCommandEventName, async () => await RunCommandAsync(service => service.EnterWorkModeAsync()));
    }

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
        if (_serviceProvider is null)
        {
            return;
        }

        var modeCommandService = _serviceProvider.GetRequiredService<IModeCommandService>();
        await action(modeCommandService);
    }

    private async Task RunPcGamingCommandAsync()
    {
        if (_serviceProvider is null)
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
        services.AddSingleton<WindowsDisplayConfigurationService>();
        services.AddSingleton<IDisplayQueryService>(provider => provider.GetRequiredService<WindowsDisplayConfigurationService>());
        services.AddSingleton<IDisplayConfigurationService>(provider => provider.GetRequiredService<WindowsDisplayConfigurationService>());
        services.AddSingleton<IDisplayLayoutApplicationService>(provider => provider.GetRequiredService<WindowsDisplayConfigurationService>());
        services.AddSingleton<IDisplayTransitionService, DisplayTransitionService>();
        services.AddSingleton<IModeOrchestrator, ModeOrchestrator>();
        services.AddSingleton<IModeCommandService, ModeCommandService>();
        services.AddSingleton<IDependencyDetectionService, DependencyDetectionService>();
        services.AddSingleton<WebView.WebViewBridge>();
        services.AddTransient<MainWindow>();
    }
}
