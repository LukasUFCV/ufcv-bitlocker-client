using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using BitLockerUFCV.Client.Models;

namespace BitLockerUFCV.Client.Services;

public sealed class RegistryPolicyService
{
    private const string FveSubKey = @"SOFTWARE\Policies\Microsoft\FVE";

    private static readonly IReadOnlyList<KeyValuePair<string, object>> RequiredKeys = new[]
    {
        new KeyValuePair<string, object>("ActiveDirectoryBackup", 1),
        new KeyValuePair<string, object>("ActiveDirectoryInfoToStore", 1),
        new KeyValuePair<string, object>("EnableBDEWithNoTPM", 0),
        new KeyValuePair<string, object>("EncryptionMethodWithXtsFdv", 7),
        new KeyValuePair<string, object>("EncryptionMethodWithXtsOs", 7),
        new KeyValuePair<string, object>("EncryptionMethodWithXtsRdv", 4),
        new KeyValuePair<string, object>("NetworkUnlockProvider", @"C:\Windows\System32\nkpprov.dll"),
        new KeyValuePair<string, object>("OSActiveDirectoryBackup", 1),
        new KeyValuePair<string, object>("OSActiveDirectoryInfoToStore", 1),
        new KeyValuePair<string, object>("OSEnablePrebootInputProtectorsOnSlates", 1),
        new KeyValuePair<string, object>("OSEncryptionType", 2),
        new KeyValuePair<string, object>("OSHideRecoveryPage", 1),
        new KeyValuePair<string, object>("OSManageDRA", 1),
        new KeyValuePair<string, object>("OSManageNKP", 1),
        new KeyValuePair<string, object>("OSRecovery", 1),
        new KeyValuePair<string, object>("OSRecoveryKey", 2),
        new KeyValuePair<string, object>("OSRecoveryPassword", 2),
        new KeyValuePair<string, object>("OSRequireActiveDirectoryBackup", 1),
        new KeyValuePair<string, object>("RequireActiveDirectoryBackup", 1),
        new KeyValuePair<string, object>("TPMAutoReseal", 1),
        new KeyValuePair<string, object>("UseAdvancedStartup", 1),
        new KeyValuePair<string, object>("UseRecoveryDrive", 1),
        new KeyValuePair<string, object>("UseRecoveryPassword", 1),
        new KeyValuePair<string, object>("UseTPM", 0),
        new KeyValuePair<string, object>("UseTPMKey", 0),
        new KeyValuePair<string, object>("UseTPMKeyPIN", 0),
        new KeyValuePair<string, object>("UseTPMPIN", 1)
    };

    public PolicyCheckSummary CheckCurrentPolicy()
    {
        List<PolicyCheckItem> items = new();

        using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? fveKey = baseKey.OpenSubKey(FveSubKey, writable: false);

        foreach ((string name, object expectedValue) in RequiredKeys.OrderBy(pair => pair.Key))
        {
            RegistryValueKind expectedKind = GetExpectedRegistryKind(expectedValue);
            object? currentValue = null;
            RegistryValueKind? currentKind = null;
            bool exists = false;

            if (fveKey is not null)
            {
                currentValue = fveKey.GetValue(name, null);
                exists = currentValue is not null;

                if (exists)
                {
                    try
                    {
                        currentKind = fveKey.GetValueKind(name);
                    }
                    catch
                    {
                        currentKind = null;
                    }
                }
            }

            bool typeOk = !exists || currentKind is null || IsTypeMatch(expectedKind, currentKind.Value);
            bool valueOk = exists && TestValueEquality(NormalizeValue(currentValue), NormalizeValue(expectedValue));

            string status =
                !exists ? "MISSING" :
                !typeOk ? "TYPE_MISMATCH" :
                !valueOk ? "DIFF" :
                "OK";

            items.Add(new PolicyCheckItem(
                name,
                status,
                expectedValue,
                currentValue,
                expectedKind.ToString(),
                currentKind?.ToString()));
        }

        int okCount = items.Count(item => item.Status == "OK");
        int diffTypeCount = items.Count(item => item.Status is "DIFF" or "TYPE_MISMATCH");
        int missingCount = items.Count(item => item.Status == "MISSING");

        return new PolicyCheckSummary(items, okCount, diffTypeCount, missingCount);
    }

    private static RegistryValueKind GetExpectedRegistryKind(object value)
    {
        return value switch
        {
            string => RegistryValueKind.String,
            int => RegistryValueKind.DWord,
            _ => RegistryValueKind.Unknown
        };
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            string stringValue => stringValue.Trim(),
            _ => value
        };
    }

    private static bool TestValueEquality(object? currentValue, object? expectedValue)
    {
        if (expectedValue is string expectedString)
        {
            return string.Equals(
                Convert.ToString(currentValue)?.Trim(),
                expectedString.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        return Equals(currentValue, expectedValue);
    }

    private static bool IsTypeMatch(RegistryValueKind expectedKind, RegistryValueKind currentKind)
    {
        if (expectedKind == RegistryValueKind.String)
        {
            return currentKind is RegistryValueKind.String or RegistryValueKind.ExpandString;
        }

        return expectedKind == currentKind;
    }
}
