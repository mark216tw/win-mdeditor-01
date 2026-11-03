using System.Text;

namespace VibeCode.MdEditor.Core.Helpers;

public static class LaunchArguments
{
    public static string? TryGetFilePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        return TryGetFilePath(Split(commandLine));
    }

    public static string? TryGetFilePath(IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            var candidate = argument.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith('-'))
            {
                continue;
            }

            if (!SupportedFileTypes.IsSupported(candidate))
            {
                continue;
            }

            return Path.GetFullPath(candidate);
        }

        return null;
    }

    private static IEnumerable<string> Split(string commandLine)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            arguments.Add(current.ToString());
        }

        return arguments;
    }
}
