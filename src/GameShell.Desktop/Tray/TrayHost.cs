using GameShell.Core.Displays;
using GameShell.Core.Runtime;
using GameShell.Core.Settings;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.Tray;

internal sealed class TrayHost : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IModeCommandService _modeCommandService;
    private readonly IDisplayTransitionService _displayTransitionService;
    private readonly ILogger _logger;
    private readonly Action _showMainWindow;
    private readonly Func<string, Task> _enterProfileAsync;
    private readonly Func<Task> _enterWorkModeAsync;
    private readonly Action _requestExit;

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _menu;
    private AppSettings _settings = new();
    private AppStatus _status = new();
    private RuntimeState _runtimeState = new();
    private bool _disposed;

    public TrayHost(
        ISettingsService settingsService,
        IModeCommandService modeCommandService,
        IDisplayTransitionService displayTransitionService,
        ILogger logger,
        Action showMainWindow,
        Func<string, Task> enterProfileAsync,
        Func<Task> enterWorkModeAsync,
        Action requestExit)
    {
        _settingsService = settingsService;
        _modeCommandService = modeCommandService;
        _displayTransitionService = displayTransitionService;
        _logger = logger;
        _showMainWindow = showMainWindow;
        _enterProfileAsync = enterProfileAsync;
        _enterWorkModeAsync = enterWorkModeAsync;
        _requestExit = requestExit;
    }

    public void Start(AppSettings startupSettings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _settings = startupSettings;
        _menu = new ContextMenuStrip();
        _menu.Opening += (_, _) => RebuildMenu();
        RebuildMenu();

        _notifyIcon = new NotifyIcon
        {
            Text = "GameShell",
            Icon = LoadTrayIcon(),
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => _showMainWindow();
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var settingsTask = _settingsService.LoadAsync();
            var statusTask = _modeCommandService.GetStatusAsync();
            var runtimeStateTask = _displayTransitionService.LoadRuntimeStateAsync();
            await Task.WhenAll(settingsTask, statusTask, runtimeStateTask);
            _settings = settingsTask.Result;
            _status = statusTask.Result;
            _runtimeState = runtimeStateTask.Result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh tray menu state.");
        }
    }

    public void ShowWarning(string message)
    {
        _notifyIcon?.ShowBalloonTip(4000, "GameShell", message, ToolTipIcon.Warning);
    }

    public void CloseMenu()
    {
        _menu?.Close();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip = null;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _menu?.Dispose();
        _menu = null;
    }

    private void RebuildMenu()
    {
        if (_menu is null || _disposed)
        {
            return;
        }

        var language = _settings.Ui.Language ?? "en";
        _menu.Items.Clear();
        var openItem = new ToolStripMenuItem(TrayMenuText.OpenGameShell(language))
        {
            Enabled = true,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        openItem.Click += (_, _) =>
        {
            _logger.LogInformation("Tray click: Open GameShell");
            _showMainWindow();
        };
        _menu.Items.Add(openItem);
        _menu.Items.Add(new ToolStripSeparator());

        AddCurrentStatus(language);
        var profilesItem = CreateProfilesMenu(language);
        _menu.Items.Add(profilesItem);
        _menu.Items.Add(new ToolStripSeparator());

        var workModeItem = new ToolStripMenuItem(
            TrayMenuText.ReturnToWorkMode(language)) { Enabled = true };
        workModeItem.Click += async (_, _) =>
        {
            _logger.LogInformation("Tray click: Work Mode");
            await _enterWorkModeAsync();
        };
        _menu.Items.Add(workModeItem);
        _menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem(TrayMenuText.CloseGameShell(language)) { Enabled = true };
        exitItem.Click += (_, _) => _requestExit();
        _menu.Items.Add(exitItem);
    }

    private void AddCurrentStatus(string language)
    {
        var currentMode = _status.CurrentMode ?? _runtimeState.CurrentMode?.ToString();
        var isWorkMode = string.Equals(currentMode, GameShellMode.Work.ToString(), StringComparison.OrdinalIgnoreCase);
        var activeProfileName = _settings.Display.ActiveGamingProfileName;
        var currentLabel = TrayMenuText.CurrentStatus(language, isWorkMode, activeProfileName);
        _menu!.Items.Add(new ToolStripMenuItem(currentLabel) { Enabled = false });
    }

    private ToolStripMenuItem CreateProfilesMenu(string language)
    {
        var currentMode = _status.CurrentMode ?? _runtimeState.CurrentMode?.ToString();
        var isProfileMode = string.Equals(currentMode, GameShellMode.GamingProfile.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentMode, GameShellMode.Console.ToString(), StringComparison.OrdinalIgnoreCase);
        var profilesItem = new ToolStripMenuItem(TrayMenuText.GamingProfiles(language)) { Enabled = true };
        if (_settings.Display.GamingProfiles.Count == 0)
        {
            profilesItem.DropDownItems.Add(new ToolStripMenuItem(TrayMenuText.NoGamingProfiles(language)) { Enabled = false });
            return profilesItem;
        }

        foreach (var profile in _settings.Display.GamingProfiles)
        {
            var profileName = profile.Name;
            var profileItem = new ToolStripMenuItem(profileName)
            {
                Enabled = true,
                Checked = isProfileMode && string.Equals(_settings.Display.ActiveGamingProfileName, profileName, StringComparison.OrdinalIgnoreCase),
                CheckOnClick = false
            };
            _logger.LogInformation("Tray profile item: {ProfileName} enabled={Enabled}", profileName, profileItem.Enabled);
            profileItem.Click += async (_, _) =>
            {
                _logger.LogInformation("Tray click: Profile={ProfileName}", profileName);
                await _enterProfileAsync(profileName);
            };
            profilesItem.DropDownItems.Add(profileItem);
        }

        return profilesItem;
    }

    private static Icon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "GameShell.ico");
        return File.Exists(iconPath)
            ? new Icon(iconPath, SystemInformation.SmallIconSize.Width, SystemInformation.SmallIconSize.Height)
            : Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application;
    }
}
