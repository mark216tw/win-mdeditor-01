using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Core.Services;

public sealed class DocumentTranslationService : IDocumentTranslationService
{
    private readonly MarkdownTranslationExtractor _extractor;
    private readonly ITranslationService _translationService;

    public DocumentTranslationService(
        MarkdownTranslationExtractor extractor,
        ITranslationService translationService)
    {
        _extractor = extractor;
        _translationService = translationService;
    }

    public Task<bool> IsConfiguredAsync(
        TranslationSettings settings,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default)
    {
        return _translationService.IsConfiguredAsync(settings, apiKeyOverride, cancellationToken);
    }

    public async Task<DocumentTranslationResult> TranslateMarkdownAsync(
        string markdown,
        TranslationSettings settings,
        IProgress<TranslationProgress>? progress = null,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(settings);

        var plan = _extractor.CreatePlan(markdown);
        if (!plan.HasTranslatableContent)
        {
            return new DocumentTranslationResult(markdown, 0);
        }

        var batches = BuildBatches(plan.Units, settings.MaxCharactersPerRequest);
        var translations = new string[plan.Units.Count];
        var completed = 0;

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var segments = batch.Select(index => plan.Units[index].MaskedText).ToList();
            var results = await _translationService.TranslateAsync(
                    segments,
                    settings,
                    apiKeyOverride,
                    cancellationToken)
                .ConfigureAwait(false);

            if (results.Count != segments.Count)
            {
                throw new TranslationException(
                    $"翻譯結果數量不符：預期 {segments.Count} 段，實際 {results.Count} 段。");
            }

            for (var index = 0; index < results.Count; index++)
            {
                translations[batch[index]] = results[index];
            }

            completed += segments.Count;
            progress?.Report(new TranslationProgress(completed, plan.Units.Count));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var translatedText = _extractor.ApplyTranslations(plan, translations);
        return new DocumentTranslationResult(translatedText, plan.Units.Count);
    }

    private static List<List<int>> BuildBatches(
        IReadOnlyList<TranslationUnit> units,
        int maxCharactersPerRequest)
    {
        var limit = Math.Max(500, maxCharactersPerRequest);
        var batches = new List<List<int>>();
        var current = new List<int>();
        var currentLength = 0;

        for (var index = 0; index < units.Count; index++)
        {
            var length = units[index].MaskedText.Length;
            if (current.Count > 0 && currentLength + length > limit)
            {
                batches.Add(current);
                current = [];
                currentLength = 0;
            }

            current.Add(index);
            currentLength += length;
        }

        if (current.Count > 0)
        {
            batches.Add(current);
        }

        return batches;
    }
}
