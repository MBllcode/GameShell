using GameShell.Core.Services;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class TrayMenuTextTests
{
    [Fact]
    public void Labels_UseEnglishWhenLanguageIsEnglish()
    {
        Assert.Equal("Open GameShell", TrayMenuText.OpenGameShell("en"));
        Assert.Equal("Gaming profiles", TrayMenuText.GamingProfiles("en"));
        Assert.Equal("Return to Work Mode", TrayMenuText.ReturnToWorkMode("en"));
        Assert.Equal("Close GameShell", TrayMenuText.CloseGameShell("en"));
        Assert.False(TrayMenuText.ShowRestorePreviousLayout("en"));
    }

    [Fact]
    public void Labels_UsePolishWhenLanguageIsPolish()
    {
        Assert.Equal("Otwórz GameShell", TrayMenuText.OpenGameShell("pl"));
        Assert.Equal("Profile gamingowe", TrayMenuText.GamingProfiles("pl"));
        Assert.Equal("Wróć do Work Mode", TrayMenuText.ReturnToWorkMode("pl"));
        Assert.Equal("Zamknij GameShell", TrayMenuText.CloseGameShell("pl"));
        Assert.False(TrayMenuText.ShowRestorePreviousLayout("pl"));
    }
}
