namespace Dynamic.Core;

public enum CapsuleState
{
    Hidden,
    Compact,
    Expanding,
    Expanded,
    Collapsing
}

public sealed class CapsuleStateChangedEventArgs(
    CapsuleState previous,
    CapsuleState current) : EventArgs
{
    public CapsuleState Previous { get; } = previous;
    public CapsuleState Current { get; } = current;
}