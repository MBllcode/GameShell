using GameShell.Core.Runtime;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class TrayMenuTextTests
{
    [Fact]
    public void Labels_UseEnglishWhenLanguageIsEnglish()
    {
        Assert.Equal("Open GameShell", TrayMenuText.OpenGameShell("en"));
        Assert.Equal("Profiles", TrayMenuText.GamingProfiles("en"));
        Assert.Equal("Restore default setup", TrayMenuText.ReturnToWorkMode("en"));
        Assert.Equal("Close GameShell", TrayMenuText.CloseGameShell("en"));
        Assert.False(TrayMenuText.ShowRestorePreviousLayout("en"));
    }

    [Fact]
    public void Labels_UsePolishWhenLanguageIsPolish()
    {
        Assert.Equal("Otwórz GameShell", TrayMenuText.OpenGameShell("pl"));
        Assert.Equal("Profile", TrayMenuText.GamingProfiles("pl"));
        Assert.Equal("Przywróć konfigurację domyślną", TrayMenuText.ReturnToWorkMode("pl"));
        Assert.Equal("Zamknij GameShell", TrayMenuText.CloseGameShell("pl"));
        Assert.False(TrayMenuText.ShowRestorePreviousLayout("pl"));
    }
}
