using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;

namespace VibeCode.MdEditor.Views;

public sealed partial class AboutDialog : ContentDialog
{
    public AboutDialog()
    {
        InitializeComponent();

        var assembly = typeof(AboutDialog).Assembly;
        var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
        var version = string.IsNullOrWhiteSpace(fileVersion)
            ? assembly.GetName().Version?.ToString(3) ?? "0.0.0"
            : fileVersion;

        VersionText.Text = $"版本 {version}";
    }
}
