namespace GameShell.Core.Applications;

public sealed class DependencyStatus
{
    public string Name { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsDetected { get; set; }
    public string? DetectedPath { get; set; }
    public string? Version { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
}
