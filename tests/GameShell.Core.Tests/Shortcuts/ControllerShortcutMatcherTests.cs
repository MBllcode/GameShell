using GameShell.Core.Settings;
using GameShell.Core.Shortcuts;
using GameShell.Desktop.Shortcuts;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ControllerShortcutMatcherTests
{
    [Fact]
    public void Match_TriggersAfterConfiguredHoldDuration()
    {
        var settings = new AppSettings();
        settings.Display.GamingProfileConfigurations.Add(new GamingProfileConfiguration
        {
            ProfileName = "Projector",
            ControllerShortcut = new ControllerShortcutSettings
            {
                Buttons = [ControllerButton.South, ControllerButton.Back],
                HoldDurationMs = 500
            }
        });
        var matcher = new ControllerShortcutMatcher();
        var pressed = new HashSet<ControllerButton> { ControllerButton.South, ControllerButton.Back };
        var startedAt = DateTimeOffset.UtcNow;

        Assert.Empty(matcher.Match(settings, "controller-1", pressed, startedAt));
        var matches = matcher.Match(settings, "controller-1", pressed, startedAt.AddMilliseconds(500));

        var match = Assert.Single(matches);
        Assert.Equal("Projector", match.ProfileName);
        Assert.False(match.IsWorkMode);
    }

    [Fact]
    public void Match_RequiresExactButtonsAndCanTriggerAgainAfterRelease()
    {
        var settings = new AppSettings();
        settings.Display.GamingProfileConfigurations.Add(new GamingProfileConfiguration
        {
            ProfileName = "Console",
            ControllerShortcut = new ControllerShortcutSettings
            {
                Buttons = [ControllerButton.Start],
                HoldDurationMs = 300
            }
        });
        var matcher = new ControllerShortcutMatcher();
        var pressed = new HashSet<ControllerButton> { ControllerButton.Start, ControllerButton.South };
        var now = DateTimeOffset.UtcNow;

        Assert.Empty(matcher.Match(settings, "controller-1", pressed, now));
        pressed.Remove(ControllerButton.South);
        Assert.Empty(matcher.Match(settings, "controller-1", pressed, now.AddMilliseconds(300)));
        Assert.Single(matcher.Match(settings, "controller-1", pressed, now.AddMilliseconds(600)));
    }

    [Fact]
    public void Match_ReturnsWorkModeTrigger()
    {
        var settings = new AppSettings
        {
            Controller = new ControllerSettings
            {
                WorkMode = new ControllerShortcutSettings
                {
                    Buttons = [ControllerButton.Back],
                    HoldDurationMs = 300
                }
            }
        };
        var matcher = new ControllerShortcutMatcher();
        var pressed = new HashSet<ControllerButton> { ControllerButton.Back };
        var now = DateTimeOffset.UtcNow;

        matcher.Match(settings, "controller-1", pressed, now);
        var match = Assert.Single(matcher.Match(settings, "controller-1", pressed, now.AddMilliseconds(300)));

        Assert.True(match.IsWorkMode);
        Assert.Null(match.ProfileName);
    }
}
