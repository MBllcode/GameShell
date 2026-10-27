namespace GameShell.Core.Shortcuts;

public sealed class ControllerShortcutSettings
{
    public List<ControllerButton> Buttons { get; set; } = [];
    public int HoldDurationMs { get; set; } = 700;
}

public sealed class ControllerSettings
{
    public bool Enabled { get; set; }
    public ControllerShortcutSettings? ConsoleMode { get; set; }
    public ControllerShortcutSettings? GamingMode { get; set; }
    public ControllerShortcutSettings? WorkMode { get; set; }
}
