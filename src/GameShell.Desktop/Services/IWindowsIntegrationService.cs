using GameShell.Core.Models;

namespace GameShell.Desktop.Services;

public interface IWindowsIntegrationService
{
    void Apply(AppSettings settings);
}
