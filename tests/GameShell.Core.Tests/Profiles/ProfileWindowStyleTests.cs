using GameShell.Windows.Desktop;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ProfileWindowStyleTests
{
    [Theory]
    [InlineData(unchecked((int)0x90000000), true)] // Visible popup used by borderless games
    [InlineData(unchecked((int)0x91000000), true)] // Previously maximized borderless game
    [InlineData(0x10CF0000, false)] // Ordinary resizable application
    public void FullscreenSizingRecognizesBorderlessWindows(int style, bool expected)
    {
        Assert.Equal(expected, WindowsWindowManagementService.IsBorderlessApplicationWindow(style));
    }
}
