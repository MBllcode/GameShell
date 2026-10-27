using System.Runtime.InteropServices;

namespace GameShell.Windows.Desktop;

/// <summary>
/// Owns the user cursor side effects used by mode workflows.
/// </summary>
public sealed class WindowsCursorService : IWindowsCursorService
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const int SpiSetCursors = 0x0057;
    private const int CursorWidth = 32;
    private const int CursorHeight = 32;

    private static readonly int[] SystemCursorIds =
    [
        32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644,
        32645, 32646, 32648, 32649, 32650, 32651, 32671, 32672
    ];

    public void MoveToBottomRight()
    {
        var x = GetSystemMetrics(SmXVirtualScreen) + GetSystemMetrics(SmCxVirtualScreen) - 1;
        var y = GetSystemMetrics(SmYVirtualScreen) + GetSystemMetrics(SmCyVirtualScreen) - 1;
        SetCursorPos(x, y);
    }

    public void Hide()
    {
        var andMask = Enumerable.Repeat((byte)0xFF, CursorWidth * CursorHeight / 8).ToArray();
        var xorMask = new byte[CursorWidth * CursorHeight / 8];

        foreach (var cursorId in SystemCursorIds)
        {
            var cursor = CreateCursor(IntPtr.Zero, 0, 0, CursorWidth, CursorHeight, andMask, xorMask);
            if (cursor == IntPtr.Zero)
            {
                continue;
            }

            if (!SetSystemCursor(cursor, (uint)cursorId))
            {
                DestroyCursor(cursor);
            }
        }
    }

    public void Restore()
    {
        SystemParametersInfo(SpiSetCursors, 0, IntPtr.Zero, 0);
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateCursor(
        IntPtr instance,
        int hotSpotX,
        int hotSpotY,
        int width,
        int height,
        byte[] andPlane,
        byte[] xorPlane);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetSystemCursor(IntPtr cursor, uint cursorId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyCursor(IntPtr cursor);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(int action, int parameter, IntPtr value, int flags);
}
