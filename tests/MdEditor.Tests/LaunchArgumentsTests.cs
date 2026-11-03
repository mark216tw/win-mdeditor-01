using VibeCode.MdEditor.Core.Helpers;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class LaunchArgumentsTests
{
    [Fact]
    public void IgnoresExecutableAndReturnsMarkdownFile()
    {
        var result = LaunchArguments.TryGetFilePath(
            "\"C:\\Program Files\\MdEditor\\MdEditor.exe\" \"D:\\docs\\note.md\"");

        Assert.Equal(Path.GetFullPath(@"D:\docs\note.md"), result);
    }

    [Fact]
    public void ReturnsNullWhenOnlyExecutableIsGiven()
    {
        var result = LaunchArguments.TryGetFilePath(
            new[] { @"C:\Program Files\MdEditor\MdEditor.exe" });

        Assert.Null(result);
    }

    [Fact]
    public void AcceptsTextFile()
    {
        var result = LaunchArguments.TryGetFilePath(
            new[] { @"D:\docs\readme.txt" });

        Assert.Equal(Path.GetFullPath(@"D:\docs\readme.txt"), result);
    }
}
