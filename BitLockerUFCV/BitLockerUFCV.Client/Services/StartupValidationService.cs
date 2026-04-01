using System;
using System.Threading;
using System.Threading.Tasks;
using BitLockerUFCV.Client.Models;

namespace BitLockerUFCV.Client.Services;

public sealed class StartupValidationService
{
    private readonly ExecutionContextService _executionContextService;
    private readonly RegistryPolicyService _registryPolicyService;
    private readonly DomainConnectivityService _domainConnectivityService;
    private readonly PostponeCounterService _postponeCounterService;
    private readonly BitLockerService _bitLockerService;

    public StartupValidationService(
        ExecutionContextService executionContextService,
        RegistryPolicyService registryPolicyService,
        DomainConnectivityService domainConnectivityService,
        PostponeCounterService postponeCounterService,
        BitLockerService bitLockerService)
    {
        _executionContextService = executionContextService;
        _registryPolicyService = registryPolicyService;
        _domainConnectivityService = domainConnectivityService;
        _postponeCounterService = postponeCounterService;
        _bitLockerService = bitLockerService;
    }

    public async Task<StartupAssessment> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        string? systemContextWarning = _executionContextService.GetSystemContextWarning();

        PolicyCheckSummary policySummary = _registryPolicyService.CheckCurrentPolicy();
        if (!policySummary.IsEligible)
        {
            AppDialog dialog = new(
                "BitLocker - Poste non éligible",
                "Ce poste n'est pas éligible au déploiement BitLocker pour le moment." + Environment.NewLine + Environment.NewLine +
                "La configuration attendue (GPO BitLocker) n'est pas appliquée." + Environment.NewLine + Environment.NewLine +
                $"OK : {policySummary.OkCount} / DIFF/TYPE : {policySummary.DiffTypeCount} / MISSING : {policySummary.MissingCount}" + Environment.NewLine + Environment.NewLine +
                "Veuillez contacter la DSI (UFCV).");

            return new StartupAssessment(systemContextWarning, null, policySummary, dialog);
        }

        DomainConnectivityResult domainConnectivity = await _domainConnectivityService.ValidateAsync(cancellationToken);
        if (!domainConnectivity.Success)
        {
            AppDialog dialog = new(
                "BitLocker - Réseau requis",
                "Ce poste n'est pas connecté au réseau UFCV (LAN/VPN) ou n'est pas sur le bon domaine." + Environment.NewLine + Environment.NewLine +
                $"Domaine attendu : {domainConnectivity.ExpectedDomain}" + Environment.NewLine +
                $"Détail : {domainConnectivity.Detail}" + Environment.NewLine + Environment.NewLine +
                "Veuillez vous connecter au réseau interne ou au VPN UFCV puis relancer.");

            return new StartupAssessment(systemContextWarning, null, policySummary, dialog);
        }

        PostponeCounterState postponeCounterState = _postponeCounterService.GetState();

        BitLockerVolumeStateResult volumeState;
        try
        {
            volumeState = await _bitLockerService.GetVolumeStateAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            AppDialog errorDialog = new(
                "BitLocker - Erreur",
                $"Impossible de lire l'état BitLocker : {exception.Message}");

            return new StartupAssessment(systemContextWarning, postponeCounterState, policySummary, errorDialog);
        }

        AppDialog? volumeDialog = volumeState.VolumeStatus switch
        {
            "EncryptionInProgress" => new AppDialog(
                "Information",
                "Un chiffrement BitLocker est déjà en cours sur ce poste. Patientez jusqu'à la fin avant de relancer."),
            "DecryptionInProgress" => new AppDialog(
                "Information",
                "Un déchiffrement BitLocker est actuellement en cours. Attendez qu'il soit terminé avant de relancer."),
            "FullyEncrypted" when string.Equals(volumeState.ProtectionStatus, "On", StringComparison.OrdinalIgnoreCase) => new AppDialog(
                "Information",
                "BitLocker est déjà activé sur ce poste. Aucune action n'est nécessaire."),
            _ => null
        };

        return new StartupAssessment(systemContextWarning, postponeCounterState, policySummary, volumeDialog);
    }
}
