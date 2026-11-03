using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace VibeCode.MdEditor.Core.Services;

public interface IDocxExportService
{
    Task ExportAsync(
        string markdown,
        string outputPath,
        string? documentDirectory,
        CancellationToken cancellationToken = default);
}

public sealed class DocxExportService : IDocxExportService
{
    private readonly MarkdownPipeline _pipeline;

    public DocxExportService()
        : this(MarkdownPipelineFactory.Create())
    {
    }

    public DocxExportService(MarkdownPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public Task ExportAsync(
        string markdown,
        string outputPath,
        string? documentDirectory,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => Export(markdown, outputPath, documentDirectory, cancellationToken),
            cancellationToken);
    }

    private void Export(string markdown, string outputPath, string? documentDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("無法判斷匯出目錄。");
        }

        Directory.CreateDirectory(directory);

        var document = Markdown.Parse(markdown ?? string.Empty, _pipeline);

        using var wordDocument = WordprocessingDocument.Create(fullPath, WordprocessingDocumentType.Document);
        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new Document();
        AddStyles(mainPart);

        var body = new Body();
        mainPart.Document.Append(body);

        var writer = new WordWriter(mainPart, body, documentDirectory);
        writer.Write(document, cancellationToken);

        body.Append(new SectionProperties(
            new PageSize { Width = 11906U, Height = 16838U },
            new PageMargin
            {
                Top = 1134,
                Bottom = 1134,
                Left = 1134,
                Right = 1134,
                Header = 720U,
                Footer = 720U,
                Gutter = 0U,
            }));

        wordDocument.PackageProperties.Title = Path.GetFileNameWithoutExtension(fullPath);
        mainPart.Document.Save();

        Validate(wordDocument);
    }

    private static void Validate(WordprocessingDocument document)
    {
        var errors = new OpenXmlValidator().Validate(document).Take(1).ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException("Word 文件驗證失敗：" + errors[0].Description);
        }
    }

    private static void AddStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        styles.Append(new DocDefaults(
            new RunPropertiesDefault(
                new RunPropertiesBaseStyle(
                    new RunFonts
                    {
                        Ascii = "Calibri",
                        HighAnsi = "Calibri",
                        EastAsia = "Microsoft JhengHei",
                    },
                    new FontSize { Val = "22" },
                    new FontSizeComplexScript { Val = "22" }))));

        var normal = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true,
        };
        normal.Append(new StyleName { Val = "Normal" });
        normal.Append(new StyleRunProperties(
            new RunFonts
            {
                Ascii = "Calibri",
                HighAnsi = "Calibri",
                EastAsia = "Microsoft JhengHei",
            },
            new FontSize { Val = "22" },
            new FontSizeComplexScript { Val = "22" }));
        styles.Append(normal);

        AddHeadingStyle(styles, 1, 32, "1F3864");
        AddHeadingStyle(styles, 2, 28, "2E5496");
        AddHeadingStyle(styles, 3, 26, "1F4E79");
        AddHeadingStyle(styles, 4, 24, "404040");
        AddHeadingStyle(styles, 5, 22, "404040");
        AddHeadingStyle(styles, 6, 22, "595959");

        var codeBlock = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "CodeBlock",
        };
        codeBlock.Append(new StyleName { Val = "Code Block" });
        codeBlock.Append(new BasedOn { Val = "Normal" });
        codeBlock.Append(new StyleParagraphProperties(
            new Shading { Val = ShadingPatternValues.Clear, Fill = "F5F5F5" },
            new SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto },
            new Indentation { Left = "360", Right = "360" }));
        codeBlock.Append(new StyleRunProperties(
            new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas", EastAsia = "Consolas" },
            new FontSize { Val = "18" },
            new FontSizeComplexScript { Val = "18" }));
        styles.Append(codeBlock);

        var quote = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "Quote",
        };
        quote.Append(new StyleName { Val = "Quote" });
        quote.Append(new BasedOn { Val = "Normal" });
        quote.Append(new StyleParagraphProperties(
            new ParagraphBorders(
                new LeftBorder
                {
                    Val = BorderValues.Single,
                    Size = 12,
                    Space = 8,
                    Color = "C8C8C8",
                }),
            new SpacingBetweenLines { Before = "60", After = "60" },
            new Indentation { Left = "480", Right = "240" }));
        quote.Append(new StyleRunProperties(
            new Italic(),
            new Color { Val = "595959" }));
        styles.Append(quote);

        var listParagraph = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "ListParagraph",
        };
        listParagraph.Append(new StyleName { Val = "List Paragraph" });
        listParagraph.Append(new BasedOn { Val = "Normal" });
        listParagraph.Append(new StyleParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = "60" },
            new Indentation { Left = "720", Hanging = "360" }));
        styles.Append(listParagraph);

        var tableText = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "TableText",
        };
        tableText.Append(new StyleName { Val = "Table Text" });
        tableText.Append(new BasedOn { Val = "Normal" });
        tableText.Append(new StyleParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = "0" }));
        tableText.Append(new StyleRunProperties(
            new FontSize { Val = "20" },
            new FontSizeComplexScript { Val = "20" }));
        styles.Append(tableText);

        var hyperlink = new Style
        {
            Type = StyleValues.Character,
            StyleId = "Hyperlink",
        };
        hyperlink.Append(new StyleName { Val = "Hyperlink" });
        hyperlink.Append(new StyleRunProperties(
            new Color { Val = "0563C1" },
            new Underline { Val = UnderlineValues.Single }));
        styles.Append(hyperlink);

        stylesPart.Styles = styles;
    }

    private static void AddHeadingStyle(Styles styles, int level, int halfPointSize, string color)
    {
        var style = new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = $"Heading{level}",
        };
        style.Append(new StyleName { Val = $"heading {level}" });
        style.Append(new BasedOn { Val = "Normal" });
        style.Append(new NextParagraphStyle { Val = "Normal" });
        style.Append(new StyleParagraphProperties(
            new KeepNext(),
            new SpacingBetweenLines
            {
                Before = level == 1 ? "360" : "240",
                After = "120",
            },
            new OutlineLevel { Val = level - 1 }));
        style.Append(new StyleRunProperties(
            new Bold(),
            new Color { Val = color },
            new FontSize { Val = halfPointSize.ToString(CultureInfo.InvariantCulture) }));
        styles.Append(style);
    }

    private sealed class WordWriter(MainDocumentPart mainPart, Body body, string? baseDirectory)
    {
        private readonly MainDocumentPart _mainPart = mainPart;
        private readonly Body _body = body;
        private readonly string? _baseDirectory = baseDirectory;
        private int _imageId = 1;

        public void Write(MarkdownDocument document, CancellationToken cancellationToken)
        {
            foreach (var block in document)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendBlock(_body, block);
            }
        }

        private void AppendBlock(OpenXmlElement container, Block block)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    AppendParagraph(container, heading.Inline, $"Heading{Math.Clamp(heading.Level, 1, 6)}");
                    break;

                case ParagraphBlock paragraph:
                    AppendParagraph(container, paragraph.Inline, "Normal");
                    break;

                case ListBlock list:
                    AppendList(container, list, 0);
                    break;

                case QuoteBlock quote:
                    AppendQuote(container, quote);
                    break;

                case FencedCodeBlock fenced:
                    AppendCodeBlock(container, fenced);
                    break;

                case CodeBlock code:
                    AppendCodeBlock(container, code);
                    break;

                case ThematicBreakBlock:
                    AppendThematicBreak(container);
                    break;

                case MdTable table:
                    AppendTable(container, table);
                    break;

                case HtmlBlock:
                case LinkReferenceDefinitionGroup:
                case FootnoteGroup:
                    break;

                case ContainerBlock containerBlock:
                    foreach (var child in containerBlock)
                    {
                        AppendBlock(container, child);
                    }

                    break;
            }
        }

        private void AppendQuote(OpenXmlElement container, QuoteBlock quote)
        {
            foreach (var child in quote)
            {
                switch (child)
                {
                    case ParagraphBlock paragraph:
                        AppendParagraph(container, paragraph.Inline, "Quote");
                        break;
                    case ListBlock list:
                        AppendList(container, list, 0);
                        break;
                    default:
                        AppendBlock(container, child);
                        break;
                }
            }
        }

        private void AppendList(OpenXmlElement container, ListBlock list, int level)
        {
            var index = 1;
            if (list.IsOrdered &&
                int.TryParse(list.OrderedStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start))
            {
                index = Math.Max(1, start);
            }

            foreach (var child in list)
            {
                if (child is not ListItemBlock item)
                {
                    continue;
                }

                var first = true;
                foreach (var block in item)
                {
                    if (block is ParagraphBlock paragraph)
                    {
                        var sentence = CreateParagraph("ListParagraph");
                        sentence.ParagraphProperties!.Append(new Indentation
                        {
                            Left = (720 * (level + 1)).ToString(CultureInfo.InvariantCulture),
                            Hanging = "360",
                        });

                        if (first)
                        {
                            var prefix = list.IsOrdered
                                ? index.ToString(CultureInfo.InvariantCulture) + ". "
                                : "• ";
                            sentence.Append(new Run(
                                new RunProperties(new Bold()),
                                new Text(prefix) { Space = SpaceProcessingModeValues.Preserve }));
                        }

                        AppendInlines(sentence, paragraph.Inline, default);
                        if (sentence.ChildElements.Count <= 1)
                        {
                            sentence.Append(CreateRun(string.Empty, default));
                        }

                        container.Append(sentence);
                        first = false;
                    }
                    else if (block is ListBlock nested)
                    {
                        AppendList(container, nested, level + 1);
                    }
                    else
                    {
                        AppendBlock(container, block);
                    }
                }

                index++;
            }
        }

        private void AppendTable(OpenXmlElement container, MdTable table)
        {
            var wordTable = new Table();

            wordTable.Append(new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" }),
                new TableLayout { Type = TableLayoutValues.Autofit }));

            var columnCount = table.ColumnDefinitions.Count;
            if (columnCount <= 0)
            {
                columnCount = table.OfType<MdTableRow>()
                    .Select(row => row.OfType<MdTableCell>().Count())
                    .DefaultIfEmpty(1)
                    .Max();
            }

            var tableGrid = new TableGrid();
            for (var column = 0; column < Math.Max(1, columnCount); column++)
            {
                tableGrid.Append(new GridColumn());
            }

            wordTable.Append(tableGrid);

            foreach (var row in table.OfType<MdTableRow>())
            {
                var wordRow = new TableRow();
                foreach (var cell in row.OfType<MdTableCell>())
                {
                    wordRow.Append(CreateTableCell(cell, row.IsHeader));
                }

                wordTable.Append(wordRow);
            }

            container.Append(wordTable);
        }

        private TableCell CreateTableCell(MdTableCell cell, bool isHeader)
        {
            var wordCell = new TableCell();
            var properties = new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Auto });

            if (isHeader)
            {
                properties.Append(new Shading
                {
                    Val = ShadingPatternValues.Clear,
                    Fill = "F2F2F2",
                });
            }

            wordCell.Append(properties);

            var format = isHeader
                ? new RunFormat { Bold = true }
                : default;

            var hasContent = false;
            foreach (var block in cell)
            {
                if (block is ParagraphBlock paragraph)
                {
                    var wordParagraph = CreateParagraph("TableText");
                    AppendInlines(wordParagraph, paragraph.Inline, format);
                    if (wordParagraph.ChildElements.Count <= 1)
                    {
                        wordParagraph.Append(CreateRun(string.Empty, format));
                    }

                    wordCell.Append(wordParagraph);
                    hasContent = true;
                }
                else if (block is ListBlock list)
                {
                    var text = string.Join(
                        " ",
                        list.OfType<ListItemBlock>()
                            .SelectMany(item => item.OfType<ParagraphBlock>())
                            .Select(p => p.Inline?.ToString())
                            .Where(t => !string.IsNullOrWhiteSpace(t)));
                    var wordParagraph = CreateParagraph("TableText");
                    AppendInlines(wordParagraph, null, format);
                    wordParagraph.Append(CreateRun(text ?? string.Empty, format));
                    wordCell.Append(wordParagraph);
                    hasContent = true;
                }
            }

            if (!hasContent)
            {
                var wordParagraph = CreateParagraph("TableText");
                wordParagraph.Append(CreateRun(string.Empty, format));
                wordCell.Append(wordParagraph);
            }

            return wordCell;
        }

        private void AppendCodeBlock(OpenXmlElement container, CodeBlock block)
        {
            var paragraph = CreateParagraph("CodeBlock");
            var lines = block.Lines.ToString().Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var run = new Run();

            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    run.Append(new Break());
                }

                run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            }

            if (run.ChildElements.Count == 0)
            {
                run.Append(new Text(string.Empty));
            }

            paragraph.Append(run);
            container.Append(paragraph);
        }

        private void AppendThematicBreak(OpenXmlElement container)
        {
            var paragraph = new Paragraph(new ParagraphProperties(
                new ParagraphStyleId { Val = "Normal" },
                new ParagraphBorders(
                    new BottomBorder
                    {
                        Val = BorderValues.Single,
                        Size = 6,
                        Space = 1,
                        Color = "BFBFBF",
                    })));

            container.Append(paragraph);
        }

        private void AppendParagraph(OpenXmlElement container, ContainerInline? inline, string styleId)
        {
            var paragraph = CreateParagraph(styleId);
            AppendInlines(paragraph, inline, default);
            if (paragraph.ChildElements.Count <= 1)
            {
                paragraph.Append(CreateRun(string.Empty, default));
            }

            container.Append(paragraph);
        }

        private void AppendInlines(OpenXmlElement container, ContainerInline? inline, RunFormat format)
        {
            if (inline is null)
            {
                return;
            }

            foreach (var child in inline)
            {
                switch (child)
                {
                    case TaskList task:
                        container.Append(CreateRun(task.Checked ? "☒ " : "☐ ", format));
                        break;

                    case LiteralInline literal:
                        container.Append(CreateRun(literal.Content.ToString(), format));
                        break;

                    case CodeInline code:
                        container.Append(CreateRun(code.Content, format with { Code = true }));
                        break;

                    case EmphasisInline emphasis:
                        AppendInlines(container, emphasis, ApplyEmphasis(format, emphasis));
                        break;

                    case LineBreakInline lineBreak:
                        container.Append(new Run(new Break()));
                        if (!lineBreak.IsHard)
                        {
                            container.Append(CreateRun(" ", format));
                        }

                        break;

                    case LinkInline link:
                        AppendLink(container, link, format);
                        break;

                    case AutolinkInline autolink:
                        AppendAutolink(container, autolink, format);
                        break;

                    case HtmlInline:
                        break;

                    case ContainerInline nested:
                        AppendInlines(container, nested, format);
                        break;

                    default:
                        var text = child.ToString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            container.Append(CreateRun(text, format));
                        }

                        break;
                }
            }
        }

        private void AppendLink(OpenXmlElement container, LinkInline link, RunFormat format)
        {
            if (link.IsImage)
            {
                var imageRun = CreateImageRun(link);
                if (imageRun is not null)
                {
                    container.Append(imageRun);
                    return;
                }

                AppendInlines(container, link, format with { Italic = true });
                return;
            }

            var url = link.Url ?? string.Empty;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsSupportedScheme(uri))
            {
                var relationship = _mainPart.AddHyperlinkRelationship(uri, true);
                var hyperlink = new Hyperlink
                {
                    Id = relationship.Id,
                    History = true,
                };

                if (link.FirstChild is null)
                {
                    hyperlink.Append(CreateRun(url, format with { Link = true, Code = false }));
                }
                else
                {
                    AppendInlines(hyperlink, link, format with { Link = true, Code = false });
                }

                container.Append(hyperlink);
                return;
            }

            if (url.StartsWith('#'))
            {
                var anchor = new Hyperlink
                {
                    Anchor = url[1..],
                    History = true,
                };

                if (link.FirstChild is null)
                {
                    anchor.Append(CreateRun(url, format with { Link = true, Code = false }));
                }
                else
                {
                    AppendInlines(anchor, link, format with { Link = true, Code = false });
                }

                container.Append(anchor);
                return;
            }

            AppendInlines(container, link, format);
        }

        private void AppendAutolink(OpenXmlElement container, AutolinkInline autolink, RunFormat format)
        {
            var raw = autolink.Url ?? string.Empty;
            var url = autolink.IsEmail ? "mailto:" + raw : raw;

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsSupportedScheme(uri))
            {
                var relationship = _mainPart.AddHyperlinkRelationship(uri, true);
                container.Append(new Hyperlink(
                    CreateRun(raw, format with { Link = true, Code = false }))
                {
                    Id = relationship.Id,
                    History = true,
                });
                return;
            }

            container.Append(CreateRun(raw, format));
        }

        private static bool IsSupportedScheme(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttp ||
                   uri.Scheme == Uri.UriSchemeHttps ||
                   uri.Scheme == Uri.UriSchemeMailto;
        }

        private static RunFormat ApplyEmphasis(RunFormat format, EmphasisInline emphasis)
        {
            if (emphasis.DelimiterChar == '~')
            {
                return format with { Strike = true };
            }

            return emphasis.DelimiterCount >= 2
                ? format with { Bold = true }
                : format with { Italic = true };
        }

        private Run? CreateImageRun(LinkInline link)
        {
            var url = link.Url;
            if (string.IsNullOrWhiteSpace(url) ||
                url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string? path = null;
            if (Path.IsPathRooted(url))
            {
                path = url;
            }
            else if (!string.IsNullOrEmpty(_baseDirectory))
            {
                path = Path.GetFullPath(Path.Combine(
                    _baseDirectory,
                    url.Replace('/', Path.DirectorySeparatorChar)));
            }

            if (path is null || !File.Exists(path))
            {
                return null;
            }

            var extension = Path.GetExtension(path).ToLowerInvariant();
            PartTypeInfo? partType = extension switch
            {
                ".png" => ImagePartType.Png,
                ".jpg" or ".jpeg" => ImagePartType.Jpeg,
                ".gif" => ImagePartType.Gif,
                ".bmp" => ImagePartType.Bmp,
                ".tif" or ".tiff" => ImagePartType.Tiff,
                ".emf" => ImagePartType.Emf,
                ".wmf" => ImagePartType.Wmf,
                _ => null,
            };

            if (partType is null)
            {
                return null;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception)
            {
                return null;
            }

            if (bytes.Length == 0)
            {
                return null;
            }

            var size = ImageSizeReader.TryRead(bytes) ?? (480, 320);
            var (width, height) = ConstrainSize(size.Width, size.Height, 600, 700);

            var imagePart = _mainPart.AddImagePart(partType.Value);
            using (var stream = new MemoryStream(bytes))
            {
                imagePart.FeedData(stream);
            }

            var relationshipId = _mainPart.GetIdOfPart(imagePart);
            var imageId = (uint)_imageId++;
            long cx = width * 9525L;
            long cy = height * 9525L;

            var inline = new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.DocProperties { Id = imageId, Name = $"Image {imageId}" },
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties
                                {
                                    Id = 0U,
                                    Name = Path.GetFileName(path),
                                },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                {
                                    Preset = A.ShapeTypeValues.Rectangle,
                                })))
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
                    }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U,
            };

            return new Run(new Drawing(inline));
        }

        private static (int Width, int Height) ConstrainSize(int width, int height, int maxWidth, int maxHeight)
        {
            if (width <= 0 || height <= 0)
            {
                return (480, 320);
            }

            var scale = Math.Min(1d, Math.Min(
                maxWidth / (double)width,
                maxHeight / (double)height));

            return (
                Math.Max(1, (int)Math.Round(width * scale)),
                Math.Max(1, (int)Math.Round(height * scale)));
        }

        private static Paragraph CreateParagraph(string styleId)
        {
            return new Paragraph(new ParagraphProperties(
                new ParagraphStyleId { Val = styleId }));
        }

        private static Run CreateRun(string text, RunFormat format)
        {
            var run = new Run();
            var properties = new RunProperties();

            if (format.Code)
            {
                properties.Append(new RunFonts
                {
                    Ascii = "Consolas",
                    HighAnsi = "Consolas",
                    EastAsia = "Consolas",
                });
            }

            if (format.Bold)
            {
                properties.Append(new Bold());
            }

            if (format.Italic)
            {
                properties.Append(new Italic());
            }

            if (format.Strike)
            {
                properties.Append(new Strike());
            }

            if (format.Link)
            {
                properties.Append(new Color { Val = "0563C1" });
            }

            if (format.Code)
            {
                properties.Append(new FontSize { Val = "18" });
                properties.Append(new FontSizeComplexScript { Val = "18" });
            }

            if (format.Link)
            {
                properties.Append(new Underline { Val = UnderlineValues.Single });
            }

            if (format.Code)
            {
                properties.Append(new Shading
                {
                    Val = ShadingPatternValues.Clear,
                    Fill = "F2F2F2",
                });
            }

            if (properties.HasChildren)
            {
                run.Append(properties);
            }

            run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }

        private readonly record struct RunFormat
        {
            public bool Bold { get; init; }

            public bool Italic { get; init; }

            public bool Strike { get; init; }

            public bool Code { get; init; }

            public bool Link { get; init; }
        }
    }
}
