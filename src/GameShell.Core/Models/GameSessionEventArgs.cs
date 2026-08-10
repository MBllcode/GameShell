namespace GameShell.Core.Models;

public sealed class GameSessionEventArgs : EventArgs
{
    public GameSessionEventArgs(int processId, string processName)
    {
        ProcessId = processId;
        ProcessName = processName;
    }

    public int ProcessId { get; }
    public string ProcessName { get; }
}