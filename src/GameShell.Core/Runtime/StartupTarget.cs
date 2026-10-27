namespace GameShell.Core.Runtime;

public enum StartupTargetKind
{
    Work,
    Profile
}

public sealed record StartupTarget(
    StartupTargetKind Kind,
    string? ProfileId = null,
    string Reason = "Configured")
{
    public static StartupTarget Work(string reason = "Configured") => new(StartupTargetKind.Work, null, reason);
    public static StartupTarget Profile(string profileId, string reason = "Configured") => new(StartupTargetKind.Profile, profileId, reason);

    public override string ToString() => Kind == StartupTargetKind.Profile ? ProfileId ?? "Profile" : Kind.ToString();
}
