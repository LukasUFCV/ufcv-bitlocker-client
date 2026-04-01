using System.Security.Principal;

namespace BitLockerUFCV.Client.Services;

public sealed class ExecutionContextService
{
    private const string LocalSystemSid = "S-1-5-18";
    private const string WarningMessage = "Script conçu pour le contexte SYSTEM (LocalSystem). Exécutez-le en tant que SYSTEM si nécessaire.";

    public string? GetSystemContextWarning()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value == LocalSystemSid ? null : WarningMessage;
    }
}
