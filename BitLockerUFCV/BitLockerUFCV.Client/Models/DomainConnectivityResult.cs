namespace BitLockerUFCV.Client.Models;

public sealed record DomainConnectivityResult(bool Success, string ExpectedDomain, string? CurrentDomain, string? DomainController, string? Detail);
