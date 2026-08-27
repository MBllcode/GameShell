using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Services;

/// <summary>
/// Observes real HID consumer-control input while GameShell is the shell.
/// Native Windows handling gets a short opportunity first; Core Audio is used
/// only when the physical button did not change the default render endpoint.
/// </summary>
public sealed class WindowsVolumeButtonService : IDisposable
{
    private static readonly TimeSpan NativeHandlingGracePeriod = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan DuplicateInputWindow = TimeSpan.FromMilliseconds(75);

    private readonly WindowsCoreAudioVolumeService _audioService;
    private readonly ILogger<WindowsVolumeButtonService> _logger;
    private readonly SemaphoreSlim _actionGate = new(1, 1);
    private readonly object _inputSync = new();
    private ConsumerControlInputWindow? _inputWindow;
    private VolumeButtonAction? _lastAction;
    private long _lastActionTimestamp;
    private bool _nativePathLogged;
    private bool _fallbackPathLogged;
    private bool _disposed;

    public WindowsVolumeButtonService(
        WindowsCoreAudioVolumeService audioService,
        ILogger<WindowsVolumeButtonService> logger)
    {
        _audioService = audioService;
        _logger = logger;
    }

    public bool Start()
    {
        if (_disposed || _inputWindow is not null)
        {
            return _inputWindow is not null;
        }

        try
        {
            _inputWindow = new ConsumerControlInputWindow(OnVolumeAction);
            _logger.LogInformation(
                "[CONSOLE SHELL COMPAT] Component=VolumeButtons InputSource=HidConsumerControl,MultimediaHotKey MultimediaHotKeys={MultimediaHotKeys} Result=Ready",
                _inputWindow.MultimediaHotKeysRegistered);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _inputWindow?.Dispose();
            _inputWindow = null;
            _logger.LogWarning(
                exception,
                "[CONSOLE SHELL COMPAT] Component=VolumeButtons InputSource=HidConsumerControl Result=Unavailable");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _inputWindow?.Dispose();
        _inputWindow = null;
    }

    private void OnVolumeAction(VolumeButtonAction action)
    {
        if (_disposed || IsDuplicate(action))
        {
            return;
        }

        _ = HandleVolumeActionAsync(action);
    }

    private async Task HandleVolumeActionAsync(VolumeButtonAction action)
    {
        if (_disposed)
        {
            return;
        }

        await _actionGate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            var nativeHandled = false;
            if (_audioService.TryGetState(out var before))
            {
                await Task.Delay(NativeHandlingGracePeriod);
                if (_audioService.TryGetState(out var after))
                {
                    nativeHandled = HasNativeStateChange(action, before, after);
                }
            }

            if (nativeHandled)
            {
                if (!_nativePathLogged)
                {
                    _nativePathLogged = true;
                    _logger.LogInformation(
                        "[CONSOLE SHELL COMPAT] Component=VolumeButtons InputSource=HidConsumerControl Dependency=NativeWindows Result=Ready");
                }

                return;
            }

            if (_audioService.TryApply(action) && !_fallbackPathLogged)
            {
                _fallbackPathLogged = true;
                _logger.LogInformation(
                    "[CONSOLE SHELL COMPAT] Component=VolumeButtons InputSource=HidConsumerControl Fallback=CoreAudio Result=Enabled");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogDebug(exception, "Console Shell volume compatibility action failed. Action={Action}", action);
        }
        finally
        {
            _actionGate.Release();
        }
    }

    private bool IsDuplicate(VolumeButtonAction action)
    {
        var now = Stopwatch.GetTimestamp();
        lock (_inputSync)
        {
            var elapsed = _lastActionTimestamp == 0
                ? TimeSpan.MaxValue
                : TimeSpan.FromSeconds((double)(now - _lastActionTimestamp) / Stopwatch.Frequency);
            if (_lastAction == action && elapsed < DuplicateInputWindow)
            {
                return true;
            }

            _lastAction = action;
            _lastActionTimestamp = now;
            return false;
        }
    }

    private static bool HasNativeStateChange(
        VolumeButtonAction action,
        AudioEndpointState before,
        AudioEndpointState after)
    {
        return action switch
        {
            VolumeButtonAction.Mute => before.IsMuted != after.IsMuted,
            VolumeButtonAction.VolumeUp or VolumeButtonAction.VolumeDown
                => Math.Abs(before.Volume - after.Volume) > 0.0001f,
            _ => false
        };
    }
}

internal sealed class ConsumerControlInputWindow : IDisposable
{
    private const int WmInput = 0x00FF;
    private const int WmHotkey = 0x0312;
    private const int WmNccreate = 0x0081;
    private const uint RidInput = 0x10000003;
    private const uint RidiPreparsedData = 0x20000005;
    private const uint RidevRemove = 0x00000001;
    private const uint RidevInputSink = 0x00000100;
    private const uint RawInputTypeHid = 2;
    private const int HidpStatusSuccess = 0x00110000;
    private const ushort ConsumerUsagePage = 0x0C;
    private const ushort VolumeIncrementUsage = 0x00E9;
    private const ushort VolumeDecrementUsage = 0x00EA;
    private const ushort MuteUsage = 0x00E2;
    private const int VolumeUpHotkeyId = 0x5201;
    private const int VolumeDownHotkeyId = 0x5202;
    private const int MuteHotkeyId = 0x5203;
    private const uint VirtualKeyVolumeMute = 0xAD;
    private const uint VirtualKeyVolumeDown = 0xAE;
    private const uint VirtualKeyVolumeUp = 0xAF;
    private static readonly IntPtr HwndMessage = new(-3);
    private static readonly NativeMethods.WndProc WindowProcedure = StaticWindowProcedure;
    private static readonly Dictionary<IntPtr, ConsumerControlInputWindow> Instances = [];
    private readonly Action<VolumeButtonAction> _action;
    private readonly string _className;
    private readonly IntPtr _moduleHandle;
    private readonly NativeMethods.WndProc _windowProcedure;
    private bool _disposed;

    public ConsumerControlInputWindow(Action<VolumeButtonAction> action)
    {
        _action = action;
        _windowProcedure = WindowProcedure;
        _moduleHandle = NativeMethods.GetModuleHandle(null);
        _className = $"GameShell.ConsoleShell.ConsumerControl.{Environment.ProcessId}";

        var windowClass = new NativeMethods.WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_windowProcedure),
            hInstance = _moduleHandle,
            lpszClassName = _className
        };

        if (NativeMethods.RegisterClassEx(ref windowClass) == 0
            && Marshal.GetLastWin32Error() != NativeMethods.ErrorClassAlreadyExists)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the consumer-control window class.");
        }

        Handle = NativeMethods.CreateWindowEx(
            0,
            _className,
            "GameShell.ConsoleShell.ConsumerControl",
            0,
            0,
            0,
            0,
            0,
            HwndMessage,
            IntPtr.Zero,
            _moduleHandle,
            IntPtr.Zero);

        if (Handle == IntPtr.Zero)
        {
            NativeMethods.UnregisterClass(_className, _moduleHandle);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the consumer-control window.");
        }

        lock (Instances)
        {
            Instances[Handle] = this;
        }

        try
        {
            RegisterConsumerControlInput();
            MultimediaHotKeysRegistered = RegisterMultimediaHotKeys();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public IntPtr Handle { get; }

    public bool MultimediaHotKeysRegistered { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Handle != IntPtr.Zero)
        {
            var device = new NativeMethods.RawInputDevice
            {
                UsagePage = ConsumerUsagePage,
                Usage = 0x01,
                Flags = RidevRemove,
                Target = IntPtr.Zero
            };
            NativeMethods.RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf<NativeMethods.RawInputDevice>());
            UnregisterMultimediaHotKey(VolumeUpHotkeyId);
            UnregisterMultimediaHotKey(VolumeDownHotkeyId);
            UnregisterMultimediaHotKey(MuteHotkeyId);
        }

        lock (Instances)
        {
            Instances.Remove(Handle);
        }

        if (Handle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(Handle);
        }

        NativeMethods.UnregisterClass(_className, _moduleHandle);
        GC.KeepAlive(_windowProcedure);
    }

    private void RegisterConsumerControlInput()
    {
        var device = new NativeMethods.RawInputDevice
        {
            UsagePage = ConsumerUsagePage,
            Usage = 0x01,
            Flags = RidevInputSink,
            Target = Handle
        };

        if (!NativeMethods.RegisterRawInputDevices(
                ref device,
                1,
                (uint)Marshal.SizeOf<NativeMethods.RawInputDevice>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register consumer-control input.");
        }
    }

    private bool RegisterMultimediaHotKeys()
    {
        var registered = false;
        registered |= NativeMethods.RegisterHotKey(Handle, VolumeUpHotkeyId, 0, VirtualKeyVolumeUp);
        registered |= NativeMethods.RegisterHotKey(Handle, VolumeDownHotkeyId, 0, VirtualKeyVolumeDown);
        registered |= NativeMethods.RegisterHotKey(Handle, MuteHotkeyId, 0, VirtualKeyVolumeMute);
        return registered;
    }

    private void UnregisterMultimediaHotKey(int id)
    {
        NativeMethods.UnregisterHotKey(Handle, id);
    }

    private void HandleRawInput(IntPtr rawInput)
    {
        var inputSize = 0u;
        var headerSize = (uint)Marshal.SizeOf<NativeMethods.RawInputHeader>();
        if (NativeMethods.GetRawInputData(rawInput, RidInput, IntPtr.Zero, ref inputSize, headerSize) == uint.MaxValue
            || inputSize == 0)
        {
            return;
        }

        var inputBuffer = Marshal.AllocHGlobal(checked((int)inputSize));
        try
        {
            if (NativeMethods.GetRawInputData(rawInput, RidInput, inputBuffer, ref inputSize, headerSize) == uint.MaxValue)
            {
                return;
            }

            var header = Marshal.PtrToStructure<NativeMethods.RawInputHeader>(inputBuffer);
            if (header.Type != RawInputTypeHid)
            {
                return;
            }

            var hidHeaderOffset = Marshal.SizeOf<NativeMethods.RawInputHeader>();
            var reportSize = Marshal.ReadInt32(inputBuffer, hidHeaderOffset);
            var reportCount = Marshal.ReadInt32(inputBuffer, hidHeaderOffset + sizeof(uint));
            if (reportSize <= 0 || reportCount <= 0 || reportCount > 32 || reportSize > 1024)
            {
                return;
            }

            var reportDataOffset = hidHeaderOffset + (sizeof(uint) * 2);
            var totalReportBytes = checked(reportSize * reportCount);
            if (reportDataOffset + totalReportBytes > inputSize)
            {
                return;
            }

            for (var reportIndex = 0; reportIndex < reportCount; reportIndex++)
            {
                var report = new byte[reportSize];
                Marshal.Copy(
                    IntPtr.Add(inputBuffer, reportDataOffset + (reportIndex * reportSize)),
                    report,
                    0,
                    report.Length);

                if (TryGetVolumeAction(header.Device, report, out var action))
                {
                    _action(action);
                    return;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(inputBuffer);
        }
    }

    private static bool TryGetVolumeAction(IntPtr device, byte[] report, out VolumeButtonAction action)
    {
        if (TryGetHidUsages(device, report, out var usages))
        {
            if (usages.Contains(VolumeIncrementUsage))
            {
                action = VolumeButtonAction.VolumeUp;
                return true;
            }

            if (usages.Contains(VolumeDecrementUsage))
            {
                action = VolumeButtonAction.VolumeDown;
                return true;
            }

            if (usages.Contains(MuteUsage))
            {
                action = VolumeButtonAction.Mute;
                return true;
            }

            action = default;
            return false;
        }

        for (var index = 0; index < report.Length; index++)
        {
            if (report[index] is 0xE9)
            {
                action = VolumeButtonAction.VolumeUp;
                return true;
            }

            if (report[index] is 0xEA)
            {
                action = VolumeButtonAction.VolumeDown;
                return true;
            }

            if (report[index] is 0xE2)
            {
                action = VolumeButtonAction.Mute;
                return true;
            }
        }

        action = default;
        return false;
    }

    private static bool TryGetHidUsages(IntPtr device, byte[] report, out HashSet<ushort> usages)
    {
        usages = [];
        if (device == IntPtr.Zero)
        {
            return false;
        }

        var preparsedDataSize = 0u;
        if (NativeMethods.GetRawInputDeviceInfo(device, RidiPreparsedData, IntPtr.Zero, ref preparsedDataSize) == uint.MaxValue
            || preparsedDataSize == 0)
        {
            return false;
        }

        var preparsedData = Marshal.AllocHGlobal(checked((int)preparsedDataSize));
        var reportBuffer = Marshal.AllocHGlobal(report.Length);
        try
        {
            if (NativeMethods.GetRawInputDeviceInfo(device, RidiPreparsedData, preparsedData, ref preparsedDataSize) == uint.MaxValue)
            {
                return false;
            }

            Marshal.Copy(report, 0, reportBuffer, report.Length);
            var usageList = new ushort[32];
            var usageLength = (uint)usageList.Length;
            var status = NativeMethods.HidPGetUsages(
                0,
                ConsumerUsagePage,
                0,
                usageList,
                ref usageLength,
                preparsedData,
                reportBuffer,
                (uint)report.Length);
            if (status != HidpStatusSuccess)
            {
                return false;
            }

            for (var index = 0; index < usageLength && index < usageList.Length; index++)
            {
                usages.Add(usageList[index]);
            }

            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(reportBuffer);
            Marshal.FreeHGlobal(preparsedData);
        }
    }

    private static IntPtr StaticWindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmNccreate)
        {
            return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
        }

        ConsumerControlInputWindow? instance;
        lock (Instances)
        {
            Instances.TryGetValue(hwnd, out instance);
        }

        if (message == WmHotkey && instance is not null && !instance._disposed)
        {
            var action = wParam.ToInt32() switch
            {
                VolumeUpHotkeyId => VolumeButtonAction.VolumeUp,
                VolumeDownHotkeyId => VolumeButtonAction.VolumeDown,
                MuteHotkeyId => VolumeButtonAction.Mute,
                _ => (VolumeButtonAction?)null
            };
            if (action is not null)
            {
                instance._action(action.Value);
                return IntPtr.Zero;
            }
        }

        if (message == WmInput && instance is not null && !instance._disposed)
        {
            instance.HandleRawInput(lParam);
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private static class NativeMethods
    {
        internal const int ErrorClassAlreadyExists = 1410;
        internal delegate IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WndClassEx
        {
            internal uint cbSize;
            internal uint style;
            internal IntPtr lpfnWndProc;
            internal int cbClsExtra;
            internal int cbWndExtra;
            internal IntPtr hInstance;
            internal IntPtr hIcon;
            internal IntPtr hCursor;
            internal IntPtr hbrBackground;
            internal string? lpszMenuName;
            internal string lpszClassName;
            internal IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RawInputDevice
        {
            internal ushort UsagePage;
            internal ushort Usage;
            internal uint Flags;
            internal IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RawInputHeader
        {
            internal uint Type;
            internal uint Size;
            internal IntPtr Device;
            internal IntPtr WParam;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern ushort RegisterClassEx(ref WndClassEx windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateWindowEx(
            int exStyle,
            string className,
            string windowName,
            int style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterClass(string className, IntPtr instance);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterRawInputDevices(
            ref RawInputDevice devices,
            uint numberOfDevices,
            uint deviceSize);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetRawInputData(
            IntPtr rawInput,
            uint command,
            IntPtr data,
            ref uint dataSize,
            uint headerSize);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetRawInputDeviceInfo(
            IntPtr device,
            uint command,
            IntPtr data,
            ref uint dataSize);

        [DllImport("hid.dll")]
        internal static extern int HidPGetUsages(
            int reportType,
            ushort usagePage,
            ushort linkCollection,
            [Out] ushort[] usageList,
            ref uint usageLength,
            IntPtr preparsedData,
            IntPtr report,
            uint reportLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string? moduleName);

        [DllImport("user32.dll")]
        internal static extern IntPtr DefWindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    }
}
