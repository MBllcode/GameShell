using GameShell.Core.Settings;
using GameShell.Core.Shortcuts;

namespace GameShell.Desktop.Shortcuts;

internal sealed record ControllerShortcutTrigger(
    string? ProfileName,
    bool IsWorkMode,
    ControllerShortcutSettings Shortcut);

internal sealed class ControllerShortcutMatcher
{
    private readonly Dictionary<string, DateTimeOffset> _pressedSince = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _triggered = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ControllerShortcutTrigger> Match(
        AppSettings settings,
        string controllerId,
        IReadOnlySet<ControllerButton> pressed,
        DateTimeOffset now)
    {
        var triggers = new List<ControllerShortcutTrigger>();
        foreach (var configuration in settings.Display.GamingProfileConfigurations)
        {
            if (string.IsNullOrWhiteSpace(configuration.ProfileName)
                || configuration.ControllerShortcut is not { Buttons.Count: > 0 } shortcut)
            {
                continue;
            }

            var key = $"{controllerId}:{configuration.ProfileName}";
            if (pressed.SetEquals(shortcut.Buttons))
            {
                if (ShouldTrigger(key, shortcut, now))
                {
                    triggers.Add(new ControllerShortcutTrigger(configuration.ProfileName, false, shortcut));
                }
            }
            else
            {
                Reset(key);
            }
        }

        var workModeShortcut = settings.Controller.WorkMode is { Buttons.Count: > 0 }
            ? settings.Controller.WorkMode
            : null;
        if (workModeShortcut is null)
        {
            return triggers;
        }

        const string workModeKey = "WorkMode";
        if (pressed.SetEquals(workModeShortcut.Buttons))
        {
            if (ShouldTrigger(workModeKey, workModeShortcut, now))
            {
                triggers.Add(new ControllerShortcutTrigger(null, true, workModeShortcut));
            }
        }
        else
        {
            Reset(workModeKey);
        }

        return triggers;
    }

    public void Reset()
    {
        _pressedSince.Clear();
        _triggered.Clear();
    }

    private bool ShouldTrigger(string key, ControllerShortcutSettings shortcut, DateTimeOffset now)
    {
        if (!_pressedSince.TryGetValue(key, out var startedAt))
        {
            _pressedSince[key] = now;
            return false;
        }

        if (_triggered.Contains(key))
        {
            return false;
        }

        if (now - startedAt < TimeSpan.FromMilliseconds(Math.Clamp(shortcut.HoldDurationMs, 300, 3000)))
        {
            return false;
        }

        _triggered.Add(key);
        return true;
    }

    private void Reset(string key)
    {
        _pressedSince.Remove(key);
        _triggered.Remove(key);
    }
}
