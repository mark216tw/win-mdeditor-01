namespace VibeCode.MdEditor.Core.Helpers;

public static class AutomationExportArguments
{
    public const string ExportSwitch = "--automation-export";

    public static bool IsAutomationRequest(IReadOnlyList<string> args)
    {
        return args.Any(argument =>
            argument.Equals(ExportSwitch, StringComparison.OrdinalIgnoreCase) ||
            argument.StartsWith(ExportSwitch + "=", StringComparison.OrdinalIgnoreCase));
    }

    public static AutomationExportRequest Parse(IReadOnlyList<string> args)
    {
        string? format = null;
        string? inputPath = null;
        string? outputPath = null;
        string? theme = null;
        var force = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];

            if (TryReadOption(args, ref index, ExportSwitch, out var formatValue))
            {
                format = formatValue;
                continue;
            }

            if (TryReadOption(args, ref index, "--input", out var inputValue))
            {
                inputPath = inputValue;
                continue;
            }

            if (TryReadOption(args, ref index, "--output", out var outputValue))
            {
                outputPath = outputValue;
                continue;
            }

            if (TryReadOption(args, ref index, "--theme", out var themeValue))
            {
                theme = themeValue;
                continue;
            }

            if (argument.Equals("--force", StringComparison.OrdinalIgnoreCase))
            {
                force = true;
                continue;
            }

            throw new FormatException($"未知的自動化參數：{argument}");
        }

        var parsedFormat = format?.ToLowerInvariant() switch
        {
            "html" => AutomationExportFormat.Html,
            "pdf" => AutomationExportFormat.Pdf,
            null => throw new FormatException($"缺少 {ExportSwitch} 參數。"),
            _ => throw new FormatException($"不支援的自動化匯出格式：{format}"),
        };

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new FormatException("缺少 --input 參數。");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new FormatException("缺少 --output 參數。");
        }

        return new AutomationExportRequest(parsedFormat, inputPath, outputPath, theme, force);
    }

    private static bool TryReadOption(
        IReadOnlyList<string> args,
        ref int index,
        string name,
        out string? value)
    {
        var argument = args[index];

        if (argument.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Count)
            {
                throw new FormatException($"{name} 缺少值。");
            }

            value = args[++index];
            return true;
        }

        var equalsPrefix = name + "=";
        if (argument.StartsWith(equalsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = argument[equalsPrefix.Length..];
            return true;
        }

        value = null;
        return false;
    }
}
