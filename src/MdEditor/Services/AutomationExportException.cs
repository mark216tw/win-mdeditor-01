namespace VibeCode.MdEditor.Services;

public sealed class AutomationExportException(int exitCode, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int ExitCode { get; } = exitCode;
}
