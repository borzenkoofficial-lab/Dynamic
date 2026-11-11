using Dynamic.Core;
using Xunit;

namespace Dynamic.Tests;

public sealed class CapsuleStateMachineTests
{
    [Fact]
    public void RepeatedRequestForCurrentTargetDoesNotRestartTransition()
    {
        var state = new CapsuleStateMachine();

        Assert.True(state.Request(CapsuleState.Expanded));
        Assert.Equal(CapsuleState.Expanding, state.Current);
        Assert.False(state.Request(CapsuleState.Expanded));
        Assert.Equal(CapsuleState.Expanded, state.Target);
    }

    [Fact]
    public void DirectionCanBeReversedBeforeAnimationCompletes()
    {
        var state = new CapsuleStateMachine();

        Assert.True(state.Request(CapsuleState.Expanded));
        Assert.True(state.Request(CapsuleState.Compact));
        Assert.Equal(CapsuleState.Collapsing, state.Current);
        Assert.Equal(CapsuleState.Compact, state.Target);

        state.CompleteTransition();

        Assert.Equal(CapsuleState.Compact, state.Current);
        Assert.Equal(CapsuleState.Compact, state.Target);
        Assert.False(state.IsTransitioning);
    }

    [Fact]
    public void RapidExpandCollapseExpandEndsInExpandedState()
    {
        var state = new CapsuleStateMachine();

        state.Request(CapsuleState.Expanded);
        state.Request(CapsuleState.Compact);
        state.Request(CapsuleState.Expanded);
        state.CompleteTransition();

        Assert.Equal(CapsuleState.Expanded, state.Current);
        Assert.Equal(CapsuleState.Expanded, state.Target);
    }
}
