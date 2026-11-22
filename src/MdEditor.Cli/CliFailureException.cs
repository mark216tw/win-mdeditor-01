namespace VibeCode.MdEditor.Cli;

internal sealed class CliFailureException(int exitCode, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int ExitCode { get; } = exitCode;
}
