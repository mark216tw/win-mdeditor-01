namespace VibeCode.MdEditor.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        DirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "MdEditorTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public string Combine(string fileName)
    {
        return Path.Combine(DirectoryPath, fileName);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (Exception)
        {
        }
    }
}
