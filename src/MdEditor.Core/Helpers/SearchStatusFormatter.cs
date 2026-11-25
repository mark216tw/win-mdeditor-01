using System.Text.RegularExpressions;

namespace VibeCode.MdEditor.Core.Helpers;

public static class SearchStatusFormatter
{
    public static string FormatMatchStatus(
        string? query,
        int currentIndex,
        int totalCount,
        bool isValidRegex)
    {
        if (string.IsNullOrEmpty(query))
        {
            return string.Empty;
        }

        if (!isValidRegex)
        {
            return "規則運算式無效";
        }

        if (totalCount <= 0)
        {
            return "無相符項目";
        }

        if (currentIndex <= 0)
        {
            return $"{totalCount:N0} 筆相符";
        }

        return $"{currentIndex:N0} / {totalCount:N0}";
    }

    public static string NormalizeInitialQuery(string? selectedText)
    {
        if (string.IsNullOrEmpty(selectedText))
        {
            return string.Empty;
        }

        if (selectedText.Contains('\r') || selectedText.Contains('\n'))
        {
            return string.Empty;
        }

        return selectedText.Length > 200 ? string.Empty : selectedText;
    }

    public static bool IsValidRegex(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return true;
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
