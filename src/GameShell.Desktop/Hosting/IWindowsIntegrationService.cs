using GameShell.Core.Settings;

namespace GameShell.Desktop.Hosting;

public interface IWindowsIntegrationService
{
    void Apply(AppSettings settings);
}
