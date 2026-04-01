# UFCV BitLocker Client

Application Windows native WinUI 3 dédiée à l’activation BitLocker pour l’UFCV, en transposant le script de référence [`docs/BitLocker-Enable-TPM-PIN-Recovery_UFCV.ps1`](docs/BitLocker-Enable-TPM-PIN-Recovery_UFCV.ps1) en interface moderne WinUI 3, tout en conservant l’ordre des contrôles, les textes métier et les blocages fonctionnels.

## Objectif

- Remplacer l’interface PowerShell/WPF actuelle par une application Windows native.
- Conserver strictement le workflow métier du script UFCV.
- Exécuter les contrôles d’éligibilité avant toute saisie.
- Piloter l’activation BitLocker TPM + PIN + Recovery avec un écran de progression.

## Projet

- Solution : `BitLockerUFCV/BitLockerUFCV.slnx`
- Projet WinUI 3 : `BitLockerUFCV/BitLockerUFCV.Client`
- Cible : `.NET 8`, `WinUI 3`, `Windows App SDK`

## Architecture

Le code est organisé en trois couches principales dans `BitLockerUFCV.Client` :

- `ViewModels/`
  - `MainViewModel.cs` : état de l’interface, validation du PIN, orchestration de la fermeture, progression, compteur de reports.
- `Services/`
  - `ExecutionContextService.cs` : contrôle du contexte LocalSystem.
  - `RegistryPolicyService.cs` : lecture et comparaison des clés GPO BitLocker sous `HKLM\SOFTWARE\Policies\Microsoft\FVE`.
  - `DomainConnectivityService.cs` : validation du domaine `ufcvfr.lan` et d’un contrôleur de domaine joignable.
  - `PostponeCounterService.cs` : persistance du compteur `C:\ProgramData\BitLockerActivation\PostponeCount.txt`.
  - `BitLockerService.cs` : contrôle d’état BitLocker et provisioning via PowerShell contrôlé.
  - `StartupValidationService.cs` : enchaînement fidèle des prérequis avant affichage de l’écran PIN.
- `Models/`
  - états de politique FVE, progression, résultats de provisioning, décisions de fermeture, dialogues bloquants.

## Fonctionnement repris du script

L’application reprend l’ordre logique du script UFCV :

1. Contexte français côté application.
2. Avertissement si l’application ne s’exécute pas en `SYSTEM` / `LocalSystem`.
3. Vérification de la configuration GPO BitLocker dans le registre avec statuts `OK`, `DIFF`, `TYPE_MISMATCH`, `MISSING`.
4. Blocage si la configuration FVE attendue n’est pas conforme.
5. Vérification du domaine UFCV `ufcvfr.lan` et d’un contrôleur de domaine joignable.
6. Chargement du compteur de reports (`max = 99`).
7. Vérification préalable de l’état BitLocker sur `C:`.
8. Saisie et validation du PIN.
9. Provisioning asynchrone :
   - vérification / création du `RecoveryPassword`
   - sauvegarde AD DS
   - activation BitLocker `TPM + PIN`
   - gestion spécifique du cas `0x80310060`
10. Suppression du compteur en cas de succès.
11. Blocage de fermeture pendant le provisioning.

## Pré-requis

- Windows 10/11 compatible Windows App SDK.
- Accès aux packages NuGet pour le premier build/restauration.
- PowerShell Windows disponible (`powershell.exe`).
- Contexte d’exécution permettant les commandes BitLocker.
- Réseau UFCV / VPN opérationnel pour la validation de domaine et la sauvegarde AD DS.

## Build et exécution

### Build x64

Depuis la racine du dépôt :

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1"
& "C:\Program Files\dotnet\dotnet.exe" build "BitLockerUFCV\BitLockerUFCV.slnx" -p:Platform=x64 -p:NuGetAudit=false
```

### Lancement en développement

- Ouvrir `BitLockerUFCV/BitLockerUFCV.slnx` dans Visual Studio 2022.
- Choisir le profil `BitLockerUFCV.Client (Unpackaged)` pour un lancement local.
- Pour un packaging MSIX, utiliser le profil `BitLockerUFCV.Client (Package)`.

## Permissions et contexte d’exécution

- Le script de référence est conçu pour `LocalSystem`.
- L’application n’impose pas ce contexte, mais affiche un avertissement explicite si elle n’est pas exécutée en `SYSTEM`.
- Les opérations BitLocker réelles dépendent des droits effectifs du processus et des stratégies locales/domaines.
- Les erreurs d’accès ou d’environnement sont remontées à l’utilisateur via les mêmes messages métier que possible.

## Limites et écarts documentés

- La culture `fr-FR` est appliquée au processus et aux threads de l’application. Les changements système globaux du script PowerShell (`Set-Culture`, `Set-WinSystemLocale`, `Set-WinUILanguageOverride`) ne sont pas reproduits par l’application.
- Les opérations BitLocker et AD sont pilotées par des scripts PowerShell contrôlés depuis C# pour rester au plus près du comportement métier de la source de vérité.
- Aucun logo UFCV exploitable n’était présent dans le dépôt au moment de l’implémentation. Le branding reste donc textuel et sobre dans cette version.
- L’interface a été stabilisée avec un XAML volontairement simple pour garantir une build propre WinUI 3 dans ce dépôt.

## Référence fonctionnelle

- Script source de vérité : `docs/BitLocker-Enable-TPM-PIN-Recovery_UFCV.ps1`

