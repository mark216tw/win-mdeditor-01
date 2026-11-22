using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace VibeCode.MdEditor.Core.Services;

public sealed record TranslationUnit(int Start, int End, string MaskedText, IReadOnlyList<string> Gaps);

public sealed class MarkdownTranslationPlan
{
    public MarkdownTranslationPlan(string sourceText, IReadOnlyList<TranslationUnit> units)
    {
        SourceText = sourceText;
        Units = units;
    }

    public string SourceText { get; }

    public IReadOnlyList<TranslationUnit> Units { get; }

    public bool HasTranslatableContent => Units.Count > 0;
}

public sealed class MarkdownTranslationExtractor
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .UseAdvancedExtensions()
        .Build();

    private static readonly Regex PlaceholderPattern = new("⟦\\s*(\\d+)\\s*⟧", RegexOptions.Compiled);

    public MarkdownTranslationPlan CreatePlan(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var prefixLength = FindProtectedPrefixLength(markdown);
        var body = markdown[prefixLength..];
        if (body.Length == 0)
        {
            return new MarkdownTranslationPlan(markdown, []);
        }

        var document = Markdown.Parse(body, Pipeline);
        var units = new List<TranslationUnit>();

        foreach (var leaf in document.Descendants<LeafBlock>())
        {
            if (leaf is CodeBlock or HtmlBlock || leaf.Inline is null)
            {
                continue;
            }

            var inline = leaf.Inline;
            if (inline.Span.Length <= 0)
            {
                continue;
            }

            var literals = inline.Descendants<LiteralInline>()
                .Where(literal => !IsInsideReferenceLink(literal))
                .OrderBy(literal => literal.Span.Start)
                .ToList();

            if (literals.Count == 0)
            {
                continue;
            }

            var unit = BuildUnit(body, prefixLength, inline.Span, literals);
            if (unit is not null)
            {
                units.Add(unit);
            }
        }

        units.Sort((left, right) => left.Start.CompareTo(right.Start));
        return new MarkdownTranslationPlan(markdown, units);
    }

    public string ApplyTranslations(MarkdownTranslationPlan plan, IReadOnlyList<string> translations)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(translations);

        if (translations.Count != plan.Units.Count)
        {
            throw new TranslationException("翻譯結果的數量與原文段落不一致。");
        }

        var builder = new StringBuilder(plan.SourceText.Length + 32);
        var cursor = 0;

        for (var index = 0; index < plan.Units.Count; index++)
        {
            var unit = plan.Units[index];
            if (unit.Start < cursor || unit.End < unit.Start || unit.End >= plan.SourceText.Length)
            {
                throw new TranslationException("翻譯範圍無效，無法組回文件。");
            }

            builder.Append(plan.SourceText, cursor, unit.Start - cursor);
            builder.Append(ReassembleUnit(unit, translations[index]));
            cursor = unit.End + 1;
        }

        builder.Append(plan.SourceText, cursor, plan.SourceText.Length - cursor);
        return builder.ToString();
    }

    private static TranslationUnit? BuildUnit(
        string body,
        int offset,
        SourceSpan span,
        IReadOnlyList<LiteralInline> literals)
    {
        var start = span.Start;
        var end = span.End;

        if (start < 0 || end < start || end >= body.Length)
        {
            return null;
        }

        var masked = new StringBuilder();
        var gaps = new List<string>();
        var cursor = start;

        foreach (var literal in literals)
        {
            var literalStart = literal.Span.Start;
            var literalEnd = literal.Span.End;

            if (literalStart < cursor ||
                literalEnd < literalStart ||
                literalStart < start ||
                literalEnd > end)
            {
                continue;
            }

            if (literalStart > cursor)
            {
                AppendGap(masked, gaps, body[cursor..literalStart]);
            }

            masked.Append(body, literalStart, literalEnd - literalStart + 1);
            cursor = literalEnd + 1;
        }

        if (cursor <= end)
        {
            AppendGap(masked, gaps, body[cursor..(end + 1)]);
        }

        if (masked.Length == 0)
        {
            return null;
        }

        return new TranslationUnit(offset + start, offset + end, masked.ToString(), gaps);
    }

    private static void AppendGap(StringBuilder builder, List<string> gaps, string gap)
    {
        if (gap.Length == 0)
        {
            return;
        }

        builder.Append('⟦').Append(gaps.Count).Append('⟧');
        gaps.Add(gap);
    }

    private static string ReassembleUnit(TranslationUnit unit, string translated)
    {
        if (unit.Gaps.Count == 0)
        {
            return translated;
        }

        var builder = new StringBuilder(translated.Length + 32);
        var visited = new HashSet<int>();
        var position = 0;

        while (position < translated.Length)
        {
            var match = PlaceholderPattern.Match(translated, position);
            if (!match.Success)
            {
                break;
            }

            builder.Append(translated, position, match.Index - position);

            if (!int.TryParse(match.Groups[1].Value, out var gapIndex) ||
                gapIndex < 0 ||
                gapIndex >= unit.Gaps.Count)
            {
                throw new TranslationException("翻譯結果包含無效的結構佔位符。");
            }

            if (!visited.Add(gapIndex))
            {
                throw new TranslationException("翻譯結果重複包含結構佔位符。");
            }

            builder.Append(unit.Gaps[gapIndex]);
            position = match.Index + match.Length;
        }

        builder.Append(translated, position, translated.Length - position);

        if (visited.Count != unit.Gaps.Count)
        {
            throw new TranslationException("翻譯結果遺漏了部分結構佔位符。");
        }

        return builder.ToString();
    }

    private static int FindProtectedPrefixLength(string text)
    {
        var prefixLength = 0;
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            prefixLength = 1;
        }

        var frontMatterLength = FindFrontMatterLength(text, prefixLength);
        return frontMatterLength > 0 ? frontMatterLength : prefixLength;
    }

    private static int FindFrontMatterLength(string text, int start)
    {
        if (!StartsWithAt(text, start, "---"))
        {
            return 0;
        }

        var firstLineEnd = FindLineEnd(text, start);
        if (firstLineEnd < 0 || text[start..firstLineEnd].TrimEnd() != "---")
        {
            return 0;
        }

        var position = firstLineEnd + GetNewLineLength(text, firstLineEnd);

        while (position < text.Length)
        {
            var lineEnd = FindLineEnd(text, position);
            var line = lineEnd < 0
                ? text[position..]
                : text[position..lineEnd];

            var trimmed = line.TrimEnd();
            if (trimmed is "---" or "...")
            {
                return lineEnd < 0
                    ? text.Length
                    : lineEnd + GetNewLineLength(text, lineEnd);
            }

            if (lineEnd < 0)
            {
                break;
            }

            position = lineEnd + GetNewLineLength(text, lineEnd);
        }

        return 0;
    }

    private static bool IsInsideReferenceLink(LiteralInline literal)
    {
        for (var parent = literal.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is LinkInline link &&
                (link.IsShortcut || link.LinkRefDefLabel is not null || link.Reference is not null))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithAt(string text, int start, string value)
    {
        return start + value.Length <= text.Length &&
               string.CompareOrdinal(text, start, value, 0, value.Length) == 0;
    }

    private static int FindLineEnd(string text, int start)
    {
        for (var index = start; index < text.Length; index++)
        {
            if (text[index] is '\r' or '\n')
            {
                return index;
            }
        }

        return -1;
    }

    private static int GetNewLineLength(string text, int index)
    {
        return text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n'
            ? 2
            : 1;
    }
}
