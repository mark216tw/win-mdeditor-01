namespace VibeCode.MdEditor.Core.Models;

public sealed record RegistryValueReference(string KeyPath, string ValueName, bool RemoveKeyIfEmpty);
