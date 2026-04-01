namespace BitLockerUFCV.Client.Models;

public sealed record WindowCloseDecision(bool Cancel, AppDialog? Dialog = null);
