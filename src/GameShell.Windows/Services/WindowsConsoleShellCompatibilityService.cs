using System.Diagnostics;
using System.Runtime.InteropServices;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameShell.Windows.Services;

/// <summary>
/// Keeps the small set of integrations that can otherwise depend on Explorer
/// available while GameShell is running as the Windows shell.
/// </summary>
public sealed class WindowsConsoleShellCompatibilityService : IConsoleShellCompatibilityService, IAsyncDisposable
{
    private readonly ILogger<WindowsConsoleShellCompatibilityService> _logger;
    private readonly WindowsGameBarProtocolService _gameBarService;
    private readonly WindowsVolumeButtonService _volumeButtonService;
    private bool _initialized;

    public WindowsConsoleShellCompatibilityService(
        ILogger<WindowsConsoleShellCompatibilityService> logger,
        ILogger<WindowsGameBarProtocolService> gameBarLogger,
        WindowsCoreAudioVolumeService audioService,
        ILogger<WindowsVolumeButtonService> volumeButtonLogger)
    {
        _logger = logger;
        _gameBarService = new WindowsGameBarProtocolService(gameBarLogger);
        _volumeButtonService = new WindowsVolumeButtonService(audioService, volumeButtonLogger);
    }

    public Task InitializeAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!settings.Startup.ConsoleShellModeEnabled || _initialized)
        {
            return Task.CompletedTask;
        }

        _initialized = true;
        var explorerRunning = WindowsShellProcessSnapshot.IsExplorerShellRunning();
        var volumeReady = _volumeButtonService.Start();
        var gameBarProtocol = _gameBarService.IsAvailable;
        var runningProcesses = WindowsShellProcessSnapshot.FindRelevantProcesses();
        var installedAsusServices = WindowsShellProcessSnapshot.FindAsusServiceStates();

        _logger.LogInformation(
            "[CONSOLE SHELL COMPAT] Mode=Minimal ExplorerRunning={ExplorerRunning} GameBarProtocol={GameBarProtocol} VolumeButtons={VolumeButtons} Result={Result}",
            explorerRunning,
            gameBarProtocol ? "Available" : "Unavailable",
            volumeReady ? "Ready" : "Unavailable",
            volumeReady || gameBarProtocol ? "Ready" : "Partial");
        _logger.LogDebug(
            "[CONSOLE SHELL COMPAT] RelevantProcesses={RelevantProcesses} InstalledAsusServices={InstalledAsusServices}",
            string.Join(',', runningProcesses),
            string.Join(',', installedAsusServices));
        foreach (var service in installedAsusServices.Where(service => service.EndsWith(":Stopped", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning(
                "[CONSOLE SHELL COMPAT] Component=AsusService Service={Service} Result=Stopped",
                service[..service.LastIndexOf(':')]);
        }
        return Task.CompletedTask;
    }

    public Task<bool> OpenGameBarAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_gameBarService.TryActivate());
    }

    public ValueTask DisposeAsync()
    {
        _volumeButtonService.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed class WindowsGameBarProtocolService
{
    private const string ProtocolScheme = "ms-gamebar";
    private readonly ILogger<WindowsGameBarProtocolService> _logger;

    public WindowsGameBarProtocolService(ILogger<WindowsGameBarProtocolService> logger)
    {
        _logger = logger;
        IsAvailable = CheckAvailability();
    }

    public bool IsAvailable { get; }

    public bool TryActivate()
    {
        var explorerRunning = WindowsShellProcessSnapshot.IsExplorerShellRunning();
        try
        {
            if (!IsAvailable)
            {
                _logger.LogWarning(
                    "[CONSOLE SHELL COMPAT] Component=GameBar ExplorerRunning={ExplorerRunning} ProtocolActivation=Unavailable Result=Failed",
                    explorerRunning);
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = $"{ProtocolScheme}:",
                UseShellExecute = true
            })?.Dispose();
            _logger.LogInformation(
                "[CONSOLE SHELL COMPAT] Component=GameBar ExplorerRunning={ExplorerRunning} ProtocolActivation=Success Result=Ready",
                explorerRunning);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "[CONSOLE SHELL COMPAT] Component=GameBar ExplorerRunning={ExplorerRunning} ProtocolActivation=Failure Result=Unavailable",
                explorerRunning);
            return false;
        }
    }

    private static bool CheckAvailability()
    {
        try
        {
            using var protocolKey = Registry.ClassesRoot.OpenSubKey(ProtocolScheme);
            return protocolKey is not null;
        }
        catch
        {
            return false;
        }
    }
}

internal static class WindowsShellProcessSnapshot
{
    private static readonly string[] RelevantProcessNames =
    [
        "explorer",
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "TextInputHost",
        "RuntimeBroker",
        "SearchHost",
        "ApplicationFrameHost",
        "GameBar",
        "GameBarFTServer"
    ];

    public static bool IsExplorerShellRunning()
    {
        return GetShellWindow() != IntPtr.Zero;
    }

    public static IReadOnlyList<string> FindRelevantProcesses()
    {
        var relevant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            // Process enumeration is diagnostic-only. A restricted or
            // partially initialized shell must not prevent GameShell startup.
            return [];
        }

        foreach (var process in processes)
        {
            try
            {
                if (RelevantProcessNames.Any(name => string.Equals(name, process.ProcessName, StringComparison.OrdinalIgnoreCase))
                    || process.ProcessName.Contains("asus", StringComparison.OrdinalIgnoreCase)
                    || process.ProcessName.Contains("armoury", StringComparison.OrdinalIgnoreCase))
                {
                    relevant.Add(process.ProcessName);
                }
            }
            catch
            {
                // A process can exit or deny access between enumeration and inspection.
            }
            finally
            {
                process.Dispose();
            }
        }

        return relevant.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<string> FindAsusServiceStates()
    {
        try
        {
            using var servicesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            var serviceNames = servicesKey?.GetSubKeyNames()
                .Where(IsAsusServiceName)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
            if (serviceNames.Length == 0)
            {
                return [];
            }

            var manager = NativeMethods.OpenScManager(null, null, NativeMethods.ScManagerConnect);
            if (manager == IntPtr.Zero)
            {
                return serviceNames.Select(name => $"{name}:Unknown").ToArray();
            }

            try
            {
                return serviceNames.Select(name => GetServiceState(manager, name)).ToArray();
            }
            finally
            {
                NativeMethods.CloseServiceHandle(manager);
            }
        }
        catch
        {
            return [];
        }
    }

    private static string GetServiceState(IntPtr manager, string serviceName)
    {
        var service = NativeMethods.OpenService(manager, serviceName, NativeMethods.ServiceQueryStatus);
        if (service == IntPtr.Zero)
        {
            return $"{serviceName}:Unknown";
        }

        try
        {
            var status = new NativeMethods.ServiceStatusProcess();
            var statusSize = (uint)Marshal.SizeOf<NativeMethods.ServiceStatusProcess>();
            var statusBuffer = Marshal.AllocHGlobal((int)statusSize);
            try
            {
                if (!NativeMethods.QueryServiceStatusEx(
                        service,
                        NativeMethods.ScStatusProcessInfo,
                        statusBuffer,
                        statusSize,
                        out _))
                {
                    return $"{serviceName}:Unknown";
                }

                status = Marshal.PtrToStructure<NativeMethods.ServiceStatusProcess>(statusBuffer);
                return $"{serviceName}:{status.CurrentState switch
                {
                    1 => "Stopped",
                    2 => "StartPending",
                    3 => "StopPending",
                    4 => "Running",
                    5 => "ContinuePending",
                    6 => "PausePending",
                    7 => "Paused",
                    _ => "Unknown"
                }}";
            }
            finally
            {
                Marshal.FreeHGlobal(statusBuffer);
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(service);
        }
    }

    private static bool IsAsusServiceName(string name)
    {
        return name.Contains("asus", StringComparison.OrdinalIgnoreCase)
            || name.Contains("armoury", StringComparison.OrdinalIgnoreCase)
            || name.Contains("optimization", StringComparison.OrdinalIgnoreCase);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    private static class NativeMethods
    {
        internal const uint ScManagerConnect = 0x0001;
        internal const uint ServiceQueryStatus = 0x0004;
        internal const int ScStatusProcessInfo = 0;

        [StructLayout(LayoutKind.Sequential)]
        internal struct ServiceStatusProcess
        {
            internal uint ServiceType;
            internal uint CurrentState;
            internal uint ControlsAccepted;
            internal uint Win32ExitCode;
            internal uint ServiceSpecificExitCode;
            internal uint CheckPoint;
            internal uint WaitHint;
            internal uint ProcessId;
            internal uint ServiceFlags;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr OpenScManager(
            string? machineName,
            string? databaseName,
            uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr OpenService(
            IntPtr manager,
            string serviceName,
            uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryServiceStatusEx(
            IntPtr service,
            int infoLevel,
            IntPtr buffer,
            uint bufferSize,
            out uint bytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseServiceHandle(IntPtr handle);
    }
}
