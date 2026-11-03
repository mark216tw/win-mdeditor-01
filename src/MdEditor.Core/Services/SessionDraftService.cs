using System.Text.Json;
using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Core.Services;

public interface ISessionDraftService
{
    string DraftsDirectory { get; }

    Task<DraftSession?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DraftSession session, CancellationToken cancellationToken = default);

    Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default);
}

public sealed class SessionDraftService : ISessionDraftService
{
    public const int CurrentSchemaVersion = 1;

    private const string ManifestFileName = "session.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _manifestPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SessionDraftService(string baseDirectory)
    {
        DraftsDirectory = Path.Combine(baseDirectory, "drafts");
        _manifestPath = Path.Combine(DraftsDirectory, ManifestFileName);
    }

    public string DraftsDirectory { get; }

    public async Task<DraftSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!File.Exists(_manifestPath))
            {
                return null;
            }

            DraftManifest? manifest;

            try
            {
                var json = await File.ReadAllTextAsync(_manifestPath, cancellationToken).ConfigureAwait(false);
                manifest = JsonSerializer.Deserialize<DraftManifest>(json, JsonOptions);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return null;
            }

            if (manifest is null || manifest.SchemaVersion != CurrentSchemaVersion)
            {
                return null;
            }

            var documents = new List<DraftDocumentSnapshot>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in manifest.Documents ?? [])
            {
                if (string.IsNullOrWhiteSpace(entry.DocumentId) || !seen.Add(entry.DocumentId))
                {
                    continue;
                }

                var draftPath = GetDocumentPath(entry.DocumentId);

                if (!File.Exists(draftPath))
                {
                    continue;
                }

                try
                {
                    var json = await File.ReadAllTextAsync(draftPath, cancellationToken).ConfigureAwait(false);
                    var draft = JsonSerializer.Deserialize<DraftDocumentFile>(json, JsonOptions);

                    if (draft is null ||
                        !string.Equals(draft.DocumentId, entry.DocumentId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    documents.Add(new DraftDocumentSnapshot(
                        entry.DocumentId,
                        entry.Title,
                        entry.FilePath,
                        draft.Text ?? string.Empty,
                        entry.EncodingName,
                        entry.CodePage,
                        entry.HasBom,
                        entry.LineEnding,
                        entry.IsDirty,
                        entry.LastKnownHash,
                        entry.LastWriteTimeUtc));
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                }
            }

            if (documents.Count == 0)
            {
                return null;
            }

            return new DraftSession(
                manifest.ActiveDocumentId,
                NormalizeViewMode(manifest.ViewMode),
                documents);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(DraftSession session, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(DraftsDirectory);

            var entries = new List<DraftManifestEntry>(session.Documents.Count);

            foreach (var document in session.Documents)
            {
                await WriteJsonAtomicAsync(
                    GetDocumentPath(document.DocumentId),
                    new DraftDocumentFile
                    {
                        DocumentId = document.DocumentId,
                        Text = document.Text,
                    },
                    cancellationToken).ConfigureAwait(false);

                entries.Add(new DraftManifestEntry
                {
                    DocumentId = document.DocumentId,
                    Title = document.Title,
                    FilePath = document.FilePath,
                    IsDirty = document.IsDirty,
                    EncodingName = document.EncodingName,
                    CodePage = document.CodePage,
                    HasBom = document.HasBom,
                    LineEnding = document.LineEnding,
                    LastKnownHash = document.LastKnownHash,
                    LastWriteTimeUtc = document.LastWriteTimeUtc,
                });
            }

            await WriteJsonAtomicAsync(
                _manifestPath,
                new DraftManifest
                {
                    SchemaVersion = CurrentSchemaVersion,
                    ActiveDocumentId = session.ActiveDocumentId,
                    ViewMode = NormalizeViewMode(session.ViewMode),
                    Documents = entries,
                },
                cancellationToken).ConfigureAwait(false);

            DeleteOrphanDraftFiles(entries);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var path = GetDocumentPath(documentId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private string GetDocumentPath(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId) ||
            documentId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("暫存文件識別碼無效。", nameof(documentId));
        }

        return Path.Combine(DraftsDirectory, documentId + ".json");
    }

    private void DeleteOrphanDraftFiles(IReadOnlyList<DraftManifestEntry> entries)
    {
        var allowed = new HashSet<string>(
            entries.Select(entry => entry.DocumentId + ".json"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(DraftsDirectory, "*.json"))
        {
            var fileName = Path.GetFileName(path);

            if (string.Equals(fileName, ManifestFileName, StringComparison.OrdinalIgnoreCase) ||
                allowed.Contains(fileName))
            {
                continue;
            }

            File.Delete(path);
        }
    }

    private static async Task WriteJsonAtomicAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string NormalizeViewMode(string? viewMode)
    {
        return viewMode is "Edit" or "Preview" or "Split" ? viewMode : "Split";
    }
}
