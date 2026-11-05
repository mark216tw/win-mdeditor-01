using System.Globalization;
using Markdig;
using Markdig.Extensions.Footnotes;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace VibeCode.MdEditor.Core.Services;

public static class MarkdownPipelineFactory
{
    public static MarkdownPipeline Create(bool includeLineNumbers = false)
    {
        var builder = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml();

        if (includeLineNumbers)
        {
            builder.DocumentProcessed += AttachSourceLines;
        }

        return builder.Build();
    }

    private static void AttachSourceLines(MarkdownDocument document)
    {
        foreach (var block in document.Descendants<Block>())
        {
            if (block is MarkdownDocument
                or LinkReferenceDefinitionGroup
                or LinkReferenceDefinition
                or FootnoteGroup
                or Footnote)
            {
                continue;
            }

            block.GetAttributes().AddProperty(
                "data-line",
                (block.Line + 1).ToString(CultureInfo.InvariantCulture));
        }
    }
}
