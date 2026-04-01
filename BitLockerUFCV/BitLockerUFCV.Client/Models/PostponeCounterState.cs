namespace BitLockerUFCV.Client.Models;

public sealed record PostponeCounterState(int CurrentCount, int MaxCount)
{
    public int RemainingCount => MaxCount - CurrentCount;
    public bool HasReachedLimit => CurrentCount >= MaxCount;
}
