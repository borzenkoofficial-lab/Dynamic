namespace Dynamic.Core;

public sealed class CapsuleStateMachine
{
    public CapsuleState Current { get; private set; } = CapsuleState.Compact;
    public CapsuleState Target { get; private set; } = CapsuleState.Compact;

    public event EventHandler<CapsuleStateChangedEventArgs>? StateChanged;

    public void Request(CapsuleState target)
    {
        if (target is CapsuleState.Expanding or CapsuleState.Collapsing)
            throw new ArgumentException("Transient states are controlled by the motion engine.", nameof(target));

        if (Target == target && Current == target)
            return;

        var previous = Current;
        Target = target;
        Current = target;
        StateChanged?.Invoke(this, new CapsuleStateChangedEventArgs(previous, target));
    }
}