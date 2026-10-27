
namespace GameShell.Core.Settings;

public interface ISettingsService
{
    string SettingsPath { get; }
    /// <summary>Returns an independent snapshot of the last persisted settings.</summary>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    /// <summary>Publishes a snapshot only after it has been saved successfully.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
