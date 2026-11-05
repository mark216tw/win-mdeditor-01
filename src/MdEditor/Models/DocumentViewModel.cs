using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Models;

public sealed partial class DocumentViewModel : ObservableObject
{
    public DocumentViewModel()
    {
        Text = string.Empty;
        Title = "未命名";
        EncodingName = "UTF-8";
        CodePage = 65001;
        LineEnding = "LF";
        LastKnownHash = string.Empty;
        ExternalChangeMessage = string.Empty;
    }

    public DocumentViewModel(string title, string? id = null)
        : this()
    {
        Title = title;

        if (!string.IsNullOrWhiteSpace(id))
        {
            Id = id;
        }
    }

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial string? FilePath { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial string EncodingName { get; set; }

    [ObservableProperty]
    public partial int CodePage { get; set; }

    [ObservableProperty]
    public partial bool HasBom { get; set; }

    [ObservableProperty]
    public partial string LineEnding { get; set; }

    [ObservableProperty]
    public partial string LastKnownHash { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset LastWriteTimeUtc { get; set; }

    [ObservableProperty]
    public partial long LastKnownLength { get; set; }

    [ObservableProperty]
    public partial bool IsExternalChange { get; set; }

    [ObservableProperty]
    public partial string ExternalChangeMessage { get; set; }

    [ObservableProperty]
    public partial bool IsMissing { get; set; }

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public long Revision { get; set; } = 1;

    public string FileDirectory => FilePath is null
        ? string.Empty
        : Path.GetDirectoryName(FilePath) ?? string.Empty;

    public int CharCount => Text.Length;

    public int LineCount => string.IsNullOrEmpty(Text) ? 1 : Text.Count(c => c == '\n') + 1;

    public int WordCount => CountWords(Text);

    public Visibility DirtyVisibility => IsDirty ? Visibility.Visible : Visibility.Collapsed;

    public void Load(LoadedDocument document)
    {
        Text = document.Text;
        EncodingName = document.EncodingName;
        CodePage = document.CodePage;
        HasBom = document.HasBom;
        LineEnding = document.LineEnding;
        LastKnownHash = document.Hash;
        LastWriteTimeUtc = document.LastWriteTimeUtc;
        LastKnownLength = document.Length;
        IsExternalChange = false;
        ExternalChangeMessage = string.Empty;
        IsMissing = false;
        IsDirty = false;
    }

    partial void OnFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(FileDirectory));
    }

    partial void OnIsDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(DirtyVisibility));
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var count = 0;
        var inAsciiWord = false;

        foreach (var character in text)
        {
            if (character >= 0x2E80)
            {
                count++;
                inAsciiWord = false;
            }
            else if (char.IsLetterOrDigit(character))
            {
                if (!inAsciiWord)
                {
                    count++;
                    inAsciiWord = true;
                }
            }
            else
            {
                inAsciiWord = false;
            }
        }

        return count;
    }
}
