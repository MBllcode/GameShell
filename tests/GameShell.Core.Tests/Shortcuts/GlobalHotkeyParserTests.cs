using GameShell.Desktop.Shortcuts;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class GlobalHotkeyParserTests
{
    [Fact]
    public void TryParse_ConvertsModifiersAndLetterKey()
    {
        var result = GlobalHotkeyParser.TryParse("Ctrl+Alt+A", out var modifiers, out var virtualKey);

        Assert.True(result);
        Assert.Equal(0x0003u, modifiers);
        Assert.NotEqual(0u, virtualKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("Ctrl+UnknownKey")]
    [InlineData("UnknownModifier+A")]
    public void TryParse_RejectsInvalidInput(string? value)
    {
        Assert.False(GlobalHotkeyParser.TryParse(value, out _, out _));
    }
}
