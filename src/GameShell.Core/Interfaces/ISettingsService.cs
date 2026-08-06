using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

public interface ISettingsService
{
    string SettingsPath { get; }
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
