namespace VibeCode.MdEditor.Core.Helpers;

public static class CliExportArguments
{
    public static bool TryParse(
        IReadOnlyList<string> args,
        out CliExportOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        if (args.Count == 0 || !args[0].Equals("export", StringComparison.OrdinalIgnoreCase))
        {
            error = "未知命令。請使用 export docx|html|pdf。";
            return false;
        }

        if (args.Count < 2)
        {
            error = "缺少匯出格式。請使用 export docx|html|pdf。";
            return false;
        }

        if (!TryParseFormat(args[1], out var format))
        {
            error = $"不支援的匯出格式：{args[1]}。請使用 docx、html 或 pdf。";
            return false;
        }

        string? inputPath = null;
        string? outputPath = null;
        string? theme = null;
        var force = false;
        var quiet = false;
        var positionalOnly = false;

        try
        {
            for (var index = 2; index < args.Count; index++)
            {
                var argument = args[index];

                if (!positionalOnly && argument.Equals("--", StringComparison.Ordinal))
                {
                    positionalOnly = true;
                    continue;
                }

                if (!positionalOnly && TryReadOptionValue(args, ref index, "--output", "-o", out var output))
                {
                    if (string.IsNullOrWhiteSpace(output))
                    {
                        error = "--output 不可為空。";
                        return false;
                    }

                    outputPath = output;
                    continue;
                }

                if (!positionalOnly && TryReadOptionValue(args, ref index, "--theme", null, out var themeValue))
                {
                    if (string.IsNullOrWhiteSpace(themeValue))
                    {
                        error = "--theme 不可為空。";
                        return false;
                    }

                    theme = themeValue;
                    continue;
                }

                if (!positionalOnly && (argument.Equals("--force", StringComparison.Ordinal) ||
                                        argument.Equals("-f", StringComparison.Ordinal)))
                {
                    force = true;
                    continue;
                }

                if (!positionalOnly && (argument.Equals("--quiet", StringComparison.Ordinal) ||
                                        argument.Equals("-q", StringComparison.Ordinal)))
                {
                    quiet = true;
                    continue;
                }

                if (!positionalOnly && argument.StartsWith('-'))
                {
                    error = $"未知選項：{argument}";
                    return false;
                }

                if (inputPath is not null)
                {
                    error = "只能指定一個輸入檔案。";
                    return false;
                }

                inputPath = argument;
            }
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            error = "缺少輸入檔案。";
            return false;
        }

        if (format == CliExportFormat.Docx && theme is not null)
        {
            error = "docx 匯出不支援 --theme。";
            return false;
        }

        options = new CliExportOptions(format, inputPath, outputPath, theme, force, quiet);
        return true;
    }

    private static bool TryParseFormat(string value, out CliExportFormat format)
    {
        switch (value.ToLowerInvariant())
        {
            case "docx":
                format = CliExportFormat.Docx;
                return true;

            case "html":
                format = CliExportFormat.Html;
                return true;

            case "pdf":
                format = CliExportFormat.Pdf;
                return true;

            default:
                format = default;
                return false;
        }
    }

    private static bool TryReadOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        string longName,
        string? shortName,
        out string? value)
    {
        var argument = args[index];

        if (argument.Equals(longName, StringComparison.OrdinalIgnoreCase) ||
            (shortName is not null && argument.Equals(shortName, StringComparison.OrdinalIgnoreCase)))
        {
            if (index + 1 >= args.Count)
            {
                throw new FormatException($"{longName} 缺少值。");
            }

            value = args[++index];
            return true;
        }

        var equalsPrefix = longName + "=";
        if (argument.StartsWith(equalsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = argument[equalsPrefix.Length..];
            return true;
        }

        value = null;
        return false;
    }
}
