using System.Reflection;
using System.Text;
using VibeCode.MdEditor.Core.Helpers;

namespace VibeCode.MdEditor.Cli;

internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;
    private const int ExitUsage = 2;

    private static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.InputEncoding = Encoding.UTF8;
        }
        catch (Exception)
        {
        }

        try
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return ExitSuccess;
            }

            if (args.Any(argument =>
                    argument.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                    argument.Equals("-h", StringComparison.OrdinalIgnoreCase)))
            {
                PrintHelp();
                return ExitSuccess;
            }

            if (args[0].Equals("version", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("--version", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("-v", StringComparison.OrdinalIgnoreCase))
            {
                PrintVersion();
                return ExitSuccess;
            }

            if (!args[0].Equals("export", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"未知命令：{args[0]}");
                Console.Error.WriteLine();
                PrintHelp();
                return ExitUsage;
            }

            if (!CliExportArguments.TryParse(args, out var options, out var error))
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine();
                PrintHelp();
                return ExitUsage;
            }

            return await CliExportCommand.ExecuteAsync(options!);
        }
        catch (CliFailureException ex)
        {
            Console.Error.WriteLine($"錯誤：{ex.Message}");
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"錯誤：{ex.Message}");
            return ExitFailure;
        }
    }

    private static void PrintVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        Console.Out.WriteLine($"mdeditor-cli {version}");
    }

    private static void PrintHelp()
    {
        Console.Out.WriteLine(
            """
            MiniMdEditor 命令列匯出工具

            用法：
              mdeditor-cli export docx <input.md> [-o <output.docx>] [--force] [--quiet]
              mdeditor-cli export html <input.md> [-o <output.html>] [--theme <theme>] [--force] [--quiet]
              mdeditor-cli export pdf  <input.md> [-o <output.pdf>]  [--theme <theme>] [--force] [--quiet]
              mdeditor-cli help
              mdeditor-cli version

            說明：
              - 未指定 -o 時，輸出到輸入檔旁，使用相同主檔名。
              - 預設不覆寫既有檔案；需要覆寫時加上 --force。
              - --theme 僅適用 html 與 pdf，未指定時使用 App 目前主題。
              - html 與 pdf 需要 WebView2 Runtime 與同資料夾的 MdEditor.exe。
              - docx 不需要 WebView2。
              - 錯誤訊息會輸出到 stderr。

            範例：
              mdeditor-cli export docx note.md
              mdeditor-cli export html note.md -o out\note.html --theme dark
              mdeditor-cli export pdf note.md --force --quiet

            結束代碼：
              0 成功、1 一般錯誤、2 參數錯誤、3 輸入檔錯誤、4 缺少 App／WebView2、5 輸出檔已存在
            """);
    }
}
