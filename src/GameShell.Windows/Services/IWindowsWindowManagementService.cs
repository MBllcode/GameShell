using GameShell.Core.Models;

namespace GameShell.Windows.Services;

public interface IWindowsWindowManagementService
{
    bool SetDesktopIconsVisible(bool visible);

    bool SetTaskbarsVisible(bool visible);

    void CloseVisibleWindows(GameShellSettings behaviour);

    void MinimizeRemainingWindows(GameShellSettings behaviour);

    void MinimizeCurrentWindow();

    void CloseExplorerWindows(GameShellSettings behaviour);

    void MinimizeExplorerWindows();

    void RestoreAndFocus(IntPtr windowHandle);

    bool MoveWindowToPrimaryDisplay(IntPtr windowHandle);
}
