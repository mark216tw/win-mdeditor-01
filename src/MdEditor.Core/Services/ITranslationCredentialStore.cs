namespace VibeCode.MdEditor.Core.Services;

public interface ITranslationCredentialStore
{
    Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default);

    Task SetApiKeyAsync(string? apiKey, CancellationToken cancellationToken = default);
}
