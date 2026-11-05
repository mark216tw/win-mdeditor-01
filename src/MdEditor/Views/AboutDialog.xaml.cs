using System.Reflection;
using Microsoft.UI.Xaml.Controls;

namespace VibeCode.MdEditor.Views;

public sealed partial class AboutDialog : ContentDialog
{
    public AboutDialog()
    {
        InitializeComponent();

        var assembly = typeof(AboutDialog).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+')[0];
        var version = string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString(3) ?? "1.0.0"
            : informationalVersion;

        var fileVersion = assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version ?? assembly.GetName().Version?.ToString() ?? "1.0.0.0";

        VersionText.Text = $"版本 {version}（檔案版本 {fileVersion}）";
    }
}
