namespace GameShell.Desktop.Remote;

public sealed record RemoteStatusResponse(
    string? Mode,
    string? SessionState,
    string? ActiveProfileId,
    string? ActiveProfileName,
    string? ActiveLauncher);

public sealed record RemoteProfileResponse(string Id, string Name);

public sealed record RemoteCommandResponse(
    bool Accepted,
    string Command,
    string? ProfileId = null,
    string? Error = null);

public sealed record RemoteCommandDispatchResult(int StatusCode, RemoteCommandResponse Body);
