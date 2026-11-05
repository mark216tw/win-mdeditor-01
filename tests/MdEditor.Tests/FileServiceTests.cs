using System.Text;
using VibeCode.MdEditor.Core.Services;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class FileServiceTests
{
    private readonly FileService _service = new();

    [Fact]
    public async Task LoadAndSave_PreservesUtf8BomAndCrLf()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("sample.md");
        var preamble = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = Encoding.UTF8.GetBytes("Hello\r\n世界\r\n");
        await File.WriteAllBytesAsync(path, [.. preamble, .. content]);

        var loaded = await _service.LoadAsync(path);

        Assert.Equal("Hello\n世界\n", loaded.Text);
        Assert.Equal("UTF-8", loaded.EncodingName);
        Assert.Equal("CRLF", loaded.LineEnding);
        Assert.True(loaded.HasBom);
        Assert.False(string.IsNullOrWhiteSpace(loaded.Hash));

        await _service.SaveAsync(
            path,
            loaded.Text,
            loaded.EncodingName,
            loaded.CodePage,
            loaded.HasBom,
            loaded.LineEnding);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.True(bytes.Take(3).SequenceEqual(preamble));
        var text = Encoding.UTF8.GetString(bytes.Skip(3).ToArray());
        Assert.Equal("Hello\r\n世界\r\n", text);
    }

    [Fact]
    public async Task Save_WritesUtf8WithoutBom_WhenSourceHadNoBom()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("plain.md");
        await File.WriteAllBytesAsync(path, new UTF8Encoding(false).GetBytes("one\ntwo\n"));

        var loaded = await _service.LoadAsync(path);
        Assert.False(loaded.HasBom);
        Assert.Equal("LF", loaded.LineEnding);

        await _service.SaveAsync(
            path,
            loaded.Text,
            loaded.EncodingName,
            loaded.CodePage,
            loaded.HasBom,
            loaded.LineEnding);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Equal("one\ntwo\n", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task ComputeStamp_ChangesWhenFileChanges()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("stamp.md");
        await File.WriteAllTextAsync(path, "first");

        var first = await _service.ComputeStampAsync(path);
        await File.WriteAllTextAsync(path, "second");
        var second = await _service.ComputeStampAsync(path);

        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public async Task SavePastedImageAsync_WritesUniqueFilesUnderImagesDirectory()
    {
        using var temp = new TempDirectory();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var firstRelative = await _service.SavePastedImageAsync(temp.DirectoryPath, bytes, "image/png");
        var secondRelative = await _service.SavePastedImageAsync(temp.DirectoryPath, bytes, "image/png");
        var thirdRelative = await _service.SavePastedImageAsync(temp.DirectoryPath, bytes, "image/jpeg; charset=utf-8");

        Assert.StartsWith("images/paste-", firstRelative, StringComparison.Ordinal);
        Assert.EndsWith(".png", firstRelative, StringComparison.Ordinal);
        Assert.StartsWith("images/paste-", secondRelative, StringComparison.Ordinal);
        Assert.EndsWith(".png", secondRelative, StringComparison.Ordinal);
        Assert.NotEqual(firstRelative, secondRelative);
        Assert.EndsWith(".jpg", thirdRelative, StringComparison.Ordinal);

        var firstFullPath = Path.Combine(temp.DirectoryPath, firstRelative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(firstFullPath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(firstFullPath));
    }

    [Fact]
    public async Task LoadAsync_RejectsFileExceedingMaxSupportedBytes()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("huge.md");
        await using (var stream = File.Create(path))
        {
            stream.SetLength(FileService.MaxSupportedFileBytes + 1);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.LoadAsync(path));
    }

    [Fact]
    public async Task SavePastedImageAsync_RejectsOversizedPayload()
    {
        using var temp = new TempDirectory();
        var oversized = new byte[FileService.MaxPastedImageBytes + 1];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SavePastedImageAsync(temp.DirectoryPath, oversized, "image/png"));
    }
}
