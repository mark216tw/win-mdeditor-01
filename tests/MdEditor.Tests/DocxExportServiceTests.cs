using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using VibeCode.MdEditor.Core.Services;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class DocxExportServiceTests
{
    [Fact]
    public async Task Export_CreatesValidDocxWithCoreContent()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("export.docx");

        const string markdown = """
            # 文件標題

            這是一段**粗體**與 `行內程式碼`。

            - 項目一
            - 項目二

            > 引用內容

            | 欄位 A | 欄位 B |
            | --- | --- |
            | 1 | 2 |

            ```csharp
            Console.WriteLine("hello");
            ```

            [範例連結](https://example.com)
            """;

        var service = new DocxExportService();
        await service.ExportAsync(markdown, path, temp.DirectoryPath);

        Assert.True(File.Exists(path));

        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart!.Document!.Body!;
        var text = body.InnerText;

        Assert.Contains("文件標題", text);
        Assert.Contains("粗體", text);
        Assert.Contains("項目一", text);
        Assert.Contains("引用內容", text);
        Assert.Contains("欄位 A", text);
        Assert.Contains("Console.WriteLine", text);

        var errors = new OpenXmlValidator().Validate(document).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Export_PreservesMathSourceAsText()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("math.docx");

        const string markdown = """
            行內 $E = mc^2$

            $$
            \int_0^1 x^2 dx
            $$
            """;

        var service = new DocxExportService();
        await service.ExportAsync(markdown, path, temp.DirectoryPath);

        using var document = WordprocessingDocument.Open(path, false);
        var text = document.MainDocumentPart!.Document!.Body!.InnerText;

        Assert.Contains("$E = mc^2$", text);
        Assert.Contains(@"\int_0^1 x^2 dx", text);
        Assert.DoesNotContain("Markdig.Extensions.Mathematics", text);

        var errors = new OpenXmlValidator().Validate(document).ToList();
        Assert.Empty(errors);
    }
}
