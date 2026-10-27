using GameShell.Core.Runtime;
using GameShell.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.RemoteControl;

public sealed class RemoteControlCommandHandler
{
    private readonly ISettingsService _settingsService;
    private readonly IModeCommandService _modeCommandService;
    private readonly ILogger<RemoteControlCommandHandler> _logger;
    private readonly Func<Func<Task>, Task> _schedule;
    private readonly CancellationToken _applicationCancellationToken;

    public RemoteControlCommandHandler(
        ISettingsService settingsService,
        IModeCommandService modeCommandService,
        ILogger<RemoteControlCommandHandler> logger,
        CancellationToken applicationCancellationToken = default,
        Func<Func<Task>, Task>? schedule = null)
    {
        _settingsService = settingsService;
        _modeCommandService = modeCommandService;
        _logger = logger;
        _applicationCancellationToken = applicationCancellationToken;
        _schedule = schedule ?? (work =>
        {
            _ = Task.Run(work);
            return Task.CompletedTask;
        });
    }

    public async Task<RemoteStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var status = await _modeCommandService.GetStatusAsync(cancellationToken);
        return new RemoteStatusResponse(
            status.CurrentMode,
            status.SessionState,
            status.ActiveProfileName,
            status.ActiveProfileName,
            status.ActiveLauncher);
    }

    public async Task<IReadOnlyList<RemoteProfileResponse>> GetProfilesAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        return settings.Display.GamingProfiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .Select(profile => new RemoteProfileResponse(profile.Name, profile.Name))
            .DistinctBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<RemoteCommandDispatchResult> ActivateProfileAsync(
        string profileId,
        string? remoteAddress,
        CancellationToken requestCancellationToken)
    {
        var settings = await _settingsService.LoadAsync(requestCancellationToken);
        var profile = settings.Display.GamingProfiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, profileId, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            return new RemoteCommandDispatchResult(
                StatusCodes.Status404NotFound,
                new RemoteCommandResponse(false, "ActivateProfile", profileId, "ProfileNotFound"));
        }

        var status = await _modeCommandService.GetStatusAsync(requestCancellationToken);
        if (status.IsBusy)
        {
            LogRejected("ActivateProfile", profile.Name, remoteAddress, "TransitionAlreadyRunning");
            return new RemoteCommandDispatchResult(
                StatusCodes.Status409Conflict,
                new RemoteCommandResponse(false, "ActivateProfile", profile.Name, "TransitionAlreadyRunning"));
        }

        LogAccepted("ActivateProfile", profile.Name, remoteAddress);
        await _schedule(() => ExecuteProfileAsync(profile.Name, remoteAddress));
        return new RemoteCommandDispatchResult(
            StatusCodes.Status202Accepted,
            new RemoteCommandResponse(true, "ActivateProfile", profile.Name));
    }

    public async Task<RemoteCommandDispatchResult> EnterWorkModeAsync(
        string? remoteAddress,
        CancellationToken requestCancellationToken)
    {
        var status = await _modeCommandService.GetStatusAsync(requestCancellationToken);
        if (status.IsBusy)
        {
            LogRejected("EnterWorkMode", null, remoteAddress, "TransitionAlreadyRunning");
            return new RemoteCommandDispatchResult(
                StatusCodes.Status409Conflict,
                new RemoteCommandResponse(false, "EnterWorkMode", null, "TransitionAlreadyRunning"));
        }

        LogAccepted("EnterWorkMode", null, remoteAddress);
        await _schedule(() => ExecuteWorkModeAsync(remoteAddress));
        return new RemoteCommandDispatchResult(
            StatusCodes.Status202Accepted,
            new RemoteCommandResponse(true, "EnterWorkMode"));
    }

    private async Task ExecuteProfileAsync(string profileName, string? remoteAddress)
    {
        try
        {
            var result = await _modeCommandService.EnterGameShellWithDisplayProfileAsync(
                profileName,
                _applicationCancellationToken);
            LogCompletion("ActivateProfile", profileName, remoteAddress, result.Status.ToString());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[REMOTE COMMAND] Command=ActivateProfile ProfileId={ProfileId} Result=Failed", profileName);
        }
    }

    private async Task ExecuteWorkModeAsync(string? remoteAddress)
    {
        try
        {
            var result = await _modeCommandService.EnterWorkModeAsync(_applicationCancellationToken);
            LogCompletion("EnterWorkMode", null, remoteAddress, result.Status.ToString());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[REMOTE COMMAND] Command=EnterWorkMode Result=Failed");
        }
    }

    private void LogAccepted(string command, string? profileId, string? remoteAddress)
    {
        _logger.LogInformation(
            "[REMOTE COMMAND] Command={Command} ProfileId={ProfileId} RemoteAddress={RemoteAddress} Result=Accepted",
            command,
            profileId ?? "null",
            remoteAddress ?? "unknown");
    }

    private void LogRejected(string command, string? profileId, string? remoteAddress, string reason)
    {
        _logger.LogInformation(
            "[REMOTE COMMAND] Command={Command} ProfileId={ProfileId} RemoteAddress={RemoteAddress} Result=Rejected Reason={Reason}",
            command,
            profileId ?? "null",
            remoteAddress ?? "unknown",
            reason);
    }

    private void LogCompletion(string command, string? profileId, string? remoteAddress, string result)
    {
        _logger.LogInformation(
            "[REMOTE COMMAND] Command={Command} ProfileId={ProfileId} RemoteAddress={RemoteAddress} Result={Result}",
            command,
            profileId ?? "null",
            remoteAddress ?? "unknown",
            result);
    }
}
