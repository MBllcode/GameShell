using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GameShell.Desktop.Shortcuts;

internal sealed class GlobalHotkeyMessageWindow : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WmNccreate = 0x0081;
    private const int GwlpUserdata = -21;
    private static readonly IntPtr HwndMessage = new(-3);
    private static readonly NativeMethods.WndProc WindowProcedure = StaticWindowProcedure;
    private static readonly Dictionary<IntPtr, GlobalHotkeyMessageWindow> Instances = [];
    private readonly string _className;
    private readonly IntPtr _moduleHandle;
    private readonly NativeMethods.WndProc _windowProcedure;
    private bool _disposed;

    public GlobalHotkeyMessageWindow(Action<int> hotkeyPressed)
    {
        _windowProcedure = WindowProcedure;
        HotkeyPressed = hotkeyPressed;
        _moduleHandle = NativeMethods.GetModuleHandle(null);
        _className = $"GameShell.GlobalHotkeys.{Environment.ProcessId}";

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
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the global hotkey message window class.");
        }

        Handle = NativeMethods.CreateWindowEx(
            0,
            _className,
            "GameShell.GlobalHotkeys",
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
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the global hotkey message window.");
        }

        lock (Instances)
        {
            Instances[Handle] = this;
        }
    }

    public IntPtr Handle { get; }

    public event Action<int>? HotkeyPressed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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

    private static IntPtr StaticWindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmNccreate)
        {
            return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
        }

        GlobalHotkeyMessageWindow? instance;
        lock (Instances)
        {
            Instances.TryGetValue(hwnd, out instance);
        }

        if (message == WmHotkey && instance is not null)
        {
            instance.HotkeyPressed?.Invoke(wParam.ToInt32());
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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandle(string? moduleName);

        [DllImport("user32.dll")]
        internal static extern IntPtr DefWindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    }
}
