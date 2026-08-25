using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class TransientOperationStateTests
{
    [Fact]
    public void CompletedOperationClearsTheCanonicalState()
    {
        var state = new TransientOperationStateService(
            NullLogger<TransientOperationStateService>.Instance);

        var transitionId = state.Begin(
            TransientOperationKind.ExitToWorkMode,
            TransientOperationPhase.RestoringDisplays,
            "work-1");
        state.SetPhase(transitionId!, TransientOperationPhase.RestoringDesktop);

        Assert.True(state.Current.IsActive);
        Assert.Equal(TransientOperationPhase.RestoringDesktop, state.Current.Phase);

        Assert.True(state.Clear(transitionId!, "Completed"));
        Assert.False(state.Current.IsActive);
        Assert.Equal(TransientOperationKind.None, state.Current.Kind);
    }

    [Fact]
    public void LateCompletionCannotClearANewerOperation()
    {
        var state = new TransientOperationStateService(
            NullLogger<TransientOperationStateService>.Instance);

        var first = state.Begin(
            TransientOperationKind.EnterProfile,
            TransientOperationPhase.PreparingDisplays,
            "profile-a");
        Assert.False(state.Clear("profile-b", "LateCompletion"));
        Assert.Equal(first, state.Current.TransitionId);

        Assert.True(state.Clear(first!, "Failed"));
        var second = state.Begin(
            TransientOperationKind.EnterProfile,
            TransientOperationPhase.StartingLauncher,
            "profile-b");

        Assert.False(state.Clear(first!, "LateCompletion"));
        Assert.Equal(second, state.Current.TransitionId);
        Assert.True(state.Current.IsActive);
    }
}
