using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VibeCode.MdEditor.Models;

namespace VibeCode.MdEditor.Views;

public sealed partial class PreferencesDialog : ContentDialog
{
    public PreferencesDialog(
        string theme,
        bool syncScrolling,
        bool wordWrap,
        double fontSize,
        string editorFontFamily,
        bool fileAssociationRegistered)
    {
        InitializeComponent();

        foreach (var definition in AppThemeCatalog.All)
        {
            ThemeComboBox.Items.Add(new ComboBoxItem
            {
                Content = definition.DisplayName,
                Tag = definition.Id,
            });
        }

        var selectedIndex = 0;
        for (var index = 0; index < ThemeComboBox.Items.Count; index++)
        {
            if (ThemeComboBox.Items[index] is ComboBoxItem { Tag: string id } &&
                string.Equals(id, theme, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = index;
                break;
            }
        }

        ThemeComboBox.SelectedIndex = selectedIndex;

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
    }

    public event EventHandler? OpenDefaultAppsRequested;

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

    public string SelectedTheme =>
        (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";

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

    private void OpenDefaultAppsButton_Click(object sender, RoutedEventArgs e)
    {
        FileAssociationToggle.IsOn = true;
        OpenDefaultAppsRequested?.Invoke(this, EventArgs.Empty);
    }
}
