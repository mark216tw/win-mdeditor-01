namespace VibeCode.MdEditor.Core.Helpers;

public static class SupportedFileTypes
{
    public static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".markdown",
        ".mdown",
        ".mkd",
        ".txt",
    };

    public static bool IsSupported(string path)
    {
        return Extensions.Contains(Path.GetExtension(path));
    }
}
