using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Core.Services;

public sealed record TranslationProgress(int CompletedSegments, int TotalSegments);

public interface ITranslationService
{
    Task<bool> IsConfiguredAsync(
        TranslationSettings settings,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> segments,
        TranslationSettings settings,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentTranslationResult(string Text, int UnitCount);

public interface IDocumentTranslationService
{
    Task<bool> IsConfiguredAsync(
        TranslationSettings settings,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default);

    Task<DocumentTranslationResult> TranslateMarkdownAsync(
        string markdown,
        TranslationSettings settings,
        IProgress<TranslationProgress>? progress = null,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default);
}
