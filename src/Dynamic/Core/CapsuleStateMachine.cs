namespace Dynamic.Core;

public sealed class CapsuleStateMachine
{
    public CapsuleState Current { get; private set; } = CapsuleState.Compact;
    public CapsuleState Target { get; private set; } = CapsuleState.Compact;
    public bool IsTransitioning => Current is CapsuleState.Expanding or CapsuleState.Collapsing;

    public event EventHandler<CapsuleStateChangedEventArgs>? StateChanged;

    public bool Request(CapsuleState target)
    {
        if (target is not (CapsuleState.Hidden or CapsuleState.Compact or CapsuleState.Expanded))
            throw new ArgumentOutOfRangeException(nameof(target), "Only stable states can be requested.");

        if (Target == target)
            return false;

        var previous = Current;
        Target = target;
        Current = target switch
        {
            CapsuleState.Expanded => CapsuleState.Expanding,
            CapsuleState.Compact => CapsuleState.Collapsing,
            _ => target
        };

        StateChanged?.Invoke(this, new CapsuleStateChangedEventArgs(previous, Current));
        return true;
    }

    public void CompleteTransition()
    {
        if (!IsTransitioning)
            return;

        var previous = Current;
        Current = Target;
        StateChanged?.Invoke(this, new CapsuleStateChangedEventArgs(previous, Current));
    }
}
