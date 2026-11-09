using Microsoft.UI.Xaml.Controls;
using VibeCode.MdEditor.Models;

namespace VibeCode.MdEditor.Views;

public sealed partial class ConflictResolutionDialog : ContentDialog
{
    public ConflictResolutionDialog(string documentTitle, DocumentConflict conflict)
    {
        InitializeComponent();

        SummaryText.Text = $"「{documentTitle}」的未儲存草稿與磁碟上的版本不同。";
        DetailText.Text =
            $"磁碟版本最後修改：{conflict.DiskLastWriteTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n" +
            $"草稿 {conflict.DraftText.Length:N0} 字元 · 磁碟版本 {conflict.DiskText.Length:N0} 字元";
    }

    public ConflictResolution Choice { get; private set; } = ConflictResolution.None;

    private void KeepDraft_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Choice = ConflictResolution.KeepDraft;
        Hide();
    }

    private void LoadDisk_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Choice = ConflictResolution.LoadDisk;
        Hide();
    }

    private void Compare_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Choice = ConflictResolution.Compare;
        Hide();
    }
}
