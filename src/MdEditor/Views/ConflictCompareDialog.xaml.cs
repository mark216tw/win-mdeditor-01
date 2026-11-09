using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VibeCode.MdEditor.Core.Services;
using VibeCode.MdEditor.Models;

namespace VibeCode.MdEditor.Views;

public sealed partial class ConflictCompareDialog : ContentDialog
{
    public ConflictCompareDialog(
        string documentTitle,
        string draftText,
        string diskText,
        ILineDiffService lineDiff)
    {
        InitializeComponent();

        Title = $"並排比較 - {documentTitle}";
        HeaderText.Text =
            "左側為未儲存草稿，右側為磁碟版本。紅色為僅草稿有的行，綠色為僅磁碟版本有的行。";

        var result = lineDiff.Compare(draftText, diskText);
        if (result.IsApproximate)
        {
            DiffList.Visibility = Visibility.Collapsed;
            ApproximatePanel.Visibility = Visibility.Visible;
            DraftTextBox.Text = draftText;
            DiskTextBox.Text = diskText;
            return;
        }

        var rows = new List<DiffRowViewModel>(result.Lines.Count);
        foreach (var line in result.Lines)
        {
            rows.Add(new DiffRowViewModel
            {
                LeftNumber = line.LeftNumber?.ToString() ?? string.Empty,
                LeftText = line.LeftText ?? string.Empty,
                LeftRemovedVisibility = line.Kind == DiffLineKind.Removed
                    ? Visibility.Visible
                    : Visibility.Collapsed,
                RightNumber = line.RightNumber?.ToString() ?? string.Empty,
                RightText = line.RightText ?? string.Empty,
                RightAddedVisibility = line.Kind == DiffLineKind.Added
                    ? Visibility.Visible
                    : Visibility.Collapsed,
            });
        }

        DiffList.ItemsSource = rows;
    }
}
