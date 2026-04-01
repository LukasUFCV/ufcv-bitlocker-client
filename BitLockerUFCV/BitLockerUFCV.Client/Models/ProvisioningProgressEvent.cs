namespace BitLockerUFCV.Client.Models;

public sealed record ProvisioningProgressEvent(int Percent, string Text, ProgressStepKind Kind);
