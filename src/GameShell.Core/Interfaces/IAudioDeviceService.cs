using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

public interface IAudioDeviceService
{
    Task<IReadOnlyList<AudioDeviceSnapshot>> GetRenderDevicesAsync(CancellationToken cancellationToken = default);

    Task<AudioDeviceSnapshot?> GetDefaultRenderDeviceAsync(CancellationToken cancellationToken = default);

    Task<AudioOperationResult> SetBestRenderDeviceForDisplayAsync(
        DisplayIdentity display,
        CancellationToken cancellationToken = default);

    Task<AudioOperationResult> SetDefaultRenderDeviceAsync(
        string deviceId,
        string? deviceName = null,
        CancellationToken cancellationToken = default);
}

public sealed record AudioDeviceSnapshot
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed record AudioOperationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static AudioOperationResult Ok(string? message = null)
    {
        return new AudioOperationResult
        {
            Success = true,
            Message = message
        };
    }

    public static AudioOperationResult Fail(string message)
    {
        return new AudioOperationResult
        {
            Success = false,
            Message = message
        };
    }
}
