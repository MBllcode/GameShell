using GameShell.Core.Applications;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Applications;

/// <summary>
/// Starts external applications as the standard interactive user while allowing
/// GameShell itself to keep the elevation required by other Windows features.
/// </summary>
public sealed class WindowsExternalProcessLauncher : IExternalProcessLauncher
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenAssignPrimary = 0x0001;
    private const uint TokenDuplicate = 0x0002;
    private const uint TokenQuery = 0x0008;
    private const uint TokenAdjustDefault = 0x0080;
    private const uint TokenAdjustSessionId = 0x0100;
    private const uint TokenAccess = TokenAssignPrimary | TokenDuplicate | TokenQuery | TokenAdjustDefault | TokenAdjustSessionId;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint LogonWithProfile = 0x00000001;

    private readonly ILogger<WindowsExternalProcessLauncher> _logger;

    public WindowsExternalProcessLauncher(ILogger<WindowsExternalProcessLauncher> logger)
    {
        _logger = logger;
    }

    public ExternalProcessLaunchResult StartStandardUser(ExternalProcessLaunchRequest request)
    {
        var isElevated = IsCurrentProcessElevated();
        try
        {
            var result = isElevated
                ? StartWithInteractiveUserToken(request)
                : StartCurrentContextCore(request);

            LogStart(request, isElevated, result);
            return result;
        }
        catch (Win32Exception exception)
        {
            var result = ExternalProcessLaunchResult.Failed(exception.Message, exception.NativeErrorCode);
            LogStart(request, isElevated, result);
            return result;
        }
        catch (Exception exception)
        {
            var result = ExternalProcessLaunchResult.Failed(exception.Message);
            LogStart(request, isElevated, result);
            return result;
        }
    }

    public ExternalProcessLaunchResult StartCurrentContext(ExternalProcessLaunchRequest request)
    {
        try
        {
            var result = StartCurrentContextCore(request);
            LogStart(request, IsCurrentProcessElevated(), result, "CurrentContext");
            return result;
        }
        catch (Win32Exception exception)
        {
            var result = ExternalProcessLaunchResult.Failed(exception.Message, exception.NativeErrorCode);
            LogStart(request, IsCurrentProcessElevated(), result, "CurrentContext");
            return result;
        }
        catch (Exception exception)
        {
            var result = ExternalProcessLaunchResult.Failed(exception.Message);
            LogStart(request, IsCurrentProcessElevated(), result, "CurrentContext");
            return result;
        }
    }

    private ExternalProcessLaunchResult StartWithInteractiveUserToken(ExternalProcessLaunchRequest request)
    {
        var shellProcessId = FindInteractiveShellProcessId();
        if (shellProcessId is null)
        {
            return ExternalProcessLaunchResult.Failed("Interactive standard-user shell was not found.");
        }

        using var shellProcess = OpenProcess(ProcessQueryLimitedInformation, false, shellProcessId.Value);
        if (shellProcess.IsInvalid)
        {
            return FailureFromLastWin32Error("Could not open the interactive shell process.");
        }

        if (!OpenProcessToken(shellProcess, TokenAccess, out var shellToken))
        {
            return FailureFromLastWin32Error("Could not open the interactive shell token.");
        }

        using (shellToken)
        {
            if (!DuplicateTokenEx(shellToken, TokenAccess, IntPtr.Zero, SecurityImpersonationLevel.Impersonation, TokenType.Primary, out var primaryToken))
            {
                return FailureFromLastWin32Error("Could not duplicate the standard-user token.");
            }

            using (primaryToken)
            {
                if (!CreateEnvironmentBlock(out var environment, primaryToken, false))
                {
                    return FailureFromLastWin32Error("Could not create the standard-user environment.");
                }

                try
                {
                    return CreateProcessWithToken(primaryToken, request, environment);
                }
                finally
                {
                    DestroyEnvironmentBlock(environment);
                }
            }
        }
    }

    private ExternalProcessLaunchResult CreateProcessWithToken(
        SafeTokenHandle token,
        ExternalProcessLaunchRequest request,
        IntPtr environment)
    {
        var startupInfo = new StartupInfo
        {
            Size = Marshal.SizeOf<StartupInfo>()
        };
        var commandLine = BuildCommandLine(request);

        if (CreateProcessAsUser(
            token,
            ResolveApplicationPath(request.ExecutablePath),
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            CreateUnicodeEnvironment,
            environment,
            request.WorkingDirectory,
            ref startupInfo,
            out var processInformation))
        {
            return CompleteProcessStart(processInformation);
        }

        var createProcessAsUserError = Marshal.GetLastWin32Error();
        if (CreateProcessWithTokenW(
            token,
            LogonWithProfile,
            ResolveApplicationPath(request.ExecutablePath),
            commandLine,
            CreateUnicodeEnvironment,
            environment,
            request.WorkingDirectory,
            ref startupInfo,
            out processInformation))
        {
            return CompleteProcessStart(processInformation);
        }

        var fallbackError = Marshal.GetLastWin32Error();
        return ExternalProcessLaunchResult.Failed(
            $"Could not create the standard-user process. CreateProcessAsUser={createProcessAsUserError}, CreateProcessWithTokenW={fallbackError}.",
            fallbackError);
    }

    private static ExternalProcessLaunchResult StartCurrentContextCore(ExternalProcessLaunchRequest request)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            Arguments = request.Arguments,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
            UseShellExecute = request.UseShellExecute
        });

        return process is null
            ? ExternalProcessLaunchResult.Failed("The external process did not return a process handle.")
            : ExternalProcessLaunchResult.Started(process.Id);
    }

    private static ExternalProcessLaunchResult CompleteProcessStart(ProcessInformation processInformation)
    {
        var processId = processInformation.ProcessId;
        CloseHandle(processInformation.ProcessHandle);
        CloseHandle(processInformation.ThreadHandle);
        return ExternalProcessLaunchResult.Started(processId);
    }

    private static StringBuilder BuildCommandLine(ExternalProcessLaunchRequest request)
    {
        var executable = QuoteArgument(request.ExecutablePath);
        var commandLine = string.IsNullOrWhiteSpace(request.Arguments)
            ? executable
            : $"{executable} {request.Arguments}";
        return new StringBuilder(commandLine);
    }

    private static string ResolveApplicationPath(string executablePath)
    {
        if (!executablePath.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
        {
            return executablePath;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private static int? FindInteractiveShellProcessId()
    {
        var shellWindow = GetShellWindow();
        if (shellWindow != IntPtr.Zero)
        {
            GetWindowThreadProcessId(shellWindow, out var processId);
            if (processId != 0)
            {
                return (int)processId;
            }
        }

        var sessionId = Process.GetCurrentProcess().SessionId;
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            try
            {
                if (process.SessionId == sessionId)
                {
                    return process.Id;
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        return null;
    }

    private static bool IsCurrentProcessElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private static ExternalProcessLaunchResult FailureFromLastWin32Error(string reason)
    {
        var errorCode = Marshal.GetLastWin32Error();
        return ExternalProcessLaunchResult.Failed($"{reason} Win32Error={errorCode}.", errorCode);
    }

    private void LogStart(
        ExternalProcessLaunchRequest request,
        bool isElevated,
        ExternalProcessLaunchResult result,
        string requestedContext = "StandardUser")
    {
        _logger.LogInformation(
            "[LAUNCHER PROCESS START] RequestedContext={RequestedContext} GameShellElevated={GameShellElevated} Executable={Executable} Result={Result} Pid={Pid} Win32Error={Win32Error} FailureReason={FailureReason}.",
            requestedContext,
            isElevated,
            request.ExecutablePath,
            result.Success ? "Success" : "Failed",
            result.ProcessId,
            result.Win32ErrorCode,
            result.FailureReason);
    }

    private enum SecurityImpersonationLevel
    {
        Anonymous,
        Identification,
        Impersonation,
        Delegation
    }

    private enum TokenType
    {
        Primary = 1,
        Impersonation = 2
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved2Pointer;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr ProcessHandle;
        public IntPtr ThreadHandle;
        public int ProcessId;
        public int ThreadId;
    }

    private sealed class SafeWin32Handle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeWin32Handle() : base(true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    private sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeTokenHandle() : base(true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeWin32Handle OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(SafeWin32Handle processHandle, uint desiredAccess, out SafeTokenHandle tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        SafeTokenHandle existingToken,
        uint desiredAccess,
        IntPtr tokenAttributes,
        SecurityImpersonationLevel impersonationLevel,
        TokenType tokenType,
        out SafeTokenHandle primaryToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, SafeTokenHandle token, bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessAsUser(
        SafeTokenHandle token,
        string applicationName,
        [In, Out] StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessWithTokenW(
        SafeTokenHandle token,
        uint logonFlags,
        string applicationName,
        [In, Out] StringBuilder commandLine,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
