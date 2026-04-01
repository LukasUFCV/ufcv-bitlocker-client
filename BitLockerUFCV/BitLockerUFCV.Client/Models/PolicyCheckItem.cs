namespace BitLockerUFCV.Client.Models;

public sealed record PolicyCheckItem(
    string Name,
    string Status,
    object Expected,
    object? Current,
    string ExpectedType,
    string? CurrentType);
