using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class LauncherProcessServiceTests
{
    [Fact]
    public void StandardUserStart_RequestsStandardUserAndPreservesLauncherArguments()
    {
        var externalLauncher = new RecordingExternalProcessLauncher(ExternalProcessLaunchResult.Started(1234));
        var service = new LauncherProcessService(externalLauncher);
        var launcher = new LauncherSettings
        {
            Type = "Playnite",
            ExecutablePath = @"C:\Launchers\Playnite.FullscreenApp.exe",
            Arguments = "--startfullscreen --hidesplashscreen"
        };

        var result = service.StartStandardUser(launcher);

        Assert.True(result.Success);
        Assert.Equal(1234, result.ProcessId);
        Assert.Equal(1, externalLauncher.StandardUserCalls);
        Assert.Equal(0, externalLauncher.CurrentContextCalls);
        Assert.NotNull(externalLauncher.LastRequest);
        Assert.Equal(launcher.ExecutablePath, externalLauncher.LastRequest!.ExecutablePath);
        Assert.Equal(launcher.Arguments, externalLauncher.LastRequest.Arguments);
        Assert.Null(externalLauncher.LastRequest.WorkingDirectory);
    }

    [Fact]
    public void StandardUserStartFailure_IsReturnedWithoutCurrentContextFallback()
    {
        var failure = ExternalProcessLaunchResult.Failed("CreateProcessAsUser failed.", 5);
        var externalLauncher = new RecordingExternalProcessLauncher(failure);
        var service = new LauncherProcessService(externalLauncher);

        var result = service.StartStandardUser(new LauncherSettings());

        Assert.False(result.Success);
        Assert.Equal("CreateProcessAsUser failed.", result.FailureReason);
        Assert.Equal(5, result.Win32ErrorCode);
        Assert.Equal(1, externalLauncher.StandardUserCalls);
        Assert.Equal(0, externalLauncher.CurrentContextCalls);
    }

    [Fact]
    public void ShellLauncherPolicy_UsesStandardUserProcessRequestForSteamUri()
    {
        var request = LauncherProcessRequestFactory.Create(new LauncherSettings
        {
            Type = "Steam",
            ExecutablePath = "steam://open/bigpicture",
            Arguments = "--existing-argument"
        });

        Assert.Equal("explorer.exe", request.ExecutablePath);
        Assert.Contains("steam://open/bigpicture", request.Arguments);
        Assert.Contains("--existing-argument", request.Arguments);
        Assert.False(request.UseShellExecute);
    }

    private sealed class RecordingExternalProcessLauncher : IExternalProcessLauncher
    {
        private readonly ExternalProcessLaunchResult _result;

        public RecordingExternalProcessLauncher(ExternalProcessLaunchResult result)
        {
            _result = result;
        }

        public int StandardUserCalls { get; private set; }
        public int CurrentContextCalls { get; private set; }
        public ExternalProcessLaunchRequest? LastRequest { get; private set; }

        public ExternalProcessLaunchResult StartStandardUser(ExternalProcessLaunchRequest request)
        {
            StandardUserCalls++;
            LastRequest = request;
            return _result;
        }

        public ExternalProcessLaunchResult StartCurrentContext(ExternalProcessLaunchRequest request)
        {
            CurrentContextCalls++;
            LastRequest = request;
            return _result;
        }
    }
}
