using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Desktop;

internal sealed class SessionApplicationProcesses(ILogger logger)
{
    private readonly Dictionary<int, (DateTime StartedAtUtc, string ExecutablePath)> _startedProcesses = [];

    internal bool Track(Process process, string executablePath, DateTime launchRequestedAtUtc)
    {
        if (!Path.IsPathFullyQualified(executablePath)
            || process.Id == Environment.ProcessId
            || Path.GetFileNameWithoutExtension(executablePath).Equals("explorer", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileNameWithoutExtension(executablePath).Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var startedAtUtc = process.StartTime.ToUniversalTime();
            if (startedAtUtc < launchRequestedAtUtc)
            {
                return false;
            }

            _startedProcesses[process.Id] = (startedAtUtc, executablePath);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            logger.LogDebug(exception, "Could not verify ownership of session application PID={ProcessId}.", process.Id);
            return false;
        }
    }

    internal void EndSession(bool closeApplications, CancellationToken cancellationToken, Action<Process>? requestClose = null)
    {
        if (!closeApplications)
        {
            _startedProcesses.Clear();
            return;
        }

        // Let the app handle WM_CLOSE, including prompts to save unsaved files.
        requestClose ??= process => process.CloseMainWindow();
        foreach (var (processId, started) in _startedProcesses.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.StartTime.ToUniversalTime() == started.StartedAtUtc
                    && string.Equals(process.MainModule?.FileName, started.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    requestClose(process);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
            {
                logger.LogDebug(exception, "Could not close session application PID={ProcessId}.", processId);
            }

            _startedProcesses.Remove(processId);
        }
    }
}
