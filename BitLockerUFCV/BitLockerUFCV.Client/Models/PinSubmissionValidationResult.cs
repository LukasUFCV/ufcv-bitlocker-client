namespace BitLockerUFCV.Client.Models;

public sealed record PinSubmissionValidationResult(bool CanContinue, AppDialog? Dialog = null, bool ClearConfirmation = false);
