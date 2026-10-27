using GameShell.Core.Applications;
using GameShell.Core.Profiles;
using GameShell.Core.Runtime;
using GameShell.Core.Settings;
using GameShell.Windows.Applications;
using GameShell.Windows.Desktop;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ProfileBehaviourTests
{
    private static readonly string[] Flags =
    [
        nameof(GameShellSettings.HideDesktopIcons), nameof(GameShellSettings.HideTaskbar),
        nameof(GameShellSettings.CloseVisibleWindows), nameof(GameShellSettings.MinimizeRemainingWindows),
        nameof(GameShellSettings.MoveCursorToCorner), nameof(GameShellSettings.HideCursor),
        nameof(GameShellSettings.StartLauncher), nameof(GameShellSettings.BringLauncherToForeground)
    ];

    [Fact]
    public async Task ReadyApplicationWindow_IsFocusedWithoutAnyDelay()
    {
        var focus = NativeModeWorkflowService.WaitForApplicationWindowAsync(() => true, CancellationToken.None);
        Assert.True(focus.IsCompletedSuccessfully);
        Assert.True(await focus);
    }

    [Fact]
    public async Task ApplicationWindow_IsRetriedUntilReady()
    {
        var attempts = 0;
        Assert.True(await NativeModeWorkflowService.WaitForApplicationWindowAsync(() => ++attempts == 2, CancellationToken.None));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ApplicationWindowWait_StopsWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var wait = NativeModeWorkflowService.WaitForApplicationWindowAsync(() => false, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DesktopRestore_DoesNotWaitForFallbackAfterSuccess(bool initiallyUnavailable)
    {
        var effects = new Effects { IconFailuresRemaining = initiallyUnavailable ? 1 : 0 };
        var service = new NativeModeWorkflowService(NullLogger<NativeModeWorkflowService>.Instance,
            new Runtime(), effects, effects, effects);
        var settings = new AppSettings();
        settings.WorkMode.ShowDesktopIcons = true;
        settings.WorkMode.ShowTaskbars = true;
        settings.WorkMode.DesktopIconsDelaySeconds = 5;

        var restore = service.RunWorkWorkflowAsync(settings);

        // Successful Windows calls need no retry timer or configured fallback delay.
        Assert.True(restore.IsCompletedSuccessfully);
        Assert.True((await restore).Success);
        Assert.Equal(initiallyUnavailable ? 2 : 1, effects.Calls.Count(call => call == "icons"));
    }

    [Fact]
    public void ValidationAndResolution_PreserveEveryCombinationOfProfileFlags()
    {
        for (var mask = 0; mask < 256; mask++)
        {
            var behaviour = Behaviour(mask);
            var settings = AppSettingsValidator.Validate(new AppSettings
            {
                Display = new DisplaySettings
                {
                    GamingProfiles = [new DisplayLayoutProfile { Name = "TV" }],
                    GamingProfileConfigurations = [new GamingProfileConfiguration { ProfileName = "TV", Behaviour = behaviour }]
                }
            });
            var effective = new ProfileSettingsResolver().BuildEffectiveSettings(settings, "TV");
            for (var i = 0; i < Flags.Length; i++)
                Assert.Equal((mask & (1 << i)) != 0, typeof(GameShellSettings).GetProperty(Flags[i])!.GetValue(effective.Gaming));
        }
    }

    [Fact]
    public async Task DisplayOnlyProfile_PreservesDisabledFlagsAcrossSaveAndReload()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GameShell-behaviour-" + Guid.NewGuid());
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var service = new SettingsService(NullLogger<SettingsService>.Instance, path);
            await service.SaveAsync(new AppSettings
            {
                Display = new DisplaySettings
                {
                    GamingProfiles = [new DisplayLayoutProfile { Name = "TV" }],
                    GamingProfileConfigurations = [new GamingProfileConfiguration { ProfileName = "TV", Behaviour = Behaviour(0) }]
                }
            });
            var loaded = await new SettingsService(NullLogger<SettingsService>.Instance, path).LoadAsync();
            var effective = new ProfileSettingsResolver().BuildEffectiveSettings(loaded, "TV");
            Assert.All(Flags, flag => Assert.Equal(false, typeof(GameShellSettings).GetProperty(flag)!.GetValue(effective.Gaming)));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DisplayOnlyWorkflow_PerformsNoWindowCursorOrLauncherActions()
    {
        var effects = new Effects();
        var service = new NativeModeWorkflowService(NullLogger<NativeModeWorkflowService>.Instance,
            new Runtime(), effects, effects, effects);
        var settings = new AppSettings { Gaming = Behaviour(0) };

        Assert.True((await service.PrepareGamingWorkflowAsync(settings, GameShellMode.GamingProfile)).Success);
        Assert.True((await service.CompleteGamingWorkflowAsync(settings, GameShellMode.GamingProfile)).Success);
        Assert.Empty(effects.Calls);
    }

    [Theory]
    [InlineData(0, "icons")]
    [InlineData(1, "taskbar")]
    [InlineData(2, "close")]
    [InlineData(3, "minimize")]
    [InlineData(4, "move")]
    [InlineData(5, "hide")]
    public async Task Preparation_PerformsOnlySelectedAction(int flag, string expected)
    {
        var effects = new Effects();
        var service = new NativeModeWorkflowService(NullLogger<NativeModeWorkflowService>.Instance,
            new Runtime(), effects, effects, effects);
        Assert.True((await service.PrepareGamingWorkflowAsync(new AppSettings { Gaming = Behaviour(1 << flag) }, GameShellMode.GamingProfile)).Success);
        Assert.NotEmpty(effects.Calls);
        Assert.All(effects.Calls, call => Assert.Equal(expected, call));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_RequiresAnEnabledApplicationRegardlessOfLegacyForegroundFlag(bool foreground)
    {
        var effects = new Effects();
        var recovery = new WindowsLauncherRecoveryStrategy(NullLogger<WindowsLauncherRecoveryStrategy>.Instance, effects);
        var settings = new AppSettings { Gaming = Behaviour(0) };
        settings.Gaming.StartLauncher = false;
        settings.Gaming.BringLauncherToForeground = foreground;
        Assert.True((await recovery.RecoverAsync(settings, null)).Success);
        Assert.Empty(effects.Calls);
    }

    private static GamingSettings Behaviour(int mask)
    {
        var behaviour = new GamingSettings();
        for (var i = 0; i < Flags.Length; i++)
            typeof(GameShellSettings).GetProperty(Flags[i])!.SetValue(behaviour, (mask & (1 << i)) != 0);
        return behaviour;
    }

    private sealed class Runtime : IRuntimeStateService
    {
        public string StatePath => "unused";
        public Task<RuntimeState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new RuntimeState());
        public Task SaveAsync(RuntimeState state, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Effects : IWindowsWindowManagementService, IWindowsCursorService, ILauncherProcessService
    {
        public List<string> Calls { get; } = [];
        public int IconFailuresRemaining { get; set; }
        public bool SetDesktopIconsVisible(bool visible) { Calls.Add("icons"); return IconFailuresRemaining-- <= 0; }
        public bool SetTaskbarsVisible(bool visible) { Calls.Add("taskbar"); return true; }
        public void CloseVisibleWindows(GameShellSettings behaviour) => Calls.Add("close");
        public void CloseExplorerWindows(GameShellSettings behaviour) => Calls.Add("close");
        public void MinimizeRemainingWindows(GameShellSettings behaviour) => Calls.Add("minimize");
        public void MinimizeCurrentWindow() => Calls.Add("minimize");
        public void MinimizeExplorerWindows() => Calls.Add("minimize");
        public void RestoreAndFocus(IntPtr handle) => Calls.Add("focus");
        public bool MoveWindowToPrimaryDisplay(IntPtr handle) { Calls.Add("window"); return true; }
        public void MoveToBottomRight() => Calls.Add("move");
        public void Hide() => Calls.Add("hide");
        public void Restore() => Calls.Add("restore");
        public ExternalProcessLaunchResult StartStandardUser(LauncherSettings launcher) { Calls.Add("launcher"); throw new InvalidOperationException("Unexpected launch"); }
        public ExternalProcessLaunchResult StartCurrentContext(LauncherSettings launcher) { Calls.Add("launcher"); throw new InvalidOperationException("Unexpected launch"); }
    }
}
