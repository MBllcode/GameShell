using GameShell.Core.Applications;
using GameShell.Core.Displays;
using GameShell.Core.Profiles;
using GameShell.Core.Runtime;
using GameShell.Core.Settings;
using GameShell.Desktop.Bridge;
using GameShell.Desktop.Displays;
using GameShell.Desktop.RemoteControl;
using GameShell.Desktop.Shortcuts;
using GameShell.Desktop.Windowing;
using GameShell.Windows.Applications;
using GameShell.Windows.Desktop;
using GameShell.Desktop.Logging;
using GameShell.Windows.Detection;
using GameShell.Windows.Displays;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.Hosting;

internal static class ServiceRegistration
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new FileLoggerProvider());
        });

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IExternalProcessLauncher, WindowsExternalProcessLauncher>();
        services.AddSingleton<ILauncherProcessService, LauncherProcessService>();
        services.AddSingleton<IWindowsCursorService, WindowsCursorService>();
        services.AddSingleton<IWindowsWindowManagementService, WindowsWindowManagementService>();
        services.AddSingleton<LegacySettingsPropertyMigrator>();
        services.AddSingleton<IModeWorkflowService, NativeModeWorkflowService>();
        services.AddSingleton<IRuntimeStateService, RuntimeStateService>();
        services.AddSingleton<StartupTargetResolver>();
        services.AddSingleton<ConsoleDisplayLayoutBuilder>();
        services.AddSingleton<ProfileSettingsResolver>();
        services.AddSingleton<GamingProfileTopologyReadinessEvaluator>();
        services.AddSingleton<IGamingProfileTopologyReadinessEvaluator>(provider => provider.GetRequiredService<GamingProfileTopologyReadinessEvaluator>());
        services.AddSingleton<WindowsDisplayConfigurationService>();
        services.AddSingleton<WindowsDisplayTopologyReader>();
        services.AddSingleton<IDisplayQueryService>(provider => provider.GetRequiredService<WindowsDisplayConfigurationService>());
        services.AddSingleton<DisplayDiscoveryCoordinator>();
        services.AddSingleton<IDisplayDiscoveryStabilizationService>(provider => provider.GetRequiredService<DisplayDiscoveryCoordinator>());
        services.AddSingleton<ITransientOperationStateService, TransientOperationStateService>();
        services.AddSingleton<IDisplayConfigurationService>(provider => provider.GetRequiredService<WindowsDisplayConfigurationService>());
        services.AddSingleton<IDisplayLayoutApplicationService>(provider => provider.GetRequiredService<WindowsDisplayConfigurationService>());
        services.AddSingleton<IDisplayCapabilityDiagnostic, DisplayCapabilityDiagnostic>();
        services.AddSingleton<DisplayTransitionService>();
        services.AddSingleton<IDisplayTransitionService>(provider => provider.GetRequiredService<DisplayTransitionService>());
        services.AddSingleton<IDisplayTransitionStabilityGuard>(provider => provider.GetRequiredService<DisplayTransitionService>());
        services.AddSingleton<IModeOrchestrator, ModeOrchestrator>();
        services.AddSingleton<IGameSessionMonitor, WindowsGameSessionMonitor>();
        services.AddSingleton<ILauncherRecoveryStrategy, WindowsLauncherRecoveryStrategy>();
        services.AddSingleton<IProfileSessionManager, ProfileSessionManager>();
        services.AddSingleton<IModeCommandService, ModeCommandService>();
        services.AddSingleton<StartupReconciliationService>();
        services.AddSingleton<RemoteControlHost>();
        services.AddSingleton<IDependencyDetectionService, DependencyDetectionService>();
        services.AddSingleton<IWindowsIntegrationService, WindowsIntegrationService>();
        services.AddSingleton<SdlControllerShortcutService>();
        services.AddSingleton<WebViewBridge>();
        services.AddSingleton<IWindowPlacementService, WindowsWindowPlacementService>();
        services.AddTransient<MainWindow>();
    }
}
