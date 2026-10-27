namespace GameShell.Core.Runtime;

public enum GameShellMode
{
    // Reserved for reading older settings/state files. Activation never emits this value.
    Console,
    GamingProfile,
    Work
}
