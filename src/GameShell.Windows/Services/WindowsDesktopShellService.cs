using System.Diagnostics;
using System.Runtime.InteropServices;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Services;

public sealed class WindowsDesktopShellService : IDesktopShellService
{
    private static readonly TimeSpan ExplorerStartupTimeout = TimeSpan.FromSeconds(5);
    private readonly ILogger<WindowsDesktopShellService> _logger;

    public WindowsDesktopShellService(ILogger<WindowsDesktopShellService> logger)
    {
        _logger = logger;
    }

    public bool IsExplorerShellRunning() => GetShellWindow() != IntPtr.Zero;

    public async Task<bool> EnsureDesktopAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (IsExplorerShellRunning())
        {
            return true;
        }

        _logger.LogInformation("[WINDOWS DESKTOP] Action=StartExplorer Result=Started");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = false
            })?.Dispose();

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < ExplorerStartupTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsExplorerShellRunning())
                {
                    _logger.LogInformation(
                        "[WINDOWS DESKTOP] Action=StartExplorer Result=Success DurationMs={DurationMs}",
                        stopwatch.ElapsedMilliseconds);
                    return true;
                }

                await Task.Delay(200, cancellationToken);
            }

            _logger.LogWarning("[WINDOWS DESKTOP] Action=StartExplorer Result=Timeout");
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[WINDOWS DESKTOP] Action=StartExplorer Result=Failed");
            return false;
        }
    }

    public Task PrepareGamingAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.Startup.ConsoleShellModeEnabled)
        {
            return Task.CompletedTask;
        }

        var explorerRunning = IsExplorerShellRunning();
        if (!explorerRunning)
        {
            _logger.LogInformation("[SHELL STARTUP] ExplorerRunning=False Result=Expected");
            return Task.CompletedTask;
        }

        // Explorer does not expose a supported way to terminate only its shell
        // host without risking unrelated File Explorer windows. The existing
        // idempotent taskbar/desktop hide workflow remains the safe fallback.
        _logger.LogInformation(
            "[CONSOLE SHELL] ExplorerRunning=True CloseRequested={CloseRequested} Action=RetainAndHideShellUi Reason=SafeShellOnlyShutdownUnavailable",
            settings.Startup.CloseExplorerWhenEnteringGamingProfileInShellMode);
        return Task.CompletedTask;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();
}
