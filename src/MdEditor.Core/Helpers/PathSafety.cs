namespace VibeCode.MdEditor.Core.Helpers;

public static class PathSafety
{
    public static bool IsPathWithinDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory);
        var trimmedDirectory = Path.TrimEndingDirectorySeparator(fullDirectory);
        var prefix = Path.EndsInDirectorySeparator(trimmedDirectory)
            ? trimmedDirectory
            : trimmedDirectory + Path.DirectorySeparatorChar;

        return string.Equals(fullPath, trimmedDirectory, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryResolveRelativePathWithinDirectory(string? baseDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var candidate = relativePath.Trim();
        if (candidate.StartsWith('/') || candidate.StartsWith('\\'))
        {
            return null;
        }

        string unescaped;
        try
        {
            unescaped = Uri.UnescapeDataString(candidate);
        }
        catch (UriFormatException)
        {
            unescaped = candidate;
        }

        if (string.IsNullOrWhiteSpace(unescaped) ||
            unescaped.StartsWith('/') ||
            unescaped.StartsWith('\\') ||
            Path.IsPathRooted(unescaped))
        {
            return null;
        }

        try
        {
            var fullDirectory = Path.GetFullPath(baseDirectory);
            var combined = Path.GetFullPath(Path.Combine(
                fullDirectory,
                unescaped.Replace('/', Path.DirectorySeparatorChar)));

            return IsPathWithinDirectory(combined, fullDirectory) ? combined : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
