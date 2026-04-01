using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BitLockerUFCV.Client.Models;

namespace BitLockerUFCV.Client.Services;

public sealed class BitLockerService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly PowerShellRunner _powerShellRunner;

    public BitLockerService(PowerShellRunner powerShellRunner)
    {
        _powerShellRunner = powerShellRunner;
    }

    public async Task<BitLockerVolumeStateResult> GetVolumeStateAsync(CancellationToken cancellationToken = default)
    {
        const string script = """
try { Import-Module BitLocker -ErrorAction Stop } catch { }

$blv = Get-BitLockerVolume -MountPoint 'C:'

[pscustomobject]@{
    volumeStatus = [string]$blv.VolumeStatus
    protectionStatus = [string]$blv.ProtectionStatus
} | ConvertTo-Json -Compress
""";

        PowerShellExecutionResult result = await _powerShellRunner.RunAsync(script, cancellationToken);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(BuildFailureMessage("lecture de l'état BitLocker", result.StandardError, result.StandardOutput));
        }

        BitLockerVolumeStateResult? volumeState = JsonSerializer.Deserialize<BitLockerVolumeStateResult>(result.StandardOutput.Trim(), JsonOptions);
        if (volumeState is null)
        {
            throw new InvalidOperationException("Impossible d'interpréter l'état BitLocker retourné par PowerShell.");
        }

        return volumeState;
    }

    public async Task<ProvisioningResult> ProvisionAsync(
        string pin,
        IProgress<ProvisioningProgressEvent> progress,
        CancellationToken cancellationToken = default)
    {
        const string script = """
param([string]$Pin)

function Emit([int]$percent, [string]$text, [string]$tag = 'info') {
    [pscustomobject]@{
        kind = 'progress'
        percent = $percent
        text = $text
        tag = $tag
    } | ConvertTo-Json -Compress | Write-Output
}

function EmitResult([string]$status, [string]$message) {
    [pscustomobject]@{
        kind = 'result'
        status = $status
        message = $message
    } | ConvertTo-Json -Compress | Write-Output
}

try { Import-Module BitLocker -ErrorAction Stop } catch { }

$mountPoint = 'C:'
$encryptionMethod = 'XtsAes256'

Emit 5 "Vérification de l'état BitLocker..."
$blv = Get-BitLockerVolume -MountPoint $mountPoint

if ($blv.VolumeStatus -eq 'EncryptionInProgress') {
    EmitResult 'already' "Un chiffrement BitLocker est déjà en cours. Patientez puis relancez."
    return
}

if ($blv.VolumeStatus -eq 'DecryptionInProgress') {
    EmitResult 'already' "Un déchiffrement BitLocker est en cours. Patientez puis relancez."
    return
}

if ($blv.VolumeStatus -eq 'FullyEncrypted' -and $blv.ProtectionStatus -eq 'On') {
    EmitResult 'already' "BitLocker est déjà activé sur ce poste. Aucune action nécessaire."
    return
}

function Get-Protector([string]$mp, [string]$type) {
    (Get-BitLockerVolume -MountPoint $mp).KeyProtector | Where-Object { $_.KeyProtectorType -eq $type }
}

function Get-FirstProtectorId([string]$mp, [string]$type) {
    Get-Protector -mp $mp -type $type | Select-Object -ExpandProperty KeyProtectorId -First 1
}

Emit 20 "Étape 1/3 : vérification / création du RecoveryPassword..."
$recId = Get-FirstProtectorId -mp $mountPoint -type 'RecoveryPassword'

if (-not $recId) {
    Add-BitLockerKeyProtector -MountPoint $mountPoint -RecoveryPasswordProtector -ErrorAction Stop | Out-Null
    $recId = Get-FirstProtectorId -mp $mountPoint -type 'RecoveryPassword'
    if (-not $recId) {
        throw 'Impossible de récupérer l''ID du RecoveryPassword après création.'
    }

    Emit 30 'RecoveryPassword ajouté.' 'ok'
}
else {
    Emit 30 'RecoveryPassword déjà présent (réutilisation).' 'ok'
}

Emit 45 "Étape 2/3 : sauvegarde du RecoveryPassword dans AD DS..."
Backup-BitLockerKeyProtector -MountPoint $mountPoint -KeyProtectorId $recId -ErrorAction Stop | Out-Null
Emit 55 'Sauvegarde AD effectuée.' 'ok'

Emit 65 "Étape 3/3 : activation BitLocker (Used Space Only, TPM + PIN)..."
$userPin = ConvertTo-SecureString $Pin -AsPlainText -Force
$existingTpmPins = @(Get-Protector -mp $mountPoint -type 'TpmPin')

if ($existingTpmPins.Count -gt 0) {
    Emit 70 'Un protecteur TPM+PIN existe déjà : suppression avant recréation...' 'warn'
    foreach ($kp in $existingTpmPins) {
        Remove-BitLockerKeyProtector -MountPoint $mountPoint -KeyProtectorId $kp.KeyProtectorId -ErrorAction Stop
    }

    Emit 72 'Protecteur(s) TPM+PIN supprimé(s).' 'ok'
}

try {
    Enable-BitLocker -MountPoint $mountPoint `
        -EncryptionMethod $encryptionMethod `
        -UsedSpaceOnly `
        -TpmAndPinProtector `
        -Pin $userPin `
        -ErrorAction Stop | Out-Null

    Emit 85 'Enable-BitLocker lancé.' 'ok'
}
catch {
    $message = $_.Exception.Message
    $hResult = $_.Exception.HResult

    if ($hResult -eq -2144272384 -or $message -match '0x80310060') {
        New-Item -ItemType File -Path "$env:ProgramData\BitLockerActivation\PendingReboot.flag" -Force | Out-Null
        EmitResult 'policy_pending' "La stratégie BitLocker n'autorise pas encore le PIN (0x80310060). Redémarrez puis relancez le script."
        return
    }

    throw "Échec Enable-BitLocker : $message"
}

$counterPath = "$env:ProgramData\BitLockerActivation\PostponeCount.txt"
if (Test-Path $counterPath) {
    Remove-Item $counterPath -Force
}

Emit 100 'Configuration terminée. Redémarrage requis pour finaliser et démarrer le chiffrement.' 'done'
EmitResult 'success' 'BitLocker configuré. Un redémarrage est requis.'
""";

        ProvisioningResult? finalResult = null;

        PowerShellExecutionResult executionResult = await _powerShellRunner.RunStreamingAsync(
            script,
            async line =>
            {
                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(line);
                }
                catch (JsonException)
                {
                    return;
                }

                using (document)
                {
                    JsonElement root = document.RootElement;
                    string kind = root.TryGetProperty("kind", out JsonElement kindElement)
                        ? kindElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (string.Equals(kind, "progress", StringComparison.OrdinalIgnoreCase))
                    {
                        int percent = root.GetProperty("percent").GetInt32();
                        string text = root.GetProperty("text").GetString() ?? string.Empty;
                        string tag = root.TryGetProperty("tag", out JsonElement tagElement)
                            ? tagElement.GetString() ?? string.Empty
                            : string.Empty;

                        progress.Report(new ProvisioningProgressEvent(percent, text, MapProgressKind(tag)));
                    }
                    else if (string.Equals(kind, "result", StringComparison.OrdinalIgnoreCase))
                    {
                        string status = root.GetProperty("status").GetString() ?? string.Empty;
                        string message = root.GetProperty("message").GetString() ?? string.Empty;
                        finalResult = new ProvisioningResult(MapOutcome(status), message);
                    }
                }

                await Task.CompletedTask;
            },
            cancellationToken);

        if (!executionResult.Succeeded)
        {
            throw new InvalidOperationException(BuildFailureMessage("provisionnement BitLocker", executionResult.StandardError, executionResult.StandardOutput));
        }

        if (finalResult is null)
        {
            throw new InvalidOperationException("Le script de provisioning BitLocker n'a retourné aucun résultat final.");
        }

        return finalResult;
    }

    private static ProvisioningOutcome MapOutcome(string status)
    {
        return status switch
        {
            "already" => ProvisioningOutcome.Already,
            "policy_pending" => ProvisioningOutcome.PolicyPending,
            _ => ProvisioningOutcome.Success
        };
    }

    private static ProgressStepKind MapProgressKind(string tag)
    {
        return tag switch
        {
            "ok" or "done" => ProgressStepKind.Success,
            "warn" => ProgressStepKind.Warning,
            _ => ProgressStepKind.InProgress
        };
    }

    private static string BuildFailureMessage(string operation, string standardError, string standardOutput)
    {
        string details = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
        return $"Erreur lors du {operation} : {details.Trim()}";
    }
}
