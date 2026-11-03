namespace VibeCode.MdEditor.Services;

public interface IFileAssociationService
{
    bool IsRegistered { get; }

    string ExecutablePath { get; }

    void Register();

    void Unregister();

    void OpenDefaultAppsSettings();
}
