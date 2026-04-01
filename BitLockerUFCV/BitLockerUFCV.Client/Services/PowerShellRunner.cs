using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitLockerUFCV.Client.Models;

namespace BitLockerUFCV.Client.Services;

public sealed class PowerShellRunner
{
    private static readonly UTF8Encoding OutputEncoding = new(encoderShouldEmitUTF8Identifier: false);
    private readonly string _powerShellPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    public async Task<PowerShellExecutionResult> RunAsync(string script, CancellationToken cancellationToken = default)
    {
        using Process process = CreateProcess(script);
        using CancellationTokenRegistration registration = RegisterCancellation(process, cancellationToken);

        process.Start();

        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(standardOutputTask, standardErrorTask);

        return new PowerShellExecutionResult(process.ExitCode, standardOutputTask.Result, standardErrorTask.Result);
    }

    public async Task<PowerShellExecutionResult> RunStreamingAsync(
        string script,
        Func<string, Task> onOutputLineAsync,
        CancellationToken cancellationToken = default)
    {
        using Process process = CreateProcess(script);
        using CancellationTokenRegistration registration = RegisterCancellation(process, cancellationToken);

        process.Start();

        StringBuilder errorBuilder = new();
        Task outputTask = ReadLinesAsync(process.StandardOutput, onOutputLineAsync, cancellationToken);
        Task errorTask = ReadLinesAsync(
            process.StandardError,
            line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    errorBuilder.AppendLine(line);
                }

                return Task.CompletedTask;
            },
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(outputTask, errorTask);

        return new PowerShellExecutionResult(process.ExitCode, string.Empty, errorBuilder.ToString().Trim());
    }

    private Process CreateProcess(string script)
    {
        string wrappedScript = $$"""
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[System.Threading.Thread]::CurrentThread.CurrentCulture = 'fr-FR'
[System.Threading.Thread]::CurrentThread.CurrentUICulture = 'fr-FR'

{{script}}
""";

        string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrappedScript));

        ProcessStartInfo startInfo = new()
        {
            FileName = _powerShellPath,
            Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = OutputEncoding,
            StandardErrorEncoding = OutputEncoding,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };

        return new Process { StartInfo = startInfo };
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        Func<string, Task> onLineAsync,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync();

            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await onLineAsync(line);
        }
    }

    private static CancellationTokenRegistration RegisterCancellation(Process process, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return default;
        }

        return cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        });
    }
}
