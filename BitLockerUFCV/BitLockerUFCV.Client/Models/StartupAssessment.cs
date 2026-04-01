namespace BitLockerUFCV.Client.Models;

public sealed record StartupAssessment(
    string? SystemContextWarning,
    PostponeCounterState? PostponeCounterState,
    PolicyCheckSummary? PolicySummary,
    AppDialog? BlockingDialog)
{
    public bool CanContinue => BlockingDialog is null;
}
