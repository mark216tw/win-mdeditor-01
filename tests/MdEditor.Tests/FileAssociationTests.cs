using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Models;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class FileAssociationTests
{
    private const string ExecutablePath = @"C:\Program Files\MdEditor\MdEditor.exe";

    [Fact]
    public void RegistrationPlanUsesQuotedExecutableAndPlaceholder()
    {
        var plan = FileAssociationRegistration.CreateRegistrationPlan(ExecutablePath);

        var command = Find(plan, FileAssociationRegistration.CommandPath, null);
        Assert.Equal($"\"{ExecutablePath}\" \"%1\"", command.Value);

        var icon = Find(plan, FileAssociationRegistration.ProgIdPath, "DefaultIcon");
        Assert.Equal($"\"{ExecutablePath}\",0", icon.Value);
    }

    [Fact]
    public void RegistrationPlanRegistersEveryExtension()
    {
        Assert.Equal([".md", ".markdown", ".mdown", ".mkd"], FileAssociationRegistration.Extensions);

        var plan = FileAssociationRegistration.CreateRegistrationPlan(ExecutablePath);

        foreach (var extension in FileAssociationRegistration.Extensions)
        {
            var extensionPath = $@"Software\Classes\{extension}";

            var contentType = Find(plan, extensionPath, "Content Type");
            Assert.Equal(FileAssociationRegistration.ContentType, contentType.Value);

            var perceivedType = Find(plan, extensionPath, "PerceivedType");
            Assert.Equal(FileAssociationRegistration.PerceivedType, perceivedType.Value);

            var openWith = Find(plan, $@"{extensionPath}\OpenWithProgids", FileAssociationRegistration.ProgId);
            Assert.Equal(string.Empty, openWith.Value);

            var supportedType = Find(
                plan,
                $@"{FileAssociationRegistration.ApplicationPath}\SupportedTypes",
                extension);
            Assert.Equal(string.Empty, supportedType.Value);

            var fileAssociation = Find(
                plan,
                $@"{FileAssociationRegistration.CapabilitiesPath}\FileAssociations",
                extension);
            Assert.Equal(FileAssociationRegistration.ProgId, fileAssociation.Value);
        }
    }

    [Fact]
    public void RegistrationPlanRegistersApplicationForDefaultApps()
    {
        var plan = FileAssociationRegistration.CreateRegistrationPlan(ExecutablePath);

        var registered = Find(
            plan,
            FileAssociationRegistration.RegisteredApplicationsPath,
            FileAssociationRegistration.RegisteredAppName);
        Assert.Equal(FileAssociationRegistration.CapabilitiesPath, registered.Value);

        var capabilities = Find(
            plan,
            FileAssociationRegistration.CapabilitiesPath,
            "ApplicationName");
        Assert.Equal(FileAssociationRegistration.ApplicationName, capabilities.Value);

        var applicationCommand = Find(
            plan,
            $@"{FileAssociationRegistration.ApplicationPath}\shell\open\command",
            null);
        Assert.Equal($"\"{ExecutablePath}\" \"%1\"", applicationCommand.Value);
    }

    [Fact]
    public void RemovalPlanCoversRegisteredExtensionsAndApplication()
    {
        var keys = FileAssociationRegistration.CreateKeyRemovalPlan();
        var values = FileAssociationRegistration.CreateValueRemovalPlan();

        Assert.Contains(FileAssociationRegistration.ProgIdPath, keys);
        Assert.Contains(FileAssociationRegistration.ApplicationPath, keys);
        Assert.Contains(
            values,
            value => value.KeyPath == FileAssociationRegistration.RegisteredApplicationsPath &&
                     value.ValueName == FileAssociationRegistration.RegisteredAppName);

        foreach (var extension in FileAssociationRegistration.Extensions)
        {
            Assert.Contains(
                values,
                value => value.KeyPath == $@"Software\Classes\{extension}\OpenWithProgids" &&
                         value.ValueName == FileAssociationRegistration.ProgId &&
                         value.RemoveKeyIfEmpty);
        }
    }

    [Fact]
    public void RemovalPlanDeletesEveryRegisteredKeyPath()
    {
        var registrationPlan = FileAssociationRegistration.CreateRegistrationPlan(ExecutablePath);
        var removalKeys = FileAssociationRegistration.CreateKeyRemovalPlan();

        var managedKeys = new[]
        {
            FileAssociationRegistration.ProgIdPath,
            FileAssociationRegistration.ApplicationPath,
            FileAssociationRegistration.CapabilitiesPath,
        };

        foreach (var key in managedKeys)
        {
            var covered = removalKeys.Any(removedKey =>
                string.Equals(removedKey, key, StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith(removedKey + @"\", StringComparison.OrdinalIgnoreCase));

            Assert.True(covered, $"移除計畫未涵蓋機碼：{key}");
            Assert.Contains(registrationPlan, entry =>
                string.Equals(entry.KeyPath, key, StringComparison.OrdinalIgnoreCase) ||
                entry.KeyPath.StartsWith(key + @"\", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void BlankExecutablePathIsRejected()
    {
        Assert.Throws<ArgumentException>(() => FileAssociationRegistration.BuildOpenCommand(" "));
        Assert.Throws<ArgumentException>(() => FileAssociationRegistration.CreateRegistrationPlan(string.Empty));
    }

    private static RegistryEntry Find(
        IReadOnlyList<RegistryEntry> plan,
        string keyPath,
        string? valueName)
    {
        return Assert.Single(plan, entry =>
            string.Equals(entry.KeyPath, keyPath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.ValueName, valueName, StringComparison.OrdinalIgnoreCase));
    }
}
