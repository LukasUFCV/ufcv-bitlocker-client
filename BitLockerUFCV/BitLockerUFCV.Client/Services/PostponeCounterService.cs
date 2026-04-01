using System;
using System.Globalization;
using System.IO;
using BitLockerUFCV.Client.Models;

namespace BitLockerUFCV.Client.Services;

public sealed class PostponeCounterService
{
    public const int MaxPostpones = 99;

    private readonly string _counterDirectoryPath;
    private readonly string _counterFilePath;
    private readonly string _pendingRebootFlagPath;

    public PostponeCounterService()
    {
        _counterDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "BitLockerActivation");
        _counterFilePath = Path.Combine(_counterDirectoryPath, "PostponeCount.txt");
        _pendingRebootFlagPath = Path.Combine(_counterDirectoryPath, "PendingReboot.flag");
    }

    public string CounterFilePath => _counterFilePath;

    public string PendingRebootFlagPath => _pendingRebootFlagPath;

    public PostponeCounterState GetState()
    {
        EnsureDirectory();

        if (!File.Exists(_counterFilePath))
        {
            return new PostponeCounterState(0, MaxPostpones);
        }

        string rawValue = File.ReadAllText(_counterFilePath).Trim();
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int currentCount) || currentCount < 0)
        {
            currentCount = 0;
        }

        if (currentCount > MaxPostpones)
        {
            currentCount = MaxPostpones;
        }

        return new PostponeCounterState(currentCount, MaxPostpones);
    }

    public PostponeCounterState Increment()
    {
        PostponeCounterState currentState = GetState();
        int nextValue = Math.Min(currentState.CurrentCount + 1, currentState.MaxCount);
        File.WriteAllText(_counterFilePath, nextValue.ToString(CultureInfo.InvariantCulture));
        return new PostponeCounterState(nextValue, currentState.MaxCount);
    }

    public void DeleteCounterIfExists()
    {
        if (File.Exists(_counterFilePath))
        {
            File.Delete(_counterFilePath);
        }
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(_counterDirectoryPath);
    }
}
