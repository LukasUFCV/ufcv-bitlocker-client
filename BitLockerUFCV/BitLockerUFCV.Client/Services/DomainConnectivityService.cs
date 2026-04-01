using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BitLockerUFCV.Client.Models;

namespace BitLockerUFCV.Client.Services;

public sealed class DomainConnectivityService
{
    private const string ExpectedDomain = "ufcvfr.lan";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly PowerShellRunner _powerShellRunner;

    public DomainConnectivityService(PowerShellRunner powerShellRunner)
    {
        _powerShellRunner = powerShellRunner;
    }

    public async Task<DomainConnectivityResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        const string script = """
$expectedDomain = 'ufcvfr.lan'.Trim().ToLowerInvariant()

try {
    $domainObj = [System.DirectoryServices.ActiveDirectory.Domain]::GetCurrentDomain()
    $currentDomain = ($domainObj.Name).ToLowerInvariant()
    $dcObj = $domainObj.FindDomainController()
    $dcName = ($dcObj.Name).ToLowerInvariant()

    if ($currentDomain -ne $expectedDomain) {
        throw "Domaine détecté : $currentDomain (attendu : $expectedDomain)."
    }

    if (-not $dcName.EndsWith("." + $expectedDomain)) {
        throw "Contrôleur de domaine détecté : $dcName (hors domaine $expectedDomain)."
    }

    [pscustomobject]@{
        success = $true
        expectedDomain = $expectedDomain
        currentDomain = $currentDomain
        domainController = $dcName
        detail = $null
    } | ConvertTo-Json -Compress
}
catch {
    [pscustomobject]@{
        success = $false
        expectedDomain = $expectedDomain
        currentDomain = $null
        domainController = $null
        detail = $_.Exception.Message
    } | ConvertTo-Json -Compress
}
""";

        PowerShellExecutionResult result = await _powerShellRunner.RunAsync(script, cancellationToken);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(BuildFailureMessage("vérification du domaine UFCV", result.StandardError, result.StandardOutput));
        }

        DomainConnectivityResult? connectivityResult = JsonSerializer.Deserialize<DomainConnectivityResult>(result.StandardOutput.Trim(), JsonOptions);
        return connectivityResult ?? new DomainConnectivityResult(false, ExpectedDomain, null, null, "Réponse PowerShell introuvable.");
    }

    private static string BuildFailureMessage(string operation, string standardError, string standardOutput)
    {
        string details = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
        return $"Erreur lors de la {operation} : {details.Trim()}";
    }
}
