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

    public string SelectedTheme =>
        (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";

    public bool SyncScrollingEnabled => SyncScrollingToggle.IsOn;

    public bool WordWrapEnabled => WordWrapToggle.IsOn;

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
