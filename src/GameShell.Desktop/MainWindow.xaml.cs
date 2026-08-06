using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Desktop.WebView;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace GameShell.Desktop;

public partial class MainWindow : Window
{
    private const int HotkeyConsoleModeId = 0x4701;
    private const int HotkeyPcGamingModeId = 0x4702;
    private const int HotkeyWorkModeId = 0x4703;
    private const int HotkeyProfileEnterBaseId = 0x4800;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const int WmHotkey = 0x0312;

    private readonly ILogger<MainWindow> _logger;
    private readonly WebViewBridge _webViewBridge;
    private readonly ISettingsService _settingsService;
    private readonly IModeCommandService _modeCommandService;
    private readonly IDisplayTransitionService _displayTransitionService;
    private readonly Dictionary<int, string> _profileEnterHotkeys = new();
    private NotifyIcon? _notifyIcon;
    private HwndSource? _source;
    private bool _isExitRequested;
    private bool _isHotkeyCaptureActive;

    public MainWindow(
        ILogger<MainWindow> logger,
        WebViewBridge webViewBridge,
        ISettingsService settingsService,
        IModeCommandService modeCommandService,
        IDisplayTransitionService displayTransitionService)
    {
        _logger = logger;
        _webViewBridge = webViewBridge;
        _settingsService = settingsService;
        _modeCommandService = modeCommandService;
        _displayTransitionService = displayTransitionService;
        InitializeComponent();
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += OnClosed;
        InitializeTray();
        _webViewBridge.SettingsSaved += OnSettingsSaved;
        _webViewBridge.HotkeyCaptureStateChanged += OnHotkeyCaptureStateChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ApplySavedWindowBoundsAsync();

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameShell",
                "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await WebView.EnsureCoreWebView2Async(webViewEnvironment);
            _webViewBridge.Attach(WebView.CoreWebView2);
            var webRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var indexPath = Path.Combine(webRootPath, "index.html");

            if (File.Exists(indexPath))
            {
                WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.gameshell.local",
                    webRootPath,
                    CoreWebView2HostResourceAccessKind.Allow);

                WebView.Source = new Uri("https://app.gameshell.local/index.html");
            }
            else
            {
                WebView.NavigateToString("""
                <!doctype html>
                <html lang="en">
                  <head>
                    <meta charset="utf-8">
                    <title>GameShell</title>
                    <style>
                      body {
                        margin: 0;
                        min-height: 100vh;
                        display: grid;
                        place-items: center;
                        background: #0e1116;
                        color: #f4f7fb;
                        font-family: "Segoe UI", sans-serif;
                      }
                    </style>
                  </head>
                  <body>
                    <main>
                      <h1>GameShell</h1>
                      <p>React UI has not been built yet. Run build.ps1.</p>
                    </main>
                  </body>
                </html>
                """);
                        }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebView2");
            MessageBox.Show("GameShell could not initialize WebView2.", "GameShell", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        await RegisterConfiguredHotkeysAsync();
        await ApplyStartupWindowStateAsync();
    }

    private async void OnSourceInitialized(object? sender, EventArgs e)
    {
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(OnWindowMessage);
        await RegisterConfiguredHotkeysAsync();
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        EnsureWindowVisible();
        Activate();
    }

    public async void ExitFromTray()
    {
        _isExitRequested = true;
        await SaveWindowBoundsAsync();
        _notifyIcon?.Dispose();
        Application.Current.Shutdown();
    }

    private void InitializeTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open GameShell", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Enter Console Mode", null, async (_, _) => await RunTrayCommandAsync(() => _modeCommandService.EnterGameShellAsync()));
        menu.Items.Add("Enter PC Gaming", null, async (_, _) => await RunTrayCommandAsync(() => _modeCommandService.EnterGameShellWithDisplayProfileAsync("PcGaming")));
        menu.Items.Add("Enter Work Mode", null, async (_, _) => await RunTrayCommandAsync(() => _modeCommandService.EnterWorkModeAsync()));
        menu.Items.Add("Przywróć poprzedni układ ekranów", null, async (_, _) => await RestorePreviousDisplayLayoutFromTrayAsync());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitFromTray));

        _notifyIcon = new NotifyIcon
        {
            Text = "GameShell",
            Icon = LoadTrayIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "GameShell.ico");
        if (File.Exists(iconPath))
        {
            return new System.Drawing.Icon(
                iconPath,
                SystemInformation.SmallIconSize.Width,
                SystemInformation.SmallIconSize.Height);
        }

        return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
            ?? System.Drawing.SystemIcons.Application;
    }

    private async Task ApplyStartupWindowStateAsync()
    {
        var settings = await _settingsService.LoadAsync();
        if (settings.Startup.StartMinimizedToTray)
        {
            Hide();
        }
    }

    private async Task ApplySavedWindowBoundsAsync()
    {
        var settings = await _settingsService.LoadAsync();
        if (settings.Ui.WindowWidth is null || settings.Ui.WindowHeight is null)
        {
            FitWindowToPrimaryWorkArea();
            CenterOnPrimaryScreen();
            return;
        }

        Width = Math.Max(MinWidth, settings.Ui.WindowWidth.Value);
        Height = Math.Max(MinHeight, settings.Ui.WindowHeight.Value);
        FitWindowToPrimaryWorkArea();

        if (settings.Ui.WindowLeft is null || settings.Ui.WindowTop is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        Left = settings.Ui.WindowLeft.Value;
        Top = settings.Ui.WindowTop.Value;
        EnsureWindowVisible();
    }

    private void EnsureWindowVisible()
    {
        FitWindowToPrimaryWorkArea();

        var windowRectangle = new System.Drawing.Rectangle(
            (int)Math.Round(Left),
            (int)Math.Round(Top),
            (int)Math.Round(Width),
            (int)Math.Round(Height));

        if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(windowRectangle)))
        {
            return;
        }

        CenterOnPrimaryScreen();
    }

    private void FitWindowToPrimaryWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        var maxWidth = Math.Max(MinWidth, workArea.Width * 0.92);
        var maxHeight = Math.Max(MinHeight, workArea.Height * 0.88);
        Width = Math.Min(Math.Max(MinWidth, Width), maxWidth);
        Height = Math.Min(Math.Max(MinHeight, Height), maxHeight);
    }

    private void CenterOnPrimaryScreen()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.AllScreens.First().WorkingArea;
        Left = area.Left + Math.Max(0, (area.Width - Width) / 2.0);
        Top = area.Top + Math.Max(0, (area.Height - Height) / 2.0);
    }

    private async Task SaveWindowBoundsAsync()
    {
        if (WindowState is WindowState.Minimized)
        {
            return;
        }

        try
        {
            var settings = await _settingsService.LoadAsync();
            var bounds = RestoreBounds;
            settings.Ui.WindowLeft = bounds.Left;
            settings.Ui.WindowTop = bounds.Top;
            settings.Ui.WindowWidth = Math.Max(MinWidth, bounds.Width);
            settings.Ui.WindowHeight = Math.Max(MinHeight, bounds.Height);
            await _settingsService.SaveAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save window bounds.");
        }
    }

    private async Task RunTrayCommandAsync(Func<Task<GameShell.Core.Models.ModeResult>> command)
    {
        try
        {
            await command();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tray command failed.");
        }
        finally
        {
            _webViewBridge.NotifyStatusChanged();
        }
    }

    private async Task RestorePreviousDisplayLayoutFromTrayAsync()
    {
        try
        {
            var result = await _displayTransitionService.RestorePreviousLayoutAsync();
            if (!result.Success)
            {
                _notifyIcon?.ShowBalloonTip(4000, "GameShell", result.UserMessage ?? "Nie udało się przywrócić układu ekranów.", ToolTipIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore previous display layout tray command failed.");
        }
        finally
        {
            _webViewBridge.NotifyStatusChanged();
        }
    }

    private async void OnSettingsSaved(object? sender, EventArgs e)
    {
        await RegisterConfiguredHotkeysAsync();
    }

    private async Task RegisterConfiguredHotkeysAsync()
    {
        if (_source is null)
        {
            return;
        }

        UnregisterHotkey(HotkeyConsoleModeId);
        UnregisterHotkey(HotkeyPcGamingModeId);
        UnregisterHotkey(HotkeyWorkModeId);
        UnregisterProfileHotkeys();

        try
        {
            var settings = await _settingsService.LoadAsync();
            RegisterHotkey(HotkeyConsoleModeId, settings.Hotkeys?.ConsoleMode ?? "");

            var profileConfigurations = settings.Display.PcGamingProfileConfigurations;
            var hasProfileEnterHotkeys = false;

            for (var index = 0; index < profileConfigurations.Count; index++)
            {
                var configuration = profileConfigurations[index];
                var profileName = configuration.ProfileName;
                if (string.IsNullOrWhiteSpace(profileName))
                {
                    continue;
                }

                var enterId = HotkeyProfileEnterBaseId + index;
                if (RegisterHotkey(enterId, configuration.Hotkeys?.EnterProfile ?? ""))
                {
                    _profileEnterHotkeys[enterId] = profileName;
                    hasProfileEnterHotkeys = true;
                }
            }

            if (!hasProfileEnterHotkeys)
            {
                RegisterHotkey(HotkeyPcGamingModeId, settings.Hotkeys?.PcGamingMode ?? "");
            }

            RegisterHotkey(HotkeyWorkModeId, settings.Hotkeys?.WorkMode ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register configured hotkeys.");
        }
    }

    private bool RegisterHotkey(int id, string hotkey)
    {
        if (!TryParseHotkey(hotkey, out var modifiers, out var virtualKey))
        {
            return false;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (!RegisterHotKey(handle, id, modifiers, virtualKey))
        {
            _logger.LogWarning("Failed to register hotkey {Hotkey}.", hotkey);
            return false;
        }

        return true;
    }

    private void UnregisterHotkey(int id)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            UnregisterHotKey(handle, id);
        }
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is not WmHotkey)
        {
            return IntPtr.Zero;
        }

        handled = true;
        var hotkeyId = wParam.ToInt32();

        if (_profileEnterHotkeys.TryGetValue(hotkeyId, out var profileName))
        {
            if (_isHotkeyCaptureActive)
            {
                return IntPtr.Zero;
            }

            _ = RunPcGamingHotkeyAsync(profileName);
            return IntPtr.Zero;
        }

        if (_isHotkeyCaptureActive)
        {
            return IntPtr.Zero;
        }

        _ = hotkeyId switch
        {
            HotkeyConsoleModeId => RunTrayCommandAsync(() => _modeCommandService.EnterGameShellAsync()),
            HotkeyPcGamingModeId => RunPcGamingHotkeyAsync(null),
            HotkeyWorkModeId => RunTrayCommandAsync(() => _modeCommandService.EnterWorkModeAsync()),
            _ => Task.CompletedTask
        };

        return IntPtr.Zero;
    }

    private async Task RunPcGamingHotkeyAsync(string? forcedProfileName)
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            var profileName = ResolveRequestedProfileName(settings, forcedProfileName);
            if (settings.Display.PcGamingLayout is not null || settings.Display.PcGamingProfiles.Count > 0)
            {
                await _modeCommandService.EnterGameShellWithDisplayProfileAsync(profileName ?? "PcGaming");
            }
            else
            {
                await _modeCommandService.EnterGameShellAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PC Gaming hotkey command failed.");
        }
        finally
        {
            _webViewBridge.NotifyStatusChanged();
        }
    }

    private static string? ResolveRequestedProfileName(AppSettings settings, string? forcedProfileName)
    {
        if (!string.IsNullOrWhiteSpace(forcedProfileName)
            && settings.Display.PcGamingProfiles.Any(profile => string.Equals(profile.Name, forcedProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            return forcedProfileName;
        }

        if (!string.IsNullOrWhiteSpace(settings.Display.ActivePcGamingProfileName)
            && settings.Display.PcGamingProfiles.Any(profile => string.Equals(profile.Name, settings.Display.ActivePcGamingProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            return settings.Display.ActivePcGamingProfileName;
        }

        return settings.Display.PcGamingProfiles.FirstOrDefault()?.Name;
    }

    private void UnregisterProfileHotkeys()
    {
        foreach (var hotkeyId in _profileEnterHotkeys.Keys.ToList())
        {
            UnregisterHotkey(hotkeyId);
        }

        _profileEnterHotkeys.Clear();
    }

    private static bool TryParseHotkey(string? value, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        foreach (var part in parts[..^1])
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModControl;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModAlt;
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModShift;
            }
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModWin;
            }
        }

        if (modifiers == 0)
        {
            return false;
        }

        var keyName = parts[^1].ToUpperInvariant();
        var key = keyName.Length == 1 && (keyName[0] is >= 'A' and <= 'Z' || keyName[0] is >= '0' and <= '9')
            ? KeyInterop.KeyFromVirtualKey(keyName[0])
            : Enum.TryParse<Key>(keyName, true, out var parsedKey)
                ? parsedKey
                : Key.None;

        if (key is Key.None)
        {
            return false;
        }

        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        e.Cancel = true;

        try
        {
            var settings = await _settingsService.LoadAsync();
            if (settings.Ui.CloseToTray)
            {
                await SaveWindowBoundsAsync();
                Hide();
                return;
            }

            _isExitRequested = true;
            await SaveWindowBoundsAsync();
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read close-to-tray setting. Hiding window.");
            await SaveWindowBoundsAsync();
            Hide();
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        await SaveWindowBoundsAsync();
        _webViewBridge.SettingsSaved -= OnSettingsSaved;
        _webViewBridge.HotkeyCaptureStateChanged -= OnHotkeyCaptureStateChanged;
        UnregisterProfileHotkeys();
        UnregisterHotkey(HotkeyConsoleModeId);
        UnregisterHotkey(HotkeyPcGamingModeId);
        UnregisterHotkey(HotkeyWorkModeId);
        _source?.RemoveHook(OnWindowMessage);
        _notifyIcon?.Dispose();
    }

    private async void OnHotkeyCaptureStateChanged(object? sender, bool isActive)
    {
        if (_isHotkeyCaptureActive == isActive)
        {
            return;
        }

        _isHotkeyCaptureActive = isActive;

        if (_source is null)
        {
            return;
        }

        if (isActive)
        {
            UnregisterHotkey(HotkeyConsoleModeId);
            UnregisterHotkey(HotkeyPcGamingModeId);
            UnregisterHotkey(HotkeyWorkModeId);
            UnregisterProfileHotkeys();
            return;
        }

        await RegisterConfiguredHotkeysAsync();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
