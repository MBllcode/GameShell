namespace GameShell.Core.Models;

public sealed class ControllerShortcutSettings
{
    public List<ControllerButton> Buttons { get; set; } = [];
    public int HoldDurationMs { get; set; } = 700;
}

public sealed class ControllerSettings
{
    public bool Enabled { get; set; }
    public ControllerShortcutSettings? ConsoleMode { get; set; }
    public ControllerShortcutSettings? PcGamingMode { get; set; }
    public ControllerShortcutSettings? WorkMode { get; set; }
}
