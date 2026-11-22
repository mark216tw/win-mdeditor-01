using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Models;
using VibeCode.MdEditor.Core.Services;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class TranslationTests
{
    private readonly MarkdownTranslationExtractor _extractor = new();

    [Fact]
    public void CreatePlan_ExtractsOnlyNaturalLanguageBlocks()
    {
        var markdown =
            "# Title\n\n" +
            "```cs\nvar x = 1;\n```\n\n" +
            "The **bold** text with [link](https://example.com) and `code`.\n";

        var plan = _extractor.CreatePlan(markdown);
        var masked = string.Join("\n---\n", plan.Units.Select(unit => unit.MaskedText));

        Assert.Equal(2, plan.Units.Count);
        Assert.DoesNotContain("var x", masked);
        Assert.DoesNotContain("example.com", masked);
        Assert.DoesNotContain("`code`", masked);
        Assert.Contains("⟦", masked);
    }

    [Fact]
    public void ApplyTranslations_PreservesMarkupAndUrls()
    {
        var markdown = "**粗體** 與 [連結](https://example.com/a) 和 `code`\n";
        var plan = _extractor.CreatePlan(markdown);

        var translations = plan.Units
            .Select(unit => unit.MaskedText
                .Replace("粗體", "BOLD", StringComparison.Ordinal)
                .Replace("連結", "LINK", StringComparison.Ordinal)
                .Replace("與", "and", StringComparison.Ordinal)
                .Replace("和", "and", StringComparison.Ordinal))
            .ToList();

        var result = _extractor.ApplyTranslations(plan, translations);

        Assert.Equal("**BOLD** and [LINK](https://example.com/a) and `code`\n", result);
    }

    [Fact]
    public void ApplyTranslations_PreservesTableStructure()
    {
        var markdown = "| Name | Value |\n| --- | --- |\n| Alpha | One |\n";
        var plan = _extractor.CreatePlan(markdown);

        Assert.Equal(4, plan.Units.Count);

        var translations = plan.Units
            .Select(unit => unit.MaskedText
                .Replace("Name", "名稱", StringComparison.Ordinal)
                .Replace("Value", "數值", StringComparison.Ordinal)
                .Replace("Alpha", "阿法", StringComparison.Ordinal)
                .Replace("One", "一", StringComparison.Ordinal))
            .ToList();

        var result = _extractor.ApplyTranslations(plan, translations);

        Assert.Equal("| 名稱 | 數值 |\n| --- | --- |\n| 阿法 | 一 |\n", result);
    }

    [Fact]
    public void ApplyTranslations_LeavesReferenceLinkTextUntouched()
    {
        var markdown = "請看 [文件][doc] 說明。\n\n[doc]: https://example.com\n";
        var plan = _extractor.CreatePlan(markdown);

        var translations = plan.Units
            .Select(unit => unit.MaskedText
                .Replace("請看", "See", StringComparison.Ordinal)
                .Replace("說明", "docs", StringComparison.Ordinal))
            .ToList();

        var result = _extractor.ApplyTranslations(plan, translations);

        Assert.Equal("See [文件][doc] docs。\n\n[doc]: https://example.com\n", result);
    }

    [Fact]
    public void CreatePlan_KeepsFrontMatterUntouched()
    {
        var markdown = "---\ntitle: Test\ntags: [a]\n---\n\n# Hello\n";
        var plan = _extractor.CreatePlan(markdown);

        Assert.Single(plan.Units);
        Assert.DoesNotContain("title", plan.Units[0].MaskedText);

        var result = _extractor.ApplyTranslations(plan, ["你好"]);

        Assert.Equal("---\ntitle: Test\ntags: [a]\n---\n\n# 你好\n", result);
    }

    [Fact]
    public void ApplyTranslations_PreservesCrLf()
    {
        var markdown = "# Hello\r\n\r\nWorld\r\n";
        var plan = _extractor.CreatePlan(markdown);

        var result = _extractor.ApplyTranslations(plan, ["你好", "世界"]);

        Assert.Equal("# 你好\r\n\r\n世界\r\n", result);
    }

    [Fact]
    public void ApplyTranslations_ThrowsWhenPlaceholderIsMissing()
    {
        var markdown = "**粗體**\n";
        var plan = _extractor.CreatePlan(markdown);

        Assert.Throws<TranslationException>(() => _extractor.ApplyTranslations(plan, ["BOLD"]));
    }

    [Fact]
    public void Parse_ReadsOpenAiEnvelopeWithFencedJson()
    {
        var response = """
        {
          "choices": [
            {
              "message": {
                "content": "```json\n{\"translations\":[\"你好\",\"世界\"]}\n```"
              }
            }
          ]
        }
        """;

        var results = TranslationResponseParser.Parse(response, 2);

        Assert.Equal(["你好", "世界"], results);
    }

    [Fact]
    public void Parse_ReadsDirectTranslationObject()
    {
        var results = TranslationResponseParser.Parse("""{"translations":["A","B"]}""", 2);

        Assert.Equal(["A", "B"], results);
    }

    [Fact]
    public void Parse_ExtractsJsonFromSurroundingText()
    {
        var response = """
        {
          "choices": [
            { "message": { "content": "Here is the result:\n{\"translations\":[\"A\",\"B\"]}\nThanks" } }
          ]
        }
        """;

        var results = TranslationResponseParser.Parse(response, 2);

        Assert.Equal(["A", "B"], results);
    }

    [Fact]
    public void Parse_ThrowsWhenCountMismatches()
    {
        Assert.Throws<TranslationException>(
            () => TranslationResponseParser.Parse("""{"translations":["A"]}""", 2));
    }

    [Fact]
    public async Task DocumentTranslationService_ReportsProgressAndAppliesResults()
    {
        var provider = new FakeTranslationService();
        var service = new DocumentTranslationService(_extractor, provider);
        var settings = new TranslationSettings
        {
            Model = "test-model",
            TargetLanguage = "en",
        };

        var progress = new List<TranslationProgress>();
        var result = await service.TranslateMarkdownAsync(
            "# 標題\n\n內文\n",
            settings,
            new Progress<TranslationProgress>(progress.Add));

        Assert.Equal(2, result.UnitCount);
        Assert.Equal("# TITLE\n\nTEXT\n", result.Text);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task DocumentTranslationService_ReturnsOriginalWhenNothingToTranslate()
    {
        var provider = new FakeTranslationService();
        var service = new DocumentTranslationService(_extractor, provider);
        var settings = new TranslationSettings { Model = "test-model" };

        var markdown = "```cs\nvar x = 1;\n```\n";
        var result = await service.TranslateMarkdownAsync(markdown, settings);

        Assert.Equal(0, result.UnitCount);
        Assert.Equal(markdown, result.Text);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Settings_NormalizesTranslationValues()
    {
        using var temp = new TempDirectory();
        var settingsPath = Path.Combine(temp.DirectoryPath, "settings.json");
        await File.WriteAllTextAsync(
            settingsPath,
            """{"Translation":{"Endpoint":"http://example.com","Model":"  gpt-test  ","SourceLanguage":"xx","TargetLanguage":"yy","MaxCharactersPerRequest":1,"Temperature":5,"RequestTimeoutSeconds":1}}""");

        var settings = new SettingsService(temp.DirectoryPath);
        await settings.LoadAsync();

        Assert.Equal(TranslationSettings.DefaultEndpoint, settings.Settings.Translation.Endpoint);
        Assert.Equal("gpt-test", settings.Settings.Translation.Model);
        Assert.Equal(TranslationSettings.AutoDetectSourceLanguage, settings.Settings.Translation.SourceLanguage);
        Assert.Equal(TranslationSettings.DefaultTargetLanguage, settings.Settings.Translation.TargetLanguage);
        Assert.Equal(500, settings.Settings.Translation.MaxCharactersPerRequest);
        Assert.Equal(1, settings.Settings.Translation.Temperature);
        Assert.Equal(20, settings.Settings.Translation.RequestTimeoutSeconds);
        Assert.True(TranslationLanguageCatalog.IsSupportedTarget("ja"));
    }

    private sealed class FakeTranslationService : ITranslationService
    {
        public int Calls { get; private set; }

        public Task<bool> IsConfiguredAsync(
            TranslationSettings settings,
            string? apiKeyOverride = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<string>> TranslateAsync(
            IReadOnlyList<string> segments,
            TranslationSettings settings,
            string? apiKeyOverride = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            IReadOnlyList<string> results = segments
                .Select(segment => segment
                    .Replace("標題", "TITLE", StringComparison.Ordinal)
                    .Replace("內文", "TEXT", StringComparison.Ordinal))
                .ToList();
            return Task.FromResult(results);
        }
    }
}
