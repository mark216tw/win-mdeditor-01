using Microsoft.UI.Xaml;
using VibeCode.MdEditor.Models;

namespace VibeCode.MdEditor.Services;

public static class ThemeResourceManager
{
    private static ResourceDictionary? _currentDictionary;
    private static string? _currentUri;

    public static void Apply(AppThemeDefinition theme)
    {
        if (string.Equals(_currentUri, theme.ResourceUri, StringComparison.Ordinal))
        {
            return;
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;

        if (_currentDictionary is not null)
        {
            dictionaries.Remove(_currentDictionary);
            _currentDictionary = null;
            _currentUri = null;
        }

        if (theme.ResourceUri is null)
        {
            return;
        }

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(theme.ResourceUri),
        };

        dictionaries.Add(dictionary);
        _currentDictionary = dictionary;
        _currentUri = theme.ResourceUri;
    }
}
