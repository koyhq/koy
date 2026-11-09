using System.Diagnostics;
using System.Globalization;
using Koy.Core.Diagnostics;
using Koy.Core.Voice;

namespace Koy.Platform.Voice;

/// <summary>
/// CLI kodlama aracı (claude, codex, agy veya özel) süreçlerini güvenli şekilde çalıştıran servis.
/// Asla kabuk (cmd/powershell) kullanmaz, argümanları doğrudan ArgumentList ile geçirir
/// ve iptal/zaman aşımı durumunda tüm süreç ağacını (entireProcessTree: true) sonlandırır.
/// Hata metni üretmez; <see cref="VoiceFailure"/> kodu döner, metni App seçili dilde yazar.
/// </summary>
public sealed class ProcessHarnessRunnerService : IHarnessRunnerService
{
    public async Task<HarnessExecutionResult> ExecuteAsync(HarnessExecutionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolved = VoiceCommandParser.Resolve(
            request.HarnessType,
            request.Prompt,
            request.CustomExecutable,
            request.CustomArgumentsTemplate,
            request.ClaudePermission,
            request.Model);

        var workingDir = ResolveWorkingDirectory(request.WorkingDirectory);
        var timeout = TimeSpan.FromSeconds(Math.Max(5, request.TimeoutSeconds));

        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var executable = HarnessExecutableResolver.Resolve(resolved.Executable, pathDirs, File.Exists);

        if (executable.Kind == HarnessExecutableKind.ScriptNotSupported)
            return Failed(VoiceFailure.ScriptNotSupported, executable.Path, TimeSpan.Zero);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var psi = new ProcessStartInfo
        {
            // Bulunamadıysa ad olduğu gibi bırakılır; Win32 hatası aşağıdaki "bulunamadı" yolunda yakalanır.
            FileName = executable.Kind == HarnessExecutableKind.Found ? executable.Path : resolved.Executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDir
        };

        foreach (var arg in resolved.Arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        var sw = Stopwatch.StartNew();

        try
        {
            using var process = new Process { StartInfo = psi };
            if (!process.Start())
                return Failed(VoiceFailure.StartFailed, resolved.Executable, sw.Elapsed);

            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    CrashLog.Write("ProcessHarnessRunnerService.Kill", ex);
                }

                if (ct.IsCancellationRequested)
                    throw;

                return Failed(VoiceFailure.Timeout, timeout.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture), sw.Elapsed);
            }

            var output = await outputTask;
            var error = await errorTask;
            sw.Stop();

            var success = process.ExitCode == 0;
            return new HarnessExecutionResult(
                Success: success,
                ExitCode: process.ExitCode,
                Output: output.Trim(),
                Error: error.Trim(),
                Duration: sw.Elapsed,
                Failure: success ? VoiceFailure.None : VoiceFailure.ExitCode,
                Detail: process.ExitCode.ToString(CultureInfo.InvariantCulture));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            return Failed(VoiceFailure.CommandNotFound, resolved.Executable, sw.Elapsed);
        }
        catch (Exception ex)
        {
            CrashLog.Write("ProcessHarnessRunnerService.ExecuteAsync", ex);
            return new HarnessExecutionResult(
                Success: false,
                ExitCode: -1,
                Output: string.Empty,
                Error: ex.Message,
                Duration: sw.Elapsed,
                Failure: VoiceFailure.StartFailed,
                Detail: resolved.Executable);
        }
    }

    private static HarnessExecutionResult Failed(VoiceFailure failure, string detail, TimeSpan duration) =>
        new(Success: false, ExitCode: -1, Output: string.Empty, Error: string.Empty, Duration: duration,
            Failure: failure, Detail: detail);

    private static string ResolveWorkingDirectory(string? specifiedDir)
    {
        if (!string.IsNullOrWhiteSpace(specifiedDir) && Directory.Exists(specifiedDir))
            return specifiedDir;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(userProfile))
            return userProfile;

        return AppContext.BaseDirectory;
    }
}
