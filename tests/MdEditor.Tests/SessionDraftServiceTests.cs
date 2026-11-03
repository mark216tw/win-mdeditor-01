using VibeCode.MdEditor.Core.Models;
using VibeCode.MdEditor.Core.Services;
using Xunit;

namespace VibeCode.MdEditor.Tests;

public sealed class SessionDraftServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RestoresDocumentsAndActiveDocument()
    {
        using var temp = new TempDirectory();
        var service = new SessionDraftService(temp.DirectoryPath);
        var filePath = temp.Combine("note.md");

        await service.SaveAsync(new DraftSession(
            "doc2",
            "Preview",
            [
                CreateSnapshot("doc1", "未命名 1", null, "draft one", isDirty: true),
                CreateSnapshot("doc2", "note.md", filePath, "draft two", isDirty: false),
            ]));

        var session = await service.LoadAsync();

        Assert.NotNull(session);
        Assert.Equal("doc2", session.ActiveDocumentId);
        Assert.Equal("Preview", session.ViewMode);
        Assert.Equal(2, session.Documents.Count);
        Assert.Equal("draft one", session.Documents[0].Text);
        Assert.True(session.Documents[0].IsDirty);
        Assert.Equal(filePath, session.Documents[1].FilePath);
        Assert.Equal("draft two", session.Documents[1].Text);
    }

    [Fact]
    public async Task DeleteDocument_RemovesItsDraftFile()
    {
        using var temp = new TempDirectory();
        var service = new SessionDraftService(temp.DirectoryPath);

        await service.SaveAsync(new DraftSession(
            null,
            "Split",
            [
                CreateSnapshot("doc1", "one", null, "one", isDirty: true),
                CreateSnapshot("doc2", "two", null, "two", isDirty: true),
            ]));

        await service.DeleteDocumentAsync("doc1");

        Assert.False(File.Exists(Path.Combine(service.DraftsDirectory, "doc1.json")));
        Assert.True(File.Exists(Path.Combine(service.DraftsDirectory, "doc2.json")));
    }

    [Fact]
    public async Task SavingWithoutDocument_DeletesOrphanDraft()
    {
        using var temp = new TempDirectory();
        var service = new SessionDraftService(temp.DirectoryPath);

        await service.SaveAsync(new DraftSession(
            null,
            "Split",
            [
                CreateSnapshot("doc1", "one", null, "one", isDirty: true),
                CreateSnapshot("doc2", "two", null, "two", isDirty: true),
            ]));

        await service.SaveAsync(new DraftSession(
            "doc2",
            "Split",
            [CreateSnapshot("doc2", "two", null, "two", isDirty: true)]));

        var session = await service.LoadAsync();

        Assert.NotNull(session);
        Assert.Single(session.Documents);
        Assert.False(File.Exists(Path.Combine(service.DraftsDirectory, "doc1.json")));
    }

    [Fact]
    public async Task Load_SkipsMissingAndCorruptDrafts()
    {
        using var temp = new TempDirectory();
        var service = new SessionDraftService(temp.DirectoryPath);

        await service.SaveAsync(new DraftSession(
            null,
            "Split",
            [
                CreateSnapshot("doc1", "one", null, "one", isDirty: true),
                CreateSnapshot("doc2", "two", null, "two", isDirty: true),
                CreateSnapshot("doc3", "three", null, "three", isDirty: true),
            ]));

        File.Delete(Path.Combine(service.DraftsDirectory, "doc1.json"));
        await File.WriteAllTextAsync(
            Path.Combine(service.DraftsDirectory, "doc2.json"),
            "{ not valid json");

        var session = await service.LoadAsync();

        Assert.NotNull(session);
        Assert.Single(session.Documents);
        Assert.Equal("doc3", session.Documents[0].DocumentId);
    }

    private static DraftDocumentSnapshot CreateSnapshot(
        string id,
        string title,
        string? filePath,
        string text,
        bool isDirty)
    {
        return new DraftDocumentSnapshot(
            id,
            title,
            filePath,
            text,
            "UTF-8",
            65001,
            false,
            "LF",
            isDirty,
            "hash",
            DateTimeOffset.UtcNow);
    }
}
