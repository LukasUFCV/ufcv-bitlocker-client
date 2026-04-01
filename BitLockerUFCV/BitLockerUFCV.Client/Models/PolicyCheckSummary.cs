using System.Collections.Generic;

namespace BitLockerUFCV.Client.Models;

public sealed record PolicyCheckSummary(
    IReadOnlyList<PolicyCheckItem> Items,
    int OkCount,
    int DiffTypeCount,
    int MissingCount)
{
    public bool IsEligible => DiffTypeCount == 0 && MissingCount == 0;
}
