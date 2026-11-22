using System.ComponentModel;
using System.Diagnostics;
using VibeCode.MdEditor.Core.Helpers;

namespace VibeCode.MdEditor.Cli;

internal static class GuiExportInvoker
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;
    private const int ExitAppMissing = 4;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    public static async Task ExportAsync(CliExportOptions options, string inputPath, string outputPath)
    {
        var appPath = ResolveAppPath();
        var startInfo = new ProcessStartInfo(appPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };

        startInfo.ArgumentList.Add(AutomationExportArguments.ExportSwitch);
        startInfo.ArgumentList.Add(options.Format.ToString().ToLowerInvariant());
        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputPath);

        if (!string.IsNullOrWhiteSpace(options.Theme))
        {
            startInfo.ArgumentList.Add("--theme");
            startInfo.ArgumentList.Add(options.Theme);
        }

        if (options.Force)
        {
            startInfo.ArgumentList.Add("--force");
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new CliFailureException(ExitAppMissing, "無法啟動 MdEditor.exe。");
            }
        }
        catch (Win32Exception ex)
        {
            throw new CliFailureException(ExitAppMissing, $"無法啟動 MdEditor.exe：{ex.Message}", ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(Timeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw new CliFailureException(ExitFailure, "HTML／PDF 匯出逾時。");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != ExitSuccess)
        {
            var message = FirstNonEmpty(
                stderr,
                stdout,
                $"MdEditor 自動化匯出失敗（結束代碼 {process.ExitCode}）。");
            throw new CliFailureException(MapExitCode(process.ExitCode), message);
        }

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
        {
            throw new CliFailureException(ExitFailure, "MdEditor 結束但未產生有效的輸出檔。");
        }
    }

    private static string ResolveAppPath()
    {
        var configured = Environment.GetEnvironmentVariable("MDEDITOR_APP_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var explicitPath = Path.GetFullPath(configured);
            if (File.Exists(explicitPath))
            {
                return explicitPath;
            }

            throw new CliFailureException(ExitAppMissing, $"找不到 MDEDITOR_APP_PATH 指定的檔案：{explicitPath}");
        }

        var appPath = Path.Combine(AppContext.BaseDirectory, "MdEditor.exe");
        if (File.Exists(appPath))
        {
            return appPath;
        }

        throw new CliFailureException(
            ExitAppMissing,
            $"找不到 MdEditor.exe。請將 mdeditor-cli.exe 與 MdEditor.exe 放在同一資料夾，或設定 MDEDITOR_APP_PATH。");
    }

    private static int MapExitCode(int appExitCode)
    {
        return appExitCode is 2 or 3 or 4 or 5 ? appExitCode : ExitFailure;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.First(value => !string.IsNullOrWhiteSpace(value));
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
        }
    }
}
