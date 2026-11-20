using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VibeCode.MdEditor.Models;

namespace VibeCode.MdEditor.Views;

public sealed partial class PreferencesDialog : ContentDialog
{
    private readonly bool _isInitializing;

    public PreferencesDialog(
        string displayMode,
        string themeColor,
        bool syncScrolling,
        bool wordWrap,
        double fontSize,
        string editorFontFamily,
        bool fileAssociationRegistered)
    {
        _isInitializing = true;
        InitializeComponent();

        foreach (var option in AppThemeCatalog.DisplayModes)
        {
            DisplayModeComboBox.Items.Add(new ComboBoxItem
            {
                Content = option.DisplayName,
                Tag = option.Id,
            });
        }

        DisplayModeComboBox.SelectedIndex = FindComboBoxIndex(DisplayModeComboBox, displayMode);

        foreach (var option in AppThemeCatalog.ThemeColors)
        {
            ThemeColorComboBox.Items.Add(new ComboBoxItem
            {
                Content = option.DisplayName,
                Tag = option.Id,
            });
        }

        ThemeColorComboBox.SelectedIndex = FindComboBoxIndex(ThemeColorComboBox, themeColor);

        foreach (var option in EditorFontCatalog.All)
        {
            FontFamilyComboBox.Items.Add(new ComboBoxItem
            {
                Content = option.DisplayName,
                Tag = option.FamilyName,
            });
        }

        var selectedFont = string.IsNullOrWhiteSpace(editorFontFamily)
            ? "Cascadia Mono"
            : editorFontFamily.Trim();
        var fontIndex = FindFontFamilyIndex(selectedFont);
        if (fontIndex < 0)
        {
            FontFamilyComboBox.Items.Add(new ComboBoxItem
            {
                Content = selectedFont,
                Tag = selectedFont,
            });
            fontIndex = FontFamilyComboBox.Items.Count - 1;
        }

        FontFamilyComboBox.SelectedIndex = fontIndex;

        SyncScrollingToggle.IsOn = syncScrolling;
        WordWrapToggle.IsOn = wordWrap;

        var roundedSize = (int)Math.Clamp(Math.Round(fontSize), 10, 28);
        for (var size = 10; size <= 28; size++)
        {
            FontSizeComboBox.Items.Add(new ComboBoxItem
            {
                Content = $"{size} pt",
                Tag = size,
            });
        }

        FontSizeComboBox.SelectedIndex = roundedSize - 10;
        FileAssociationToggle.IsOn = fileAssociationRegistered;
        _isInitializing = false;
    }

    public event EventHandler? OpenDefaultAppsRequested;

    public event EventHandler? ThemeSelectionChanged;

    public string SelectedDisplayMode =>
        (DisplayModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";

    public string SelectedThemeColor =>
        (ThemeColorComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Sepia";

    public bool SyncScrollingEnabled => SyncScrollingToggle.IsOn;

    public bool WordWrapEnabled => WordWrapToggle.IsOn;

    public string SelectedFontFamily =>
        (FontFamilyComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Cascadia Mono";

    public double SelectedFontSize =>
        (FontSizeComboBox.SelectedItem as ComboBoxItem)?.Tag is int size ? size : 14;

    public bool FileAssociationEnabled => FileAssociationToggle.IsOn;

    public void ShowAssociationError(string message)
    {
        AssociationInfoBar.Message = message;
        AssociationInfoBar.IsOpen = true;
    }

    private static int FindComboBoxIndex(ComboBox comboBox, string value)
    {
        for (var index = 0; index < comboBox.Items.Count; index++)
        {
            if (comboBox.Items[index] is ComboBoxItem { Tag: string id } &&
                string.Equals(id, value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    private int FindFontFamilyIndex(string family)
    {
        for (var index = 0; index < FontFamilyComboBox.Items.Count; index++)
        {
            if (FontFamilyComboBox.Items[index] is ComboBoxItem { Tag: string value } &&
                string.Equals(value, family, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        ThemeSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OpenDefaultAppsButton_Click(object sender, RoutedEventArgs e)
    {
        FileAssociationToggle.IsOn = true;
        OpenDefaultAppsRequested?.Invoke(this, EventArgs.Empty);
    }
}
