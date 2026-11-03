using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Core.Helpers;

public static class FileAssociationRegistration
{
    public const string ProgId = "VibeCode.MdEditor.Markdown";
    public const string RegisteredAppName = "MdEditor";
    public const string ExecutableName = "MdEditor.exe";
    public const string DisplayName = "Markdown 文件";
    public const string ApplicationName = "Markdown 編輯器";
    public const string ApplicationDescription = "Markdown 文件編輯器";
    public const string CompanyName = "VibeCode";
    public const string ContentType = "text/markdown";
    public const string PerceivedType = "text";
    public const string CapabilitiesPath = @"Software\VibeCode\MdEditor\Capabilities";
    public const string RegisteredApplicationsPath = @"Software\RegisteredApplications";

    public static readonly IReadOnlyList<string> Extensions = [".md", ".markdown"];

    private const string ClassesPath = @"Software\Classes";

    public static string ProgIdPath => Combine(ClassesPath, ProgId);

    public static string ApplicationPath => Combine(ClassesPath, Combine("Applications", ExecutableName));

    public static string CommandPath => Combine(ProgIdPath, @"shell\open\command");

    public static string BuildOpenCommand(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{executablePath}\" \"%1\"";
    }

    public static string BuildIconLocation(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{executablePath}\",0";
    }

    public static IReadOnlyList<RegistryEntry> CreateRegistrationPlan(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var command = BuildOpenCommand(executablePath);
        var icon = BuildIconLocation(executablePath);
        var openVerbText = $"使用 {ApplicationName} 開啟";

        var entries = new List<RegistryEntry>
        {
            new(ProgIdPath, null, DisplayName),
            new(ProgIdPath, "FriendlyTypeName", DisplayName),
            new(ProgIdPath, "DefaultIcon", icon),
            new(Combine(ProgIdPath, "Application"), "ApplicationName", ApplicationName),
            new(Combine(ProgIdPath, "Application"), "ApplicationIcon", icon),
            new(Combine(ProgIdPath, "Application"), "ApplicationCompany", CompanyName),
            new(Combine(ProgIdPath, @"shell\open"), null, openVerbText),
            new(CommandPath, null, command),
            new(ApplicationPath, "ApplicationName", ApplicationName),
            new(ApplicationPath, "ApplicationIcon", icon),
            new(Combine(ApplicationPath, @"shell\open"), null, openVerbText),
            new(Combine(ApplicationPath, @"shell\open\command"), null, command),
            new(RegisteredApplicationsPath, RegisteredAppName, CapabilitiesPath),
            new(CapabilitiesPath, "ApplicationName", ApplicationName),
            new(CapabilitiesPath, "ApplicationDescription", ApplicationDescription),
            new(CapabilitiesPath, "ApplicationIcon", icon),
        };

        foreach (var extension in Extensions)
        {
            var extensionPath = Combine(ClassesPath, extension);

            entries.Add(new RegistryEntry(extensionPath, "Content Type", ContentType));
            entries.Add(new RegistryEntry(extensionPath, "PerceivedType", PerceivedType));
            entries.Add(new RegistryEntry(
                Combine(extensionPath, "OpenWithProgids"),
                ProgId,
                string.Empty));
            entries.Add(new RegistryEntry(
                Combine(ApplicationPath, "SupportedTypes"),
                extension,
                string.Empty));
            entries.Add(new RegistryEntry(
                Combine(CapabilitiesPath, "FileAssociations"),
                extension,
                ProgId));
        }

        return entries;
    }

    public static IReadOnlyList<string> CreateKeyRemovalPlan()
    {
        return
        [
            ProgIdPath,
            ApplicationPath,
            @"Software\VibeCode\MdEditor",
        ];
    }

    public static IReadOnlyList<RegistryValueReference> CreateValueRemovalPlan()
    {
        var values = new List<RegistryValueReference>
        {
            new(RegisteredApplicationsPath, RegisteredAppName, RemoveKeyIfEmpty: false),
        };

        foreach (var extension in Extensions)
        {
            values.Add(new RegistryValueReference(
                Combine(Combine(ClassesPath, extension), "OpenWithProgids"),
                ProgId,
                RemoveKeyIfEmpty: true));
        }

        return values;
    }

    private static string Combine(string left, string right)
    {
        return $@"{left}\{right}";
    }
}
